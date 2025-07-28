namespace AudioManagerAPI.Features.Management
{
    using AudioManagerAPI.Cache;
    using AudioManagerAPI.Config;
    using AudioManagerAPI.Controllers;
    using AudioManagerAPI.Defaults;
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Features.Speakers;
    using AudioManagerAPI.Speakers.Extensions;
    using AudioManagerAPI.Speakers.State;
    using LabApi.Features.Wrappers;
    using MEC;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using UnityEngine;

    using Log = LabApi.Features.Console.Logger;

    /// <summary>
    /// Implements audio management with speaker lifecycle and caching for game audio playback.
    /// </summary>
    public partial class AudioManager : IAudioManager
    {
        private readonly Dictionary<byte, ISpeaker> speakers = new Dictionary<byte, ISpeaker>();
        private readonly AudioCache audioCache;
        private readonly ISpeakerFactory speakerFactory;
        private readonly object lockObject = new object();

        public event Action<byte> OnPlaybackStarted;
        public event Action<byte> OnPaused;
        public event Action<byte> OnResumed;
        public event Action<byte> OnStop;
        public event Action<byte, int> OnSkipped;
        public event Action<byte> OnQueueEmpty;

        /// <summary>
        /// Gets the currently active audio options used by the <see cref="AudioManager"/> singleton instance.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the <see cref="Instance"/> is not of type <see cref="AudioManager"/>.
        /// </exception>
        public AudioOptions Options { get; }
    
        /// <summary>
        /// Initializes a new instance of the <see cref="AudioManager"/> class.
        /// Loads settings from AudioConfig.json (via <see cref="AudioConfigLoader"/>),
        /// applies the configured cache size and speaker factory choice,
        /// and initializes the audio cache and options accordingly.
        /// </summary>
        /// <param name="speakerFactory">
        /// The factory used to create speaker instances; must not be null.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="speakerFactory"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the cache size loaded from configuration is not positive.
        /// </exception>
        public AudioManager(ISpeakerFactory speakerFactory)
        {

            this.speakerFactory = speakerFactory
                ?? throw new ArgumentNullException(nameof(speakerFactory));
        
            var config = AudioConfigLoader.LoadOrCreate();
        
            if (config.CacheSize <= 0)
                throw new ArgumentException(
                    "Cache size must be positive (loaded from AudioConfig.json).",
                    nameof(config.CacheSize)
                );
        
            this.audioCache = new AudioCache(config.CacheSize);
        
            Options = new AudioOptions
            {
                CacheSize = config.CacheSize,
                UseDefaultSpeakerFactory = config.UseDefaultSpeakerFactory,
                DefaultFadeInDuration = config.DefaultFadeInDuration,
                DefaultFadeOutDuration = config.DefaultFadeOutDuration,
            };
        }


        public void RegisterAudio(string key, Func<Stream> streamProvider)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (streamProvider == null)
                throw new ArgumentNullException(nameof(streamProvider));
            audioCache.Register(key, streamProvider);
        }

        public ISpeaker GetSpeaker(byte controllerId)
        {
            lock (lockObject)
            {
                return speakers.TryGetValue(controllerId, out var speaker) ? speaker : null;
            }
        }

        public bool IsValidController(byte controllerId)
        {
            lock (lockObject)
            {
                return controllerId != 0 && speakers.ContainsKey(controllerId);
            }
        }

        public byte PlayAudio(string key, Vector3 position, bool loop, float volume, float minDistance, float maxDistance, bool isSpatial, AudioPriority priority, Action<ISpeaker> configureSpeaker = null, bool queue = false, bool persistent = false, float? lifespan = null, bool autoCleanup = false)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            lock (lockObject)
            {
                float[] samples = audioCache.Get(key);
                if (samples == null)
                {
                    Log.Warn($"[AudioManagerAPI] Audio with key {key} not found.");
                    return 0;
                }

                byte controllerId = 0;
                ISpeaker speaker = null;

                ControllerIdManager.AllocateId(
                    priority,
                    null,
                    persistent,
                    persistent ? new SpeakerState
                    {
                        Key = queue ? null : key,
                        Position = position,
                        Loop = queue ? false : loop,
                        Volume = volume,
                        MinDistance = minDistance,
                        MaxDistance = maxDistance,
                        IsSpatial = isSpatial,
                        Priority = priority,
                        ConfigureSpeaker = configureSpeaker,
                        QueuedClips = queue ? new List<(string, bool)> { (key, loop) } : new List<(string, bool)>(),
                        PlayerFilter = null,
                        Persistent = persistent,
                        Lifespan = lifespan,
                        AutoCleanup = autoCleanup,
                        PlaybackPosition = 0f
                    } : null,
                    id =>
                    {
                        controllerId = id;
                        speaker = speakerFactory.CreateSpeaker(position, id);
                        if (speaker == null)
                        {
                            ControllerIdManager.ReleaseId(id);
                            Log.Warn($"[AudioManagerAPI] Failed to create speaker for ID {id}.");
                            return;
                        }

                        speaker.Configure(volume, minDistance, maxDistance, isSpatial, configureSpeaker);
                        speakers[id] = speaker;

                        if (queue)
                        {
                            speaker.Queue(samples, loop);
                        }
                        else
                        {
                            speaker.Play(samples, loop, 0f);
                            OnPlaybackStarted?.Invoke(id);
                        }

                        if (persistent)
                        {
                            if (speaker is ISpeakerWithPlayerFilter filterSpeaker && configureSpeaker != null)
                            {
                                var state = ControllerIdManager.GetSpeakerState(id) as SpeakerState;
                                if (state != null)
                                {
                                    configureSpeaker(speaker);
                                    state.PlayerFilter = filterSpeaker.ValidPlayers;
                                }
                            }
                        }

                        if (autoCleanup || lifespan.HasValue)
                        {
                            speaker.StartAutoStop(id, lifespan ?? float.MaxValue, autoCleanup, newId => FadeOutAudio(id, this.Options.DefaultFadeOutDuration));
                        }

                        ControllerIdManager.UpdateStopCallback(id, () => FadeOutAudio(id, this.Options.DefaultFadeOutDuration));
                        Log.Debug($"[AudioManagerAPI] Played audio {key} with ID {id} at {Timing.LocalTime}.");
                    },
                    () => Log.Warn($"[AudioManagerAPI] Failed to allocate ID for audio {key}."));
                return controllerId;
            }
        }

        public byte PlayGlobalAudio(string key, bool loop, float volume, AudioPriority priority, bool queue = false, float fadeInDuration = 0f, bool persistent = false, float? lifespan = null, bool autoCleanup = false)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            byte controllerId = PlayAudio(
                key, Vector3.zero, loop, volume, 0f, 999.99f, false, priority,
                speaker =>
                {
                    if (speaker is ISpeakerWithPlayerFilter filterSpeaker)
                    {
                        filterSpeaker.SetValidPlayers(p => Player.ReadyList.Contains(p));
                    }
                }, queue, persistent, lifespan, autoCleanup);

            if (controllerId != 0 && fadeInDuration > 0)
            {
                lock (lockObject)
                {
                    if (persistent && speakers.ContainsKey(controllerId))
                    {
                        var state = ControllerIdManager.GetSpeakerState(controllerId) as SpeakerState;
                        if (state != null && speakers[controllerId] is ISpeakerWithPlayerFilter filterSpeaker)
                        {
                            state.PlayerFilter = p => Player.ReadyList.Contains(p);
                        }
                    }
                    FadeInAudio(controllerId, fadeInDuration);
                }
            }

            return controllerId;
        }

        public byte PlayGlobalAudioWithFilter(string key, bool loop, float volume, AudioPriority priority, Action<ISpeaker> configureSpeaker, bool queue = false, float fadeInDuration = 0f, bool persistent = false, float? lifespan = null, bool autoCleanup = false)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            byte controllerId = PlayAudio(
                key, Vector3.zero, loop, volume, 0f, 999.99f, false, priority,
                configureSpeaker, queue, persistent, lifespan, autoCleanup);

            if (controllerId != 0 && fadeInDuration > 0)
            {
                lock (lockObject)
                {
                    FadeInAudio(controllerId, fadeInDuration);
                }
            }

            return controllerId;
        }

        public bool SetSpeakerVolume(byte controllerId, float volume)
        {
            if (volume < 0f || volume > 1f)
                throw new ArgumentOutOfRangeException(nameof(volume), "Volume must be between 0.0 and 1.0.");

            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out var speaker))
                {
                    if (speaker is ISpeakerWithPlayerFilter filterSpeaker)
                    {
                        filterSpeaker.SetVolume(volume);
                        var state = ControllerIdManager.GetSpeakerState(controllerId) as SpeakerState;
                        if (state?.Persistent == true)
                        {
                            state.Volume = volume;
                        }
                        Log.Debug($"[AudioManagerAPI] Set volume to {volume} for controller ID {controllerId}.");
                        return true;
                    }
                }
                Log.Warn($"[AudioManagerAPI] Cannot set volume for controller ID {controllerId}: Speaker not found or not supported.");
                return false;
            }
        }

        public bool SetSpeakerPosition(byte controllerId, Vector3 position)
        {
            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out var speaker))
                {
                    if (speaker is ISpeakerWithPlayerFilter filterSpeaker)
                    {
                        filterSpeaker.SetPosition(position);
                        var state = ControllerIdManager.GetSpeakerState(controllerId) as SpeakerState;
                        if (state?.Persistent == true)
                        {
                            state.Position = position;
                        }
                        Log.Debug($"[AudioManagerAPI] Set position to {position} for controller ID {controllerId}.");
                        return true;
                    }
                }
                Log.Warn($"[AudioManagerAPI] Cannot set position for controller ID {controllerId}: Speaker not found or not supported.");
                return false;
            }
        }

        public bool RecoverSpeaker(byte controllerId, bool resetPlayback = false)
        {
            lock (lockObject)
            {
                var state = ControllerIdManager.GetSpeakerState(controllerId) as SpeakerState;
                if (state == null || !state.Persistent)
                {
                    Log.Warn($"[AudioManagerAPI] Cannot recover speaker with ID {controllerId}: Not persistent or state not found.");
                    return false;
                }

                if (!state.ValidateState())
                {
                    Log.Warn($"[AudioManagerAPI] Cannot recover speaker with ID {controllerId}: Invalid state.");
                    return false;
                }

                byte newId = 0;
                ControllerIdManager.AllocateId(
                    state.Priority,
                    null,
                    true,
                    state,
                    id =>
                    {
                        newId = id;
                        ControllerIdManager.UpdateStopCallback(id, () => FadeOutAudio(id, this.Options.DefaultFadeOutDuration));
                    },
                    () => Log.Warn($"[AudioManagerAPI] Failed to allocate new ID for recovering speaker {controllerId}."));
                if (newId == 0)
                    return false;

                ISpeaker speaker = speakerFactory.CreateSpeaker(state.Position, newId);
                if (speaker == null)
                {
                    ControllerIdManager.ReleaseId(newId);
                    Log.Warn($"[AudioManagerAPI] Failed to create speaker for recovering ID {controllerId}.");
                    return false;
                }

                speaker.Configure(state.Volume, state.MinDistance, state.MaxDistance, state.IsSpatial, state.ConfigureSpeaker, state.PlayerFilter);
                speakers[newId] = speaker;

                if (state.QueuedClips.Any())
                {
                    speaker.RestoreQueue(state, audioCache);
                    if (resetPlayback)
                        state.PlaybackPosition = 0f;
                    var firstSamples = audioCache.Get(state.QueuedClips[0].key);
                    if (firstSamples != null)
                    {
                        speaker.Play(firstSamples, state.QueuedClips[0].loop, state.PlaybackPosition);
                        OnPlaybackStarted?.Invoke(newId);
                    }
                }
                else
                {
                    var samples = audioCache.Get(state.Key);
                    if (samples != null)
                    {
                        if (resetPlayback)
                            state.PlaybackPosition = 0f;
                        speaker.Play(samples, state.Loop, state.PlaybackPosition);
                        OnPlaybackStarted?.Invoke(newId);
                    }
                }

                if (state.AutoCleanup || state.Lifespan.HasValue)
                {
                    speaker.StartAutoStop(newId, state.Lifespan ?? float.MaxValue, state.AutoCleanup, id => FadeOutAudio(id, this.Options.DefaultFadeOutDuration));
                }

                Log.Debug($"[AudioManagerAPI] Recovered persistent speaker ID {controllerId} as {newId} at {Timing.LocalTime}.");
                return true;
            }
        }

        public void PauseAudio(byte controllerId)
        {
            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out var speaker))
                {
                    if (speaker is DefaultSpeakerToyAdapter adapter && ControllerIdManager.GetSpeakerState(controllerId) is SpeakerState state && state.Persistent)
                    {
                        speaker.UpdatePlaybackPosition(controllerId, state);
                    }
                    speaker.Pause();
                    OnPaused?.Invoke(controllerId);
                }
            }
        }

        public void ResumeAudio(byte controllerId)
        {
            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out var speaker))
                {
                    speaker.Resume();
                    OnResumed?.Invoke(controllerId);
                }
            }
        }

        public void SkipAudio(byte controllerId, int count)
        {
            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out var speaker))
                {
                    speaker.Skip(count);
                    OnSkipped?.Invoke(controllerId, count);
                    if (speaker is DefaultSpeakerToyAdapter adapter && adapter.IsQueueEmpty)
                    {
                        OnQueueEmpty?.Invoke(controllerId);
                    }
                }
            }
        }

        public void FadeInAudio(byte controllerId, float duration)
        {
            if (duration < 0f)
                throw new ArgumentOutOfRangeException(nameof(duration), "Fade-in duration must be non-negative.");

            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out var speaker))
                {
                    speaker.FadeIn(duration);
                }
            }
        }

        public void FadeOutAudio(byte controllerId, float duration)
        {
            if (duration < 0f)
                throw new ArgumentOutOfRangeException(nameof(duration), "Fade-out duration must be non-negative.");

            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out var speaker))
                {
                    if (speaker is DefaultSpeakerToyAdapter adapter && ControllerIdManager.GetSpeakerState(controllerId) is SpeakerState state && state.Persistent)
                    {
                        speaker.UpdatePlaybackPosition(controllerId, state);
                    }
                    speaker.FadeOut(duration, () =>
                    {
                        speakers.Remove(controllerId);
                        ControllerIdManager.ReleaseId(controllerId);
                        OnStop?.Invoke(controllerId);
                    });
                }
            }
        }

        public void StopAudio(byte controllerId)
        {
            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out var speaker))
                {
                    if (speaker is DefaultSpeakerToyAdapter adapter && ControllerIdManager.GetSpeakerState(controllerId) is SpeakerState state && state.Persistent)
                    {
                        speaker.UpdatePlaybackPosition(controllerId, state);
                    }
                    speaker.Stop();
                    OnStop?.Invoke(controllerId);
                }
            }
        }

        public (int queuedCount, string currentClip) GetQueueStatus(byte controllerId)
        {
            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out var speaker))
                {
                    var state = ControllerIdManager.GetSpeakerState(controllerId) as SpeakerState;
                    var (queuedCount, currentClip) = speaker.GetQueueStatus(state);
                    Log.Debug($"[AudioManagerAPI] Queue status for controller ID {controllerId}: {queuedCount} clips queued, current clip: {currentClip ?? "none"}.");
                    return (queuedCount, currentClip);
                }
                Log.Warn($"[AudioManagerAPI] Cannot get queue status for controller ID {controllerId}: Speaker not found.");
                return (0, null);
            }
        }

        public bool ClearSpeakerQueue(byte controllerId)
        {
            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out var speaker))
                {
                    var state = ControllerIdManager.GetSpeakerState(controllerId) as SpeakerState;
                    bool result = speaker.ClearQueue(state);
                    if (result)
                    {
                        Log.Debug($"[AudioManagerAPI] Cleared queue for controller ID {controllerId} at {Timing.LocalTime}.");
                    }
                    return result;
                }
                Log.Warn($"[AudioManagerAPI] Cannot clear queue for controller ID {controllerId}: Speaker not found.");
                return false;
            }
        }

        public void DestroySpeaker(byte controllerId, bool forceRemoveState = false)
        {
            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out var speaker))
                {
                    if (speaker is DefaultSpeakerToyAdapter adapter && ControllerIdManager.GetSpeakerState(controllerId) is SpeakerState state && state.Persistent)
                    {
                        speaker.UpdatePlaybackPosition(controllerId, state);
                    }
                    speaker.Stop();
                    speaker.Destroy();
                    speakers.Remove(controllerId);
                    ControllerIdManager.ReleaseId(controllerId, forceRemoveState);
                }
            }
        }

        public void CleanupAllSpeakers()
        {
            lock (lockObject)
            {
                foreach (var controllerId in new List<byte>(speakers.Keys))
                {
                    if (speakers.TryGetValue(controllerId, out var speaker))
                    {
                        if (speaker is DefaultSpeakerToyAdapter adapter && ControllerIdManager.GetSpeakerState(controllerId) is SpeakerState state && state.Persistent)
                        {
                            speaker.UpdatePlaybackPosition(controllerId, state);
                        }
                        speaker.Stop();
                        speaker.Destroy();
                        ControllerIdManager.ReleaseId(controllerId);
                    }
                }
                speakers.Clear();
                ControllerIdManager.CleanupEvictedSpeakers();
            }
        }
    }
}

namespace AudioManagerAPI.Features.Management
{
    using AudioManagerAPI.Cache;
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
        private const float DEFAULT_FADE_DURATION = 1f;

        public event Action<byte> OnPlaybackStarted;
        public event Action<byte> OnPaused;
        public event Action<byte> OnResumed;
        public event Action<byte> OnStop;
        public event Action<byte, int> OnSkipped;
        public event Action<byte> OnQueueEmpty;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioManager"/> class.
        /// </summary>
        /// <param name="speakerFactory">The factory used to create speaker instances.</param>
        /// <param name="cacheSize">The maximum number of audio samples to cache.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="speakerFactory"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="cacheSize"/> is not positive.</exception>
        public AudioManager(ISpeakerFactory speakerFactory, int cacheSize = 50)
        {
            this.speakerFactory = speakerFactory ?? throw new ArgumentNullException(nameof(speakerFactory));
            if (cacheSize <= 0)
                throw new ArgumentException("Cache size must be positive.", nameof(cacheSize));
            this.audioCache = new AudioCache(cacheSize);
        }

        public void RegisterAudio(string key, Func<Stream> streamProvider)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (streamProvider == null) throw new ArgumentNullException(nameof(streamProvider));
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
            lock (lockObject)
            {
                float[] samples = audioCache.Get(key);
                if (samples == null)
                {
                    Log.Warn($"[AudioManagerAPI] Audio with key {key} not found.");
                    return 0;
                }

                var state = new SpeakerState
                {
                    Key = key,
                    Position = position,
                    Loop = loop,
                    Volume = volume,
                    MinDistance = minDistance,
                    MaxDistance = maxDistance,
                    IsSpatial = isSpatial,
                    Priority = priority,
                    ConfigureSpeaker = configureSpeaker,
                    Queue = queue,
                    Persistent = persistent,
                    Lifespan = lifespan,
                    AutoCleanup = autoCleanup,
                    PlaybackPosition = 0f
                };

                byte controllerId = 0;
                ControllerIdManager.AllocateId(
                    priority,
                    null, // Temporarily pass null for stopCallback
                    persistent,
                    persistent ? state : null,
                    id =>
                    {
                        controllerId = id;
                        ControllerIdManager.UpdateStopCallback(id, () => FadeOutAudio(id, DEFAULT_FADE_DURATION));
                    },
                    () => Log.Warn($"[AudioManagerAPI] Failed to allocate controller ID for audio {key}.")
                );

                if (controllerId == 0) return 0;

                ISpeaker speaker = speakerFactory.CreateSpeaker(position, controllerId);
                if (speaker == null)
                {
                    ControllerIdManager.ReleaseId(controllerId);
                    Log.Warn($"[AudioManagerAPI] Failed to create speaker for audio {key}.");
                    return 0;
                }

                speaker.Configure(volume, minDistance, maxDistance, isSpatial, configureSpeaker);
                speakers[controllerId] = speaker;

                if (queue)
                {
                    speaker.Queue(samples, loop);
                }
                else
                {
                    speaker.Play(samples, loop);
                    OnPlaybackStarted?.Invoke(controllerId);
                }

                if (autoCleanup)
                {
                    speaker.QueueEmpty += () =>
                    {
                        FadeOutAudio(controllerId, DEFAULT_FADE_DURATION);
                        OnQueueEmpty?.Invoke(controllerId);
                    };
                }

                if (autoCleanup || lifespan.HasValue)
                {
                    speaker.StartAutoStop(controllerId, lifespan ?? float.MaxValue, autoCleanup, id => FadeOutAudio(id, DEFAULT_FADE_DURATION));
                }

                return controllerId;
            }
        }

        public byte PlayGlobalAudio(string key, bool loop, float volume, AudioPriority priority, bool queue = false, float fadeInDuration = 0f, bool persistent = false, float? lifespan = null, bool autoCleanup = false)
        {
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
                FadeInAudio(controllerId, fadeInDuration);
            }

            return controllerId;
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

                byte newId = 0;
                ControllerIdManager.AllocateId(
                    state.Priority,
                    null, // Temporarily pass null for stopCallback
                    true,
                    state,
                    id =>
                    {
                        newId = id;
                        ControllerIdManager.UpdateStopCallback(id, () => FadeOutAudio(id, DEFAULT_FADE_DURATION));
                    },
                    () => Log.Warn($"[AudioManagerAPI] Failed to allocate new ID for recovering speaker {controllerId}.")
                );

                if (newId == 0) return false;

                ISpeaker speaker = speakerFactory.CreateSpeaker(state.Position, newId);
                if (speaker == null)
                {
                    ControllerIdManager.ReleaseId(newId);
                    Log.Warn($"[AudioManagerAPI] Failed to create speaker for recovering ID {controllerId}.");
                    return false;
                }

                speaker.Configure(state.Volume, state.MinDistance, state.MaxDistance, state.IsSpatial, state.ConfigureSpeaker);
                speakers[newId] = speaker;

                float[] samples = audioCache.Get(state.Key);
                if (samples != null)
                {
                    if (resetPlayback) state.PlaybackPosition = 0f;
                    if (state.Queue) speaker.Queue(samples, state.Loop);
                    else speaker.Play(samples, state.Loop, state.PlaybackPosition);
                }

                if (state.AutoCleanup || state.Lifespan.HasValue)
                {
                    speaker.StartAutoStop(newId, state.Lifespan ?? float.MaxValue, state.AutoCleanup, id => FadeOutAudio(id, DEFAULT_FADE_DURATION));
                }

                Log.Debug($"[AudioManagerAPI] Recovered persistent speaker ID {controllerId} as {newId} at {Timing.LocalTime}.");
                return true;
            }
        }

        public void StopAudio(byte controllerId)
        {
            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out var speaker))
                {
                    speaker.Stop();
                    OnStop?.Invoke(controllerId);
                }
            }
        }

        public void PauseAudio(byte controllerId)
        {
            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out var speaker))
                {
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
                if (speakers.TryGetValue(controllerId, out ISpeaker speaker))
                {
                    speaker.Skip(count);
                    OnSkipped?.Invoke(controllerId, count);
                }
            }
        }

        public void FadeInAudio(byte controllerId, float duration)
        {
            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out ISpeaker speaker))
                {
                    speaker.FadeIn(duration);
                }
            }
        }

        public void FadeOutAudio(byte controllerId, float duration)
        {
            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out var speaker))
                {
                    if (speaker is DefaultSpeakerToyAdapter adapter && ControllerIdManager.GetSpeakerState(controllerId) is SpeakerState state && state.Persistent)
                    {
                        state.PlaybackPosition = adapter.GetPlaybackPosition();
                    }
                    speaker.FadeOut(duration, () =>
                    {
                        speakers.Remove(controllerId);
                        ControllerIdManager.ReleaseId(controllerId);
                    });
                }
            }
        }

        public void DestroySpeaker(byte controllerId, bool forceRemoveState = false)
        {
            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out var speaker))
                {
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
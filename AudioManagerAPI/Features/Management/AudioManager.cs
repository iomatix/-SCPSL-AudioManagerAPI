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
    using UnityEngine;

    using Log = LabApi.Features.Console.Logger;

    /// <summary>
    /// Audio orchestration router acting as a thread-safe bridge 
    /// between abstract audio sessions and physical network speaker controller hardware components.
    /// Fully compliant with C# 7.4 execution frameworks.
    /// </summary>
    public partial class AudioManager : IAudioManager
    {
        private readonly Dictionary<byte, ISpeaker> speakers = new Dictionary<byte, ISpeaker>();
        private readonly AudioCache audioCache;
        private readonly ISpeakerFactory speakerFactory;
        private readonly object lockObject = new object();

        #region Events
        public event Action<int> OnPlaybackStarted;
        public event Action<int> OnPaused;
        public event Action<int> OnResumed;
        public event Action<int> OnStop;
        public event Action<int, int> OnSkipped;
        public event Action<int> OnQueueEmpty;
        #endregion

        public AudioOptions Options { get; }

        public AudioManager(ISpeakerFactory speakerFactory, AudioConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            this.speakerFactory = speakerFactory ?? throw new ArgumentNullException(nameof(speakerFactory));

            if (config.CacheSize <= 0)
                throw new ArgumentException("Cache size must be positive.", nameof(config.CacheSize));

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
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (streamProvider == null) throw new ArgumentNullException(nameof(streamProvider));
            audioCache.Register(key, streamProvider);
        }

        public bool IsValidSession(int sessionId)
        {
            lock (lockObject)
            {
                return ControllerIdManager.GetSessionState(sessionId) != null;
            }
        }

        public SpeakerState GetSessionState(int sessionId)
        {
            return ControllerIdManager.GetSessionState(sessionId);
        }

        public int PlayAudio(
            string key,
            Vector3 position,
            bool loop = false,
            float volume = 1f,
            float minDistance = 1f,
            float maxDistance = 20f,
            bool isSpatial = true,
            AudioPriority priority = AudioPriority.Medium,
            Func<Player, bool> validPlayersFilter = null,
            bool queue = false,
            float fadeInDuration = 0f,
            bool persistent = false,
            float? lifespan = null,
            bool autoCleanup = false)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            // OPTIMIZATION: Querying the memory cache buffer OUTSIDE the global management lock lockObject.
            // If a cache-miss triggers a lazy-loading I/O disk fetch, it runs safely in a non-blocking 
            // state, preventing thread exhaustion or frame starvation across the entire audio backend engine.
            float[] samples = audioCache.Get(key);
            if (samples == null)
            {
                Log.Warn($"[AudioManagerAPI] Audio with key {key} not found in cache registries.");
                return 0;
            }

            lock (lockObject)
            {
                var state = new SpeakerState
                {
                    Key = queue ? null : key,
                    Position = position,
                    Loop = queue ? false : loop,
                    Volume = volume,
                    MinDistance = minDistance,
                    MaxDistance = maxDistance,
                    IsSpatial = isSpatial,
                    Priority = priority,
                    PlayerFilter = validPlayersFilter,
                    QueuedClips = queue ? new List<(string key, bool loop)> { (key, loop) } : new List<(string key, bool loop)>(),
                    Persistent = persistent,
                    Lifespan = lifespan,
                    AutoCleanup = autoCleanup,
                    PlaybackPosition = 0f
                };

                int allocatedSessionId = 0;
                Action stopCallback = () =>
                {
                    if (allocatedSessionId != 0) FadeOutAudio(allocatedSessionId, this.Options.DefaultFadeOutDuration);
                };

                if (!ControllerIdManager.TryAllocate(priority, stopCallback, state, out allocatedSessionId, out byte controllerId))
                {
                    Log.Warn($"[AudioManagerAPI] Failed to initialize session for audio {key}.");
                    return 0;
                }

                if (controllerId != 0)
                {
                    InitializePhysicalSpeaker(controllerId, allocatedSessionId, state, samples, loop, queue);
                }

                return allocatedSessionId;
            }
        }

        public int PlayGlobalAudio(
            string key,
            bool loop = false,
            float volume = 1f,
            AudioPriority priority = AudioPriority.Medium,
            Func<Player, bool> validPlayersFilter = null,
            bool queue = false,
            float fadeInDuration = 0f,
            bool persistent = false,
            float? lifespan = null,
            bool autoCleanup = false)
        {
            if (validPlayersFilter == null)
            {
                validPlayersFilter = p => Player.ReadyList.Contains(p);
            }

            int sessionId = PlayAudio(
                key: key,
                position: Vector3.zero,
                loop: loop,
                volume: volume,
                minDistance: 0f,
                maxDistance: 999.99f,
                isSpatial: false,
                priority: priority,
                validPlayersFilter: validPlayersFilter,
                queue: queue,
                fadeInDuration: fadeInDuration,
                persistent: persistent,
                lifespan: lifespan,
                autoCleanup: autoCleanup);

            if (sessionId != 0 && fadeInDuration > 0)
            {
                FadeInAudio(sessionId, fadeInDuration);
            }

            return sessionId;
        }

        private void InitializePhysicalSpeaker(
            byte controllerId,
            int sessionId,
            SpeakerState state,
            float[] initialSamples,
            bool initialLoop,
            bool isQueued)
        {
            Log.Debug($"[AudioManagerAPI] InitializePhysicalSpeaker: session={sessionId}, controllerId={controllerId}, hasSamples={(initialSamples != null)}");

            ISpeaker speaker = speakerFactory.CreateSpeaker(state.Position, controllerId);
            if (speaker == null)
            {
                ControllerIdManager.ReleaseController(controllerId);
                Log.Warn($"[AudioManagerAPI] Failed to create physical speaker for session {sessionId} (Controller ID: {controllerId}).");
                return;
            }

            speaker.Configure(state.Volume, state.MinDistance, state.MaxDistance, state.IsSpatial, null);

            if (speaker is ISpeakerWithPlayerFilter filterSpeaker && state.PlayerFilter != null)
            {
                filterSpeaker.SetValidPlayers(state.PlayerFilter);
            }

            speakers[controllerId] = speaker;
            state.PhysicalSpeaker = speaker;
            state.HasPhysicalSpeaker = true;

            if (initialSamples == null)
            {
                speaker.Play(new float[] { 0f }, false, 0f);
            }
            else if (isQueued)
            {
                speaker.Queue(initialSamples, initialLoop);
            }
            else
            {
                speaker.Play(initialSamples, initialLoop, state.PlaybackPosition);
                OnPlaybackStarted?.Invoke(sessionId);
            }

            if (state.AutoCleanup || state.Lifespan.HasValue)
            {
                float targetLifespan = state.Lifespan ?? float.MaxValue;
                float adaptiveFadeOutDuration = targetLifespan < 0.5f ? 0f : this.Options.DefaultFadeOutDuration;

                speaker.StartAutoStop(
                    controllerId,
                    targetLifespan,
                    state.AutoCleanup,
                    _ => FadeOutAudio(sessionId, adaptiveFadeOutDuration));
            }
        }

        public bool SetSessionVolume(int sessionId, float volume)
        {
            if (volume < 0f || volume > 1f) throw new ArgumentOutOfRangeException(nameof(volume), "Volume must be between 0.0 and 1.0.");

            lock (lockObject)
            {
                var state = GetSessionState(sessionId);
                if (state == null) return false;

                state.Volume = volume;

                if (ControllerIdManager.TryGetActiveController(sessionId, out byte controllerId) && speakers.TryGetValue(controllerId, out var speaker))
                {
                    if (speaker is ISpeakerWithPlayerFilter filterSpeaker)
                    {
                        filterSpeaker.SetVolume(volume);
                        return true;
                    }
                }
                return true;
            }
        }

        public bool SetSessionPosition(int sessionId, Vector3 position)
        {
            lock (lockObject)
            {
                var state = GetSessionState(sessionId);
                if (state == null) return false;

                state.Position = position;

                if (ControllerIdManager.TryGetActiveController(sessionId, out byte controllerId) && speakers.TryGetValue(controllerId, out var speaker))
                {
                    if (speaker is ISpeakerWithPlayerFilter filterSpeaker)
                    {
                        filterSpeaker.SetPosition(position);
                    }
                }
                return true;
            }
        }

        public bool SetSessionPlayerFilter(int sessionId, Func<Player, bool> filter)
        {
            lock (lockObject)
            {
                var state = GetSessionState(sessionId);
                if (state == null) return false;

                state.PlayerFilter = filter;

                if (ControllerIdManager.TryGetActiveController(sessionId, out byte controllerId) && speakers.TryGetValue(controllerId, out var speaker))
                {
                    if (speaker is ISpeakerWithPlayerFilter filterSpeaker)
                    {
                        filterSpeaker.SetValidPlayers(filter);
                    }
                }
                return true;
            }
        }

        public bool RecoverSession(int sessionId, bool resetPlayback = false)
        {
            lock (lockObject)
            {
                var state = GetSessionState(sessionId);
                if (state == null || !state.Persistent) return false;

                if (resetPlayback) state.PlaybackPosition = 0f;

                if (!ControllerIdManager.TryGetActiveController(sessionId, out byte currentControllerId))
                {
                    ControllerIdManager.TryAllocate(state.Priority, null, state, out _, out byte newControllerId);
                    if (newControllerId == 0) return false;

                    var samples = audioCache.Get(state.Key);
                    InitializePhysicalSpeaker(newControllerId, sessionId, state, samples, state.Loop, false);
                }

                return true;
            }
        }

        public void PauseAudio(int sessionId)
        {
            lock (lockObject)
            {
                var state = GetSessionState(sessionId);
                if (state != null && ControllerIdManager.TryGetActiveController(sessionId, out byte controllerId))
                {
                    if (speakers.TryGetValue(controllerId, out var speaker))
                    {
                        speaker.Pause();
                        OnPaused?.Invoke(sessionId);
                    }
                }
            }
        }

        public void ResumeAudio(int sessionId)
        {
            lock (lockObject)
            {
                if (ControllerIdManager.TryGetActiveController(sessionId, out byte controllerId))
                {
                    if (speakers.TryGetValue(controllerId, out var speaker))
                    {
                        speaker.Resume();
                        OnResumed?.Invoke(sessionId);
                    }
                }
            }
        }

        public void SkipAudio(int sessionId, int count)
        {
            lock (lockObject)
            {
                if (ControllerIdManager.TryGetActiveController(sessionId, out byte controllerId))
                {
                    if (speakers.TryGetValue(controllerId, out var speaker))
                    {
                        speaker.Skip(count);
                        OnSkipped?.Invoke(sessionId, count);

                        if (speaker is DefaultSpeakerToyAdapter adapter && adapter.IsQueueEmpty)
                        {
                            OnQueueEmpty?.Invoke(sessionId);
                        }
                    }
                }
            }
        }

        public void FadeInAudio(int sessionId, float duration)
        {
            if (duration < 0f) throw new ArgumentOutOfRangeException(nameof(duration), "Fade-in duration must be non-negative.");

            lock (lockObject)
            {
                if (ControllerIdManager.TryGetActiveController(sessionId, out byte controllerId) && speakers.TryGetValue(controllerId, out var speaker))
                {
                    speaker.FadeIn(duration);
                }
            }
        }

        public void FadeOutAudio(int sessionId, float duration)
        {
            if (duration < 0f) throw new ArgumentOutOfRangeException(nameof(duration), "Fade-out duration must be non-negative.");

            lock (lockObject)
            {
                var state = GetSessionState(sessionId);
                if (state == null) return;

                if (ControllerIdManager.TryGetActiveController(sessionId, out byte controllerId) && speakers.TryGetValue(controllerId, out var speaker))
                {
                    speaker.FadeOut(duration, () =>
                    {
                        MEC.Timing.RunCoroutine(DestroySessionDeferred(sessionId), MEC.Segment.Update);
                    });
                }
                else
                {
                    MEC.Timing.RunCoroutine(DestroySessionDeferred(sessionId), MEC.Segment.Update);
                }
            }
        }

        private IEnumerator<float> DestroySessionDeferred(int sessionId)
        {
            yield return MEC.Timing.WaitForOneFrame;

            lock (lockObject)
            {
                DestroySession(sessionId);
            }
        }

        public void StopAudio(int sessionId)
        {
            lock (lockObject)
            {
                if (ControllerIdManager.TryGetActiveController(sessionId, out byte controllerId) && speakers.TryGetValue(controllerId, out var speaker))
                {
                    speaker.Stop();
                    OnStop?.Invoke(sessionId);
                }
            }
        }

        public (int queuedCount, string currentClip) GetQueueStatus(int sessionId)
        {
            lock (lockObject)
            {
                if (ControllerIdManager.TryGetActiveController(sessionId, out byte controllerId) && speakers.TryGetValue(controllerId, out var speaker))
                {
                    var state = GetSessionState(sessionId);
                    return speaker.GetQueueStatus(state);
                }
                return (0, null);
            }
        }

        public bool ClearSessionQueue(int sessionId)
        {
            lock (lockObject)
            {
                var state = GetSessionState(sessionId);
                if (state != null) state.QueuedClips.Clear();

                if (ControllerIdManager.TryGetActiveController(sessionId, out byte controllerId) && speakers.TryGetValue(controllerId, out var speaker))
                {
                    return speaker.ClearQueue(state);
                }
                return state != null;
            }
        }

        public void DestroySession(int sessionId)
        {
            lock (lockObject)
            {
                if (ControllerIdManager.TryGetActiveController(sessionId, out byte controllerId))
                {
                    if (speakers.TryGetValue(controllerId, out var speaker))
                    {
                        speaker.Stop();
                        speaker.Destroy();
                        speakers.Remove(controllerId);
                    }
                }

                ControllerIdManager.DestroySession(sessionId);
                OnStop?.Invoke(sessionId);
            }
        }

        public void CleanupAllSessions()
        {
            lock (lockObject)
            {
                foreach (var kvp in speakers)
                {
                    kvp.Value.Stop();
                    kvp.Value.Destroy();
                }
                speakers.Clear();
                ControllerIdManager.FullReset();

                Log.Info("[AudioManagerAPI] All audio sessions and physical controllers have been cleaned up.");
            }
        }

        #region Real-Time Audio Streaming
        [Obsolete("Use AppendPcmData(int, float[]) instead.")]
        public void AppendPcmData(int sessionId, short[] pcm)
        {
            if (pcm == null || pcm.Length == 0)
                return;

            var samples = new float[pcm.Length];
            const float inv = 1f / 32768f;

            for (int i = 0; i < pcm.Length; i++)
                samples[i] = pcm[i] * inv;

            AppendPcmData(sessionId, samples);
        }

        public void AppendPcmData(int sessionId, float[] samples)
        {
            var state = ControllerIdManager.GetSessionState(sessionId);
            if (state == null || samples == null || samples.Length == 0)
                return;

            state.PcmQueue.Enqueue(samples);

            if (state.HasPhysicalSpeaker && state.PhysicalSpeaker != null)
                state.PhysicalSpeaker.AppendPcm(samples);
        }

        public int CreateStreamSession(
            Vector3 position,
            bool isSpatial,
            float minDistance,
            float maxDistance,
            float volume,
            AudioPriority priority = AudioPriority.Medium,
            Func<Player, bool> validPlayersFilter = null,
            bool persistent = false,
            float? lifespan = null,
            bool autoCleanup = false)
        {
            lock (lockObject)
            {
                var state = new SpeakerState
                {
                    Key = null,
                    Position = position,
                    Loop = false,
                    Volume = volume,
                    MinDistance = minDistance,
                    MaxDistance = maxDistance,
                    IsSpatial = isSpatial,
                    Priority = priority,
                    PlayerFilter = validPlayersFilter,
                    QueuedClips = new List<(string key, bool loop)>(),
                    Persistent = persistent,
                    Lifespan = lifespan,
                    AutoCleanup = autoCleanup,
                    PlaybackPosition = 0f,
                    IsPaused = false,
                    IsStreamOnly = true
                };

                int allocatedSessionId = 0;
                Action stopCallback = () =>
                {
                    if (allocatedSessionId != 0)
                        FadeOutAudio(allocatedSessionId, this.Options.DefaultFadeOutDuration);
                };

                if (!ControllerIdManager.TryAllocate(priority, stopCallback, state, out allocatedSessionId, out byte controllerId))
                {
                    Log.Warn("[AudioManagerAPI] Failed to initialize stream-only session.");
                    return 0;
                }

                if (controllerId != 0)
                {
                    InitializePhysicalSpeaker(
                        controllerId,
                        allocatedSessionId,
                        state,
                        initialSamples: null,
                        initialLoop: false,
                        isQueued: false);
                }

                return allocatedSessionId;
            }
        }
        #endregion
    }
}
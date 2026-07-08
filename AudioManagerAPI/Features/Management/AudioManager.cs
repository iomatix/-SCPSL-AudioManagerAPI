namespace AudioManagerAPI.Features.Management
{
    using AudioManagerAPI.Cache;
    using AudioManagerAPI.Config;
    using AudioManagerAPI.Controllers;
    using AudioManagerAPI.Defaults;
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Features.Management.Settings;
    using AudioManagerAPI.Features.Speakers;
    using AudioManagerAPI.Speakers.Extensions;
    using AudioManagerAPI.Speakers.State;
    using LabApi.Features.Wrappers;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using UnityEngine;

    using Log = LabApi.Features.Console.Logger;

    /// <summary>
    /// High-performance, deadlock-free audio orchestration router utilizing fine-grained concurrency controls 
    /// and lock-free state operations to secure server frame-rate stability.
    /// </summary>
    public partial class AudioManager : IAudioManager
    {
        // Thread-safe container requiring no outer global locks for CRUD transactions
        private readonly ConcurrentDictionary<byte, ISpeaker> speakers = new ConcurrentDictionary<byte, ISpeaker>();
        private readonly AudioCache audioCache;
        private readonly ISpeakerFactory speakerFactory;

        // Lock object reserved exclusively for localized physical speaker instantiation and crossfades
        private readonly object speakerCreationLock = new object();

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
            // Optimization: ControllerIdManager possesses internal lock safety; outer lock is completely redundant
            return ControllerIdManager.GetSessionState(sessionId) != null;
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

            float[] samples = audioCache.Get(key);
            if (samples == null)
            {
                Log.Warn($" Audio with key {key} not found in cache registries.");
                return 0;
            }

            // Optimization: Structural state instantiation runs lock-free.
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

            // Localized atomic callback assignment
            Action stopCallback = () =>
            {
                if (allocatedSessionId != 0) FadeOutAudio(allocatedSessionId, this.Options.DefaultFadeOutDuration);
            };

            // Deadlock Elimination: Calling TryAllocate detached from an outer lock destroys the A->B->A lock inversion matrix
            if (!ControllerIdManager.TryAllocate(priority, stopCallback, state, out allocatedSessionId, out byte controllerId))
            {
                Log.Warn($" Failed to initialize session for audio {key}.");
                return 0;
            }

            if (controllerId != 0)
            {
                InitializePhysicalSpeaker(controllerId, allocatedSessionId, state, samples, loop, queue);
            }

            return allocatedSessionId;
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
            // Bugfix Krok 3: Replaced slow .Contains O(N^2) evaluation loop with direct lock-free state property flags
            if (validPlayersFilter == null)
            {
                validPlayersFilter = p => p != null && p.IsReady;
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

        public (int worldSessionId, int sourceSessionId) PlaySpatialSmart(string key, Vector3 position, Player sourcePlayer, AudioPriority priority = AudioPriority.Medium, float? lifespan = null, float volume = 1f, float minDistance = 1f, float maxDistance = 20f)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            int worldSession = PlayAudio(
                key: key,
                position: position,
                loop: false,
                volume: volume,
                minDistance: minDistance,
                maxDistance: maxDistance,
                isSpatial: true,
                priority: priority,
                validPlayersFilter: p => p != null && p.IsReady && (sourcePlayer == null || p.UserId != sourcePlayer.UserId),
                queue: false,
                fadeInDuration: 0f,
                persistent: false,
                lifespan: lifespan,
                autoCleanup: true
            );

            int sourceSession = 0;
            if (sourcePlayer != null && sourcePlayer.IsReady)
            {
                sourceSession = PlayAudio(
                    key: key,
                    position: position,
                    loop: false,
                    volume: volume,
                    minDistance: minDistance,
                    maxDistance: maxDistance,
                    isSpatial: true,
                    priority: priority,
                    validPlayersFilter: p => p != null && p.UserId == sourcePlayer.UserId,
                    queue: false,
                    fadeInDuration: 0f,
                    persistent: false,
                    lifespan: lifespan,
                    autoCleanup: true
                );
            }

            return (worldSession, sourceSession);
        }

        public int PlayTrackingAudio(string key, Func<Vector3> positionProvider, Func<bool> validationCheck, AudioPriority priority = AudioPriority.Medium, float? lifespan = null, Func<Player, bool> targetPlayerFilter = null, float volume = 1f, float minDistance = 1f, float maxDistance = 20f)
        {
            if (positionProvider == null || validationCheck == null || !validationCheck()) return 0;

            float initialLifespan = lifespan ?? 0f;

            int sessionId = PlayAudio(
                key: key,
                position: positionProvider(),
                loop: false,
                volume: volume,
                minDistance: minDistance,
                maxDistance: maxDistance,
                isSpatial: true,
                priority: priority,
                validPlayersFilter: targetPlayerFilter,
                queue: false,
                fadeInDuration: 0f,
                persistent: false,
                lifespan: lifespan,
                autoCleanup: true
            );

            if (sessionId == 0) return 0;

            MEC.Timing.RunCoroutine(TrackTargetTransformLoop(positionProvider, validationCheck, sessionId, initialLifespan));
            return sessionId;
        }

        private IEnumerator<float> TrackTargetTransformLoop(Func<Vector3> positionProvider, Func<bool> validationCheck, int sessionId, float duration)
        {
            float elapsed = 0f;
            yield return MEC.Timing.WaitForSeconds(0.1f);

            while (duration <= 0f || elapsed < duration)
            {
                if (!validationCheck() || !IsValidSession(sessionId))
                {
                    float failDuration = Options.DefaultFadeOutDuration > 0f ? Options.DefaultFadeOutDuration : 0.3f;
                    FadeOutAudio(sessionId, failDuration);
                    yield break;
                }

                SetSessionPosition(sessionId, positionProvider());
                elapsed += MEC.Timing.DeltaTime;
                yield return MEC.Timing.WaitForOneFrame;
            }
        }

        public int PlayOrbitingAudio(
            string key,
            Func<Vector3> positionProvider,
            Func<bool> validationCheck,
            float volume,
            float minDistance,
            float maxDistance,
            OrbitSettings orbitSettings,
            AudioPriority priority = AudioPriority.Medium,
            float? lifespan = null,
            Func<Player, bool> targetPlayerFilter = null)
        {
            if (positionProvider == null || validationCheck == null || !validationCheck()) return 0;

            float initialLifespan = lifespan ?? 0f;
            if (initialLifespan <= 0f) return 0;

            int sessionId = PlayAudio(
                key: key,
                position: positionProvider(),
                loop: false,
                volume: volume,
                minDistance: minDistance,
                maxDistance: maxDistance,
                isSpatial: true,
                priority: priority,
                validPlayersFilter: targetPlayerFilter,
                queue: false,
                fadeInDuration: 0f,
                persistent: false,
                lifespan: lifespan,
                autoCleanup: true
            );

            if (sessionId == 0) return 0;

            MEC.Timing.RunCoroutine(TrackAndOrbitPositionLoop(positionProvider, validationCheck, sessionId, initialLifespan, orbitSettings));
            return sessionId;
        }

        private IEnumerator<float> TrackAndOrbitPositionLoop(Func<Vector3> positionProvider, Func<bool> validationCheck, int sessionId, float duration, OrbitSettings settings)
        {
            float elapsed = 0f;
            float currentAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float approachPhaseOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            while (elapsed < duration)
            {
                if (!validationCheck() || !IsValidSession(sessionId))
                {
                    float failDuration = Options.DefaultFadeOutDuration > 0f ? Options.DefaultFadeOutDuration : 0.65f;
                    FadeOutAudio(sessionId, failDuration);
                    yield break;
                }

                Vector3 corePosition = positionProvider();
                float normalizedSine = (Mathf.Sin((elapsed * settings.ApproachSpeed) + approachPhaseOffset) + 1f) / 2f;
                float currentRadius = Mathf.Lerp(settings.MinRadius, settings.MaxRadius, normalizedSine);

                float xOffset = Mathf.Cos(currentAngle) * currentRadius;
                float zOffset = Mathf.Sin(currentAngle) * currentRadius;

                Vector3 projectedVector = new Vector3(
                    corePosition.x + xOffset,
                    corePosition.y + settings.HeightOffset,
                    corePosition.z + zOffset
                );

                SetSessionPosition(sessionId, projectedVector);

                currentAngle += settings.AngularSpeed * MEC.Timing.DeltaTime;
                elapsed += MEC.Timing.DeltaTime;
                yield return MEC.Timing.WaitForOneFrame;
            }
        }

        private void InitializePhysicalSpeaker(
            byte controllerId,
            int sessionId,
            SpeakerState state,
            float[] initialSamples,
            bool initialLoop,
            bool isQueued)
        {
            Log.Debug($" InitializePhysicalSpeaker: session={sessionId}, controllerId={controllerId}");

            ISpeaker speaker = speakerFactory.CreateSpeaker(state.Position, controllerId);
            if (speaker == null)
            {
                ControllerIdManager.ReleaseController(controllerId);
                Log.Warn($" Failed to create physical speaker for session {sessionId} (Controller ID: {controllerId}).");
                return;
            }

            // Localized Locking: Isolate critical dictionary mutations and crossfading behaviors to a specialized sub-lock
            lock (speakerCreationLock)
            {
                if (speakers.TryGetValue(controllerId, out ISpeaker oldSpeaker) && oldSpeaker != null)
                {
                    float crossfadeDuration = this.Options.DefaultFadeOutDuration > 0f ? this.Options.DefaultFadeOutDuration : 0.65f;
                    MEC.Timing.RunCoroutine(FadeOutAndDestroyOldSpeaker(oldSpeaker, crossfadeDuration));
                }

                speakers[controllerId] = speaker;
            }

            speaker.Configure(state.Volume, state.MinDistance, state.MaxDistance, state.IsSpatial, null);

            if (speaker is ISpeakerWithPlayerFilter filterSpeaker && state.PlayerFilter != null)
            {
                filterSpeaker.SetValidPlayers(state.PlayerFilter);
            }

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

            // Bugfix VoIP Recovery: Flush jitter buffer immediately upon hardware binding
            lock (state.PcmQueue)
            {
                while (state.PcmQueue.Count > 0)
                {
                    float[] bufferedSamples = state.PcmQueue.Dequeue();
                    if (bufferedSamples != null && bufferedSamples.Length > 0)
                    {
                        speaker.AppendPcm(bufferedSamples);
                    }
                }
            }

            if (state.AutoCleanup || state.Lifespan.HasValue)
            {
                float targetLifespan = state.Lifespan ?? float.MaxValue;
                if (targetLifespan < 0.5f)
                {
                    MEC.Timing.RunCoroutine(ExecuteTransientNetworkFlush(sessionId, targetLifespan));
                }
                else
                {
                    speaker.StartAutoStop(
                        controllerId,
                        targetLifespan,
                        state.AutoCleanup,
                        _ => FadeOutAudio(sessionId, this.Options.DefaultFadeOutDuration));
                }
            }
        }

        public bool SetSessionVolume(int sessionId, float volume)
        {
            if (volume < 0f || volume > 1f) throw new ArgumentOutOfRangeException(nameof(volume), "Volume must be between 0.0 and 1.0.");

            // Lock-Free Optimization Pattern: Extract state maps using atomic thread-safe concurrent APIs
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

        public bool SetSessionPosition(int sessionId, Vector3 position)
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

        public bool SetSessionPlayerFilter(int sessionId, Func<Player, bool> filter)
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

        public bool RecoverSession(int sessionId, bool resetPlayback = false)
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

        public void PauseAudio(int sessionId)
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

        public void ResumeAudio(int sessionId)
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

        public void SkipAudio(int sessionId, int count)
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

        public void FadeInAudio(int sessionId, float duration)
        {
            if (duration < 0f) throw new ArgumentOutOfRangeException(nameof(duration), "Fade-in duration must be non-negative.");

            if (ControllerIdManager.TryGetActiveController(sessionId, out byte controllerId) && speakers.TryGetValue(controllerId, out var speaker))
            {
                speaker.FadeIn(duration);
            }
        }

        public void FadeOutAudio(int sessionId, float duration)
        {
            if (duration < 0f) throw new ArgumentOutOfRangeException(nameof(duration), "Fade-out duration must be non-negative.");

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

        private IEnumerator<float> DestroySessionDeferred(int sessionId)
        {
            yield return MEC.Timing.WaitForOneFrame;
            DestroySession(sessionId);
        }

        public void StopAudio(int sessionId)
        {
            if (ControllerIdManager.TryGetActiveController(sessionId, out byte controllerId) && speakers.TryGetValue(controllerId, out var speaker))
            {
                speaker.Stop();
                OnStop?.Invoke(sessionId);
            }
        }

        public (int queuedCount, string currentClip) GetQueueStatus(int sessionId)
        {
            if (ControllerIdManager.TryGetActiveController(sessionId, out byte controllerId) && speakers.TryGetValue(controllerId, out var speaker))
            {
                var state = GetSessionState(sessionId);
                return speaker.GetQueueStatus(state);
            }
            return (0, null);
        }

        public bool ClearSessionQueue(int sessionId)
        {
            var state = GetSessionState(sessionId);
            if (state != null) state.QueuedClips.Clear();

            if (ControllerIdManager.TryGetActiveController(sessionId, out byte controllerId) && speakers.TryGetValue(controllerId, out var speaker))
            {
                return speaker.ClearQueue(state);
            }
            return state != null;
        }

        public void DestroySession(int sessionId)
        {
            var state = GetSessionState(sessionId);

            if (ControllerIdManager.TryGetActiveController(sessionId, out byte controllerId))
            {
                if (speakers.TryGetValue(controllerId, out var speaker) && speaker != null)
                {
                    if (state != null && speaker == state.PhysicalSpeaker)
                    {
                        speakers.TryRemove(controllerId, out _);
                        speaker.Stop();
                        speaker.Destroy();
                    }
                }
            }

            ControllerIdManager.DestroySession(sessionId);
            OnStop?.Invoke(sessionId);
        }

        public void CleanupAllSessions()
        {
            // Lock block is retained here exclusively to enforce structural atomicity during intense round-restart flushes
            lock (speakerCreationLock)
            {
                foreach (var kvp in speakers)
                {
                    kvp.Value.Stop();
                    kvp.Value.Destroy();
                }
                speakers.Clear();
                ControllerIdManager.FullReset();

                Log.Info(" All audio sessions and physical controllers have been cleaned up.");
            }
        }

        private IEnumerator<float> ExecuteTransientNetworkFlush(int sessionId, float delayHorizon)
        {
            yield return MEC.Timing.WaitForSeconds(delayHorizon);
            if (!IsValidSession(sessionId)) yield break;

            FadeOutAudio(sessionId, 0f);
            yield return MEC.Timing.WaitForSeconds(0.250f);

            try { DestroySession(sessionId); } catch { }
        }

        public void AppendPcmData(int sessionId, float[] samples)
        {
            var state = ControllerIdManager.GetSessionState(sessionId);
            if (state == null || samples == null || samples.Length == 0)
                return;

            if (!state.IsStreamOnly || !state.HasPhysicalSpeaker)
            {
                lock (state.PcmQueue)
                {
                    state.PcmQueue.Enqueue(samples);
                }
            }

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
                Log.Warn(" Failed to initialize stream-only session.");
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

        private IEnumerator<float> FadeOutAndDestroyOldSpeaker(ISpeaker oldSpeaker, float duration)
        {
            if (oldSpeaker == null) yield break;

            bool isCompleted = false;
            try
            {
                oldSpeaker.FadeOut(duration, () => isCompleted = true);
            }
            catch (Exception ex)
            {
                Log.Error(" Failed to initiate crossfade fadeout: " + ex.Message);
                isCompleted = true;
            }

            float elapsed = 0f;
            float safetyTimeout = duration + 0.2f;

            while (!isCompleted && elapsed < safetyTimeout)
            {
                elapsed += MEC.Timing.DeltaTime;
                yield return MEC.Timing.WaitForOneFrame;
            }

            try
            {
                oldSpeaker.Stop();
                if (oldSpeaker is IDisposable disposable) disposable.Dispose();
                else oldSpeaker.Destroy();
            }
            catch (Exception ex)
            {
                Log.Error(" Error during deferred crossfade resource cleanup: " + ex.Message);
            }
        }
    }
}
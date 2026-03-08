namespace AudioManagerAPI.Speakers.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MEC;
    using AudioManagerAPI.Cache;
    using AudioManagerAPI.Defaults;
    using AudioManagerAPI.Features.Management;
    using AudioManagerAPI.Features.Speakers;
    using AudioManagerAPI.Speakers.State;
    using LabApi.Features.Wrappers;

    using Log = DebugLogger;
    using UnityEngine;

    /// <summary>
    /// Provides extension methods for configuring and managing the lifecycle of physical <see cref="ISpeaker"/> instances.
    /// </summary>
    public static class SpeakerExtensions
    {
        /// <summary>
        /// Configures the specified physical speaker with audio settings, spatialization, and player filtering.
        /// </summary>
        /// <param name="speaker">The speaker instance to configure.</param>
        /// <param name="volume">The playback volume (range: 0.0 to 1.0).</param>
        /// <param name="minDistance">The minimum distance at which the audio begins to fall off.</param>
        /// <param name="maxDistance">The maximum distance beyond which the audio is no longer audible.</param>
        /// <param name="isSpatial">Determines whether 3D spatial audio positioning is applied.</param>
        /// <param name="playerFilter">An optional filter to determine which players can hear the audio.</param>
        /// <returns>The configured <see cref="ISpeaker"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="speaker"/> is null.</exception>
        public static ISpeaker Configure(this ISpeaker speaker, float volume, float minDistance, float maxDistance, bool isSpatial, Func<Player, bool> playerFilter = null)
        {
            if (speaker == null)
                throw new ArgumentNullException(nameof(speaker));
            if (volume < 0f || volume > 1f)
                throw new ArgumentOutOfRangeException(nameof(volume), "Volume must be between 0.0 and 1.0.");
            if (minDistance < 0f)
                throw new ArgumentOutOfRangeException(nameof(minDistance), "Minimum distance must be non-negative.");
            if (maxDistance < minDistance)
                throw new ArgumentOutOfRangeException(nameof(maxDistance), "Maximum distance must be greater than or equal to minimum distance.");

            if (speaker is ISpeakerWithPlayerFilter filterSpeaker)
            {
                filterSpeaker.SetVolume(volume);
                filterSpeaker.SetMinDistance(minDistance);
                filterSpeaker.SetMaxDistance(maxDistance);
                filterSpeaker.SetSpatialization(isSpatial);
                if (playerFilter != null)
                {
                    filterSpeaker.SetValidPlayers(playerFilter);
                }
            }

            return speaker;
        }

        /// <summary>
        /// Updates the playback position in the session state for persistent speakers.
        /// </summary>
        public static bool UpdatePlaybackPosition(this ISpeaker speaker, byte controllerId, SpeakerState state)
        {
            if (speaker == null)
                throw new ArgumentNullException(nameof(speaker));
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (speaker is DefaultSpeakerToyAdapter adapter && state.Persistent)
            {
                state.PlaybackPosition = adapter.GetPlaybackPosition();
                Log.Debug($"[SpeakerExtensions] Updated playback position for physical controller ID {controllerId} to {state.PlaybackPosition} seconds.");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sets the volume for the specified physical speaker.
        /// </summary>
        public static bool SetVolume(this ISpeaker speaker, float volume)
        {
            if (speaker == null)
                throw new ArgumentNullException(nameof(speaker));
            if (volume < 0f || volume > 1f)
                throw new ArgumentOutOfRangeException(nameof(volume), "Volume must be between 0.0 and 1.0.");

            if (speaker is ISpeakerWithPlayerFilter filterSpeaker)
            {
                filterSpeaker.SetVolume(volume);
                return true;
            }

            Log.Warn($"[SpeakerExtensions] Cannot set volume: Speaker is not a {nameof(ISpeakerWithPlayerFilter)}.");
            return false;
        }

        /// <summary>
        /// Sets the 3D world position for the specified physical speaker.
        /// </summary>
        public static bool SetPosition(this ISpeaker speaker, Vector3 position)
        {
            if (speaker == null)
                throw new ArgumentNullException(nameof(speaker));

            if (speaker is ISpeakerWithPlayerFilter filterSpeaker)
            {
                filterSpeaker.SetPosition(position);
                return true;
            }

            Log.Warn($"[SpeakerExtensions] Cannot set position: Speaker is not a {nameof(ISpeakerWithPlayerFilter)}.");
            return false;
        }

        /// <summary>
        /// Restores queued clips from the session state to the speaker's playback queue.
        /// </summary>
        public static int RestoreQueue(this ISpeaker speaker, SpeakerState state, AudioCache audioCache)
        {
            if (speaker == null) throw new ArgumentNullException(nameof(speaker));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (audioCache == null) throw new ArgumentNullException(nameof(audioCache));

            int queuedCount = 0;
            if (state.QueuedClips.Any())
            {
                foreach (var (clipKey, clipLoop) in state.QueuedClips)
                {
                    var samples = audioCache.Get(clipKey);
                    if (samples != null)
                    {
                        speaker.Queue(samples, clipLoop);
                        queuedCount++;
                        Log.Debug($"[SpeakerExtensions] Queued clip {clipKey} (loop: {clipLoop}) for speaker.");
                    }
                    else
                    {
                        Log.Warn($"[SpeakerExtensions] Failed to queue clip {clipKey}: Not found in cache.");
                    }
                }
            }

            return queuedCount;
        }

        /// <summary>
        /// Clears the playback queue for the specified physical speaker.
        /// </summary>
        public static bool ClearQueue(this ISpeaker speaker, SpeakerState state = null)
        {
            if (speaker == null)
                throw new ArgumentNullException(nameof(speaker));

            if (speaker is DefaultSpeakerToyAdapter adapter)
            {
                adapter.ClearQueue();
                if (state?.Persistent == true)
                {
                    state.QueuedClips.Clear();
                    Log.Debug($"[SpeakerExtensions] Cleared abstract queue for persistent speaker state.");
                }
                return true;
            }

            Log.Warn($"[SpeakerExtensions] Cannot clear queue: Speaker is not a {nameof(DefaultSpeakerToyAdapter)}.");
            return false;
        }

        /// <summary>
        /// Retrieves the current queue status for the specified physical speaker.
        /// </summary>
        public static (int queuedCount, string currentClip) GetQueueStatus(this ISpeaker speaker, SpeakerState state = null)
        {
            if (speaker == null)
                throw new ArgumentNullException(nameof(speaker));

            if (speaker is DefaultSpeakerToyAdapter adapter)
            {
                int queuedCount = adapter.QueuedClipsCount;
                string currentClip = state?.QueuedClips.FirstOrDefault().key;
                return (queuedCount, currentClip);
            }

            return (0, null);
        }

        /// <summary>
        /// Validates the abstract session state for consistency and correctness.
        /// </summary>
        public static bool ValidateState(this SpeakerState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            bool isValid = true;

            if (state.Persistent && string.IsNullOrEmpty(state.Key) && !state.QueuedClips.Any())
            {
                Log.Warn("[SpeakerExtensions] Invalid SpeakerState: Persistent session must have a Key or non-empty QueuedClips.");
                isValid = false;
            }

            if (state.Volume < 0f || state.Volume > 1f)
            {
                Log.Warn($"[SpeakerExtensions] Invalid SpeakerState: Volume {state.Volume} must be between 0.0 and 1.0.");
                isValid = false;
            }

            if (state.MinDistance < 0f)
            {
                Log.Warn($"[SpeakerExtensions] Invalid SpeakerState: MinDistance {state.MinDistance} must be non-negative.");
                isValid = false;
            }

            if (state.MaxDistance < state.MinDistance)
            {
                Log.Warn($"[SpeakerExtensions] Invalid SpeakerState: MaxDistance {state.MaxDistance} must be greater than or equal to MinDistance {state.MinDistance}.");
                isValid = false;
            }

            if (state.Lifespan.HasValue && state.Lifespan < 0f)
            {
                Log.Warn($"[SpeakerExtensions] Invalid SpeakerState: Lifespan {state.Lifespan} must be non-negative.");
                isValid = false;
            }

            return isValid;
        }

        /// <summary>
        /// Initiates a coroutine that automatically stops and fades out the physical speaker after a set lifespan.
        /// </summary>
        public static void StartAutoStop(this ISpeaker speaker, byte controllerId, float lifespan, bool autoCleanup, Action<byte> fadeOutAction)
        {
            if (speaker == null)
                throw new ArgumentNullException(nameof(speaker));
            if (fadeOutAction == null)
                throw new ArgumentNullException(nameof(fadeOutAction));
            if (lifespan < 0f)
                throw new ArgumentOutOfRangeException(nameof(lifespan), "Lifespan must be non-negative.");

            if (autoCleanup && lifespan > 0)
            {
                Timing.RunCoroutine(AutoStopCoroutine(controllerId, lifespan, fadeOutAction));
            }
        }

        private static IEnumerator<float> AutoStopCoroutine(byte controllerId, float lifespan, Action<byte> fadeOutAction)
        {
            yield return Timing.WaitForSeconds(lifespan);
            fadeOutAction?.Invoke(controllerId);
        }
    }
}
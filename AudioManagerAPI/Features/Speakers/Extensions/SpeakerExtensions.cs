namespace AudioManagerAPI.Speakers.Extensions
{
    using AudioManagerAPI.Cache;
    using AudioManagerAPI.Defaults;
    using AudioManagerAPI.Features.Speakers;
    using AudioManagerAPI.Speakers.State;
    using LabApi.Features.Wrappers;
    using MEC;
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using Log = AudioManagerAPI.Logger.ApiLogger;

    /// <summary>
    /// Provides extension methods for configuring and managing the lifecycle of physical <see cref="ISpeaker"/> instances.
    /// </summary>
    public static class SpeakerExtensions
    {
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
        /// Restores queued clips from the session state to the speaker's playback queue without LINQ allocation.
        /// </summary>
        public static int RestoreQueue(this ISpeaker speaker, SpeakerState state, AudioCache audioCache)
        {
            if (speaker == null) throw new ArgumentNullException(nameof(speaker));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (audioCache == null) throw new ArgumentNullException(nameof(audioCache));

            int queuedCount = 0;
            int listCount = state.QueuedClips.Count;

            // Optimization: Replaced .Any() with a direct Count properties verification
            if (listCount > 0)
            {
                // Optimization: Replaced foreach loop with index-based for loop to avoid structure-enumerator copy operations
                for (int i = 0; i < listCount; i++)
                {
                    var clip = state.QueuedClips[i];
                    var samples = audioCache.Get(clip.key);

                    if (samples != null)
                    {
                        speaker.Queue(samples, clip.loop);
                        queuedCount++;
                        Log.Debug($"[SpeakerExtensions] Queued clip {clip.key} (loop: {clip.loop}) for speaker.");
                    }
                    else
                    {
                        Log.Warn($"[SpeakerExtensions] Failed to queue clip {clip.key}: Not found in cache.");
                    }
                }
            }

            return queuedCount;
        }

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
        /// Retrieves the current queue status safely with zero allocation.
        /// </summary>
        public static (int queuedCount, string currentClip) GetQueueStatus(this ISpeaker speaker, SpeakerState state = null)
        {
            if (speaker == null)
                throw new ArgumentNullException(nameof(speaker));

            if (speaker is DefaultSpeakerToyAdapter adapter)
            {
                int queuedCount = adapter.QueuedClipsCount;

                // Optimization: Replaced .FirstOrDefault() LINQ chain with a safe, direct indexer access
                string currentClip = (state != null && state.QueuedClips.Count > 0)
                    ? state.QueuedClips[0].key
                    : null;

                return (queuedCount, currentClip);
            }

            return (0, null);
        }

        public static bool ValidateState(this SpeakerState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            bool isValid = true;

            // Optimization: Replaced .Any() with direct Count verification
            if (state.Persistent && string.IsNullOrEmpty(state.Key) && state.QueuedClips.Count == 0)
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
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
    /// Provides extension methods for configuring and managing the lifecycle of <see cref="ISpeaker"/> instances.
    /// </summary>
    public static class SpeakerExtensions
    {
        /// <summary>
        /// Configures the specified speaker with audio settings, spatialization, player filtering, and optional custom configuration.
        /// </summary>
        /// <param name="speaker">The speaker instance to configure.</param>
        /// <param name="volume">The playback volume (range: 0.0 to 1.0).</param>
        /// <param name="minDistance">The minimum distance at which the audio begins to fall off.</param>
        /// <param name="maxDistance">The maximum distance beyond which the audio is no longer audible.</param>
        /// <param name="isSpatial">Determines whether 3D spatial audio positioning is applied.</param>
        /// <param name="configureSpeaker">An optional delegate to apply additional speaker-specific configuration.</param>
        /// <param name="playerFilter">An optional filter to determine which players can hear the audio.</param>
        /// <returns>The configured <see cref="ISpeaker"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="speaker"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="volume"/>, <paramref name="minDistance"/>, or <paramref name="maxDistance"/> is invalid.
        /// </exception>
        /// <remarks>
        /// This method configures spatial audio settings and player filtering for speakers implementing
        /// <see cref="ISpeakerWithPlayerFilter"/>. It is used during speaker initialization or recovery
        /// (e.g., via <see cref="IAudioManager.RecoverSpeaker"/>) to ensure consistent audio behavior.
        /// The <paramref name="playerFilter"/> is typically set to limit playback to specific players,
        /// such as those in <see cref="Player.ReadyList"/> in SCP:SL. Ensure thread-safety by calling
        /// within a lock, as used in <see cref="AudioManagerAPI.Features.Management.AudioManager"/>.
        /// </remarks>
        public static ISpeaker Configure(this ISpeaker speaker, float volume, float minDistance, float maxDistance, bool isSpatial, Action<ISpeaker> configureSpeaker = null, Func<Player, bool> playerFilter = null)
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

            configureSpeaker?.Invoke(speaker);
            return speaker;
        }

        /// <summary>
        /// Updates the playback position in the speaker's state for persistent speakers.
        /// </summary>
        /// <param name="speaker">The speaker instance to update.</param>
        /// <param name="controllerId">The controller ID associated with the speaker.</param>
        /// <param name="state">The speaker state to update with the current playback position.</param>
        /// <returns>True if the playback position was updated, false if the speaker is not persistent or not a <see cref="DefaultSpeakerToyAdapter"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="speaker"/> or <paramref name="state"/> is null.
        /// </exception>
        /// <remarks>
        /// This method retrieves the current playback position from the speaker (via
        /// <see cref="DefaultSpeakerToyAdapter.GetPlaybackPosition"/>) and updates
        /// <see cref="SpeakerState.PlaybackPosition"/> for persistent speakers. It is called
        /// before operations like pause, stop, or fade-out to ensure accurate resumption during
        /// recovery (e.g., via <see cref="IAudioManager.RecoverSpeaker"/>). Use within a lock
        /// to ensure thread-safety, as in <see cref="AudioManagerAPI.Features.Management.AudioManager"/>.
        /// </remarks>
        public static bool UpdatePlaybackPosition(this ISpeaker speaker, byte controllerId, SpeakerState state)
        {
            if (speaker == null)
                throw new ArgumentNullException(nameof(speaker));
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (speaker is DefaultSpeakerToyAdapter adapter && state.Persistent)
            {
                state.PlaybackPosition = adapter.GetPlaybackPosition();
                Log.Debug($"[SpeakerExtensions] Updated playback position for controller ID {controllerId} to {state.PlaybackPosition} seconds.");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sets the volume for the specified speaker.
        /// </summary>
        /// <param name="speaker">The speaker instance to update.</param>
        /// <param name="volume">The new volume (0.0 to 1.0).</param>
        /// <returns>True if the volume was set, false if the speaker does not support volume adjustment.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="speaker"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="volume"/> is not between 0.0 and 1.0.</exception>
        /// <remarks>
        /// This method updates the volume for speakers implementing <see cref="ISpeakerWithPlayerFilter"/>.
        /// Useful for dynamic audio adjustments in SCP:SL, such as fading ambient sounds.
        /// Ensure thread-safety by calling within a lock.
        /// </remarks>
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
        /// Sets the 3D world position for the specified speaker.
        /// </summary>
        /// <param name="speaker">The speaker instance to update.</param>
        /// <param name="position">The new position in world coordinates.</param>
        /// <returns>
        /// <c>true</c> if the position was successfully set; 
        /// <c>false</c> if the speaker does not support position adjustment.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="speaker"/> is <c>null</c>.</exception>
        /// <remarks>
        /// This method updates the position for speakers implementing <see cref="ISpeakerWithPlayerFilter"/>.
        /// Useful for dynamically moving audio sources in SCP:SL, such as ambient emitters following entities.
        /// Ensure thread-safety by calling within a lock.
        /// </remarks>
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
        /// Restores queued clips from the speaker state to the speaker's playback queue.
        /// </summary>
        /// <param name="speaker">The speaker instance to restore clips to.</param>
        /// <param name="state">The speaker state containing the queued clips.</param>
        /// <param name="audioCache">The audio cache to retrieve clip samples.</param>
        /// <returns>The number of clips successfully queued.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="speaker"/>, <paramref name="state"/>, or <paramref name="audioCache"/> is null.
        /// </exception>
        /// <remarks>
        /// This method iterates through <see cref="SpeakerState.QueuedClips"/> and queues each clip
        /// using <see cref="ISpeaker.Queue"/>. It is used during speaker recovery to restore the
        /// playback queue for persistent speakers (e.g., via <see cref="IAudioManager.RecoverSpeaker"/>).
        /// Invalid or missing clips are skipped with a warning. Ensure thread-safety by calling
        /// within a lock, as in <see cref="AudioManagerAPI.Features.Management.AudioManager"/>.
        /// </remarks>
        public static int RestoreQueue(this ISpeaker speaker, SpeakerState state, AudioCache audioCache)
        {
            if (speaker == null)
                throw new ArgumentNullException(nameof(speaker));
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (audioCache == null)
                throw new ArgumentNullException(nameof(audioCache));

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
        /// Clears the playback queue for the specified speaker.
        /// </summary>
        /// <param name="speaker">The speaker instance to clear the queue for.</param>
        /// <param name="state">The speaker state to update, if persistent.</param>
        /// <returns>True if the queue was cleared, false if the speaker does not support queue clearing.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="speaker"/> is null.</exception>
        /// <remarks>
        /// This method clears the playback queue for speakers implementing <see cref="DefaultSpeakerToyAdapter"/>.
        /// If the speaker is persistent, the <see cref="SpeakerState.QueuedClips"/> list is also cleared.
        /// This is useful for resetting a speaker's queue without stopping current playback, such as when
        /// dynamically updating audio sequences in SCP:SL. Ensure thread-safety by calling within a lock,
        /// as in <see cref="AudioManagerAPI.Features.Management.AudioManager.ClearSpeakerQueue"/>.
        /// </remarks>
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
                    Log.Debug($"[SpeakerExtensions] Cleared queue for persistent speaker.");
                }
                return true;
            }

            Log.Warn($"[SpeakerExtensions] Cannot clear queue: Speaker is not a {nameof(DefaultSpeakerToyAdapter)}.");
            return false;
        }

        /// <summary>
        /// Retrieves the current queue status for the specified speaker.
        /// </summary>
        /// <param name="speaker">The speaker instance to query.</param>
        /// <param name="state">The speaker state, if persistent.</param>
        /// <returns>A tuple containing the number of queued clips and the current clip key, or (0, null) if not supported or empty.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="speaker"/> is null.</exception>
        /// <remarks>
        /// This method retrieves the number of queued clips and the current clip key for speakers implementing
        /// <see cref="DefaultSpeakerToyAdapter"/>. For persistent speakers, it uses <see cref="SpeakerState.QueuedClips"/>
        /// if available. Useful for debugging or UI updates in SCP:SL, such as displaying the current audio queue.
        /// Ensure thread-safety by calling within a lock, as in <see cref="AudioManagerAPI.Features.Management.AudioManager"/>.
        /// </remarks>
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
        /// Validates the speaker state for consistency and correctness.
        /// </summary>
        /// <param name="state">The speaker state to validate.</param>
        /// <returns>True if the state is valid, false otherwise with warnings logged.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is null.</exception>
        /// <remarks>
        /// This method checks <see cref="SpeakerState"/> for consistency, ensuring that either
        /// <see cref="SpeakerState.Key"/> is non-null or <see cref="SpeakerState.QueuedClips"/> is non-empty
        /// for persistent speakers. It also validates audio parameters like volume and distances.
        /// Useful for debugging or before recovery in <see cref="IAudioManager.RecoverSpeaker"/>.
        /// </remarks>
        public static bool ValidateState(this SpeakerState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            bool isValid = true;

            if (state.Persistent && string.IsNullOrEmpty(state.Key) && !state.QueuedClips.Any())
            {
                Log.Warn("[SpeakerExtensions] Invalid SpeakerState: Persistent speaker must have a Key or non-empty QueuedClips.");
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
        /// Initiates a coroutine that automatically stops and fades out the speaker after a set lifespan, if enabled.
        /// </summary>
        /// <param name="speaker">The speaker instance to manage.</param>
        /// <param name="controllerId">The controller ID associated with the speaker.</param>
        /// <param name="lifespan">The duration (in seconds) the speaker should remain active before auto-stopping.</param>
        /// <param name="autoCleanup">Indicates whether the speaker should fade out and stop after the lifespan expires.</param>
        /// <param name="fadeOutAction">The delegate to invoke for fade-out logic, using the controller ID.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="speaker"/> or <paramref name="fadeOutAction"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="lifespan"/> is negative.</exception>
        /// <remarks>
        /// This method starts a coroutine to monitor the speaker's lifespan and trigger fade-out
        /// if <paramref name="autoCleanup"/> is true. It uses MEC's coroutine system for timing,
        /// suitable for SCP:SL's game loop. Ensure the calling context is thread-safe, as this method
        /// does not acquire locks internally.
        /// </remarks>
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

        /// <summary>
        /// Internal coroutine responsible for timing speaker lifespan and executing fade-out logic on completion.
        /// </summary>
        /// <param name="controllerId">The controller ID to apply fade-out to.</param>
        /// <param name="lifespan">The delay duration (in seconds) before fade-out is triggered.</param>
        /// <param name="fadeOutAction">The action that performs fade-out for the target speaker.</param>
        /// <returns>A coroutine yield instruction for MEC scheduling.</returns>
        /// <remarks>
        /// This coroutine waits for the specified <paramref name="lifespan"/> using MEC's
        /// <see cref="Timing.WaitForSeconds"/> and then invokes the <paramref name="fadeOutAction"/>.
        /// It is designed to be lightweight and non-blocking, suitable for use in game environments like SCP:SL.
        /// </remarks>
        private static IEnumerator<float> AutoStopCoroutine(byte controllerId, float lifespan, Action<byte> fadeOutAction)
        {
            yield return Timing.WaitForSeconds(lifespan);
            fadeOutAction?.Invoke(controllerId);
        }
    }
}
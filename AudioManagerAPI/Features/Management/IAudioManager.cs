namespace AudioManagerAPI.Features.Management
{
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Features.Speakers;
    using AudioManagerAPI.Speakers.State;
    using LabApi.Features.Wrappers;
    using System;
    using System.IO;
    using UnityEngine;

    /// <summary>
    /// Defines the contract for managing audio playback and speaker lifecycle.
    /// </summary>
    public interface IAudioManager
    {
        #region Actions
        /// <summary>
        /// Invoked when a speaker begins playback.
        /// </summary>
        /// <remarks>
        /// Useful for tracking active audio channels or triggering visual indicators.
        /// </remarks>
        event Action<byte> OnPlaybackStarted;

        /// <summary>
        /// Raised when playback is paused for the given controller ID.
        /// </summary>
        /// <remarks>
        /// Can be used to signal UI changes, manage state persistence, or log user interactions.
        /// </remarks>
        event Action<byte> OnPaused;

        /// <summary>
        /// Raised when previously paused audio resumes playback.
        /// </summary>
        /// <remarks>
        /// Great for syncing animations, re-engaging listeners, or updating UI elements.
        /// </remarks>
        event Action<byte> OnResumed;

        /// <summary>
        /// Raised when audio playback is explicitly stopped for the given controller ID.
        /// </summary>
        /// <remarks>
        /// Useful for tracking manual termination of audio clips, distinguishing from natural queue completion.
        /// </remarks>
        event Action<byte> OnStop;

        /// <summary>
        /// Raised when audio skip logic is invoked for the specified controller ID.
        /// </summary>
        /// <remarks>
        /// Useful for signaling transitions between clips, syncing UI indicators, or initiating custom queue logic.
        /// </remarks>
        event Action<byte, int> OnSkipped;

        /// <summary>
        /// Triggered when the audio queue for a speaker becomes empty.
        /// </summary>
        /// <remarks>
        /// Ideal for cleanup logic, auto-removal of inactive speakers, or triggering chained actions.
        /// </remarks>
        event Action<byte> OnQueueEmpty;
        #endregion

        #region Methods
        /// <summary>
        /// Registers an audio clip with the specified key and stream provider.
        /// </summary>
        /// <param name="key">The unique identifier for the audio clip.</param>
        /// <param name="streamProvider">The function providing the audio stream (48kHz, Mono, 16-bit PCM WAV).</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> or <paramref name="streamProvider"/> is null.</exception>
        void RegisterAudio(string key, Func<Stream> streamProvider);

        /// <summary>
        /// Retrieves the speaker associated with the specified controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker.</param>
        /// <returns>The <see cref="ISpeaker"/> instance, or null if not found.</returns>
        ISpeaker GetSpeaker(byte controllerId);

        /// <summary>
        /// Checks if the specified controller ID is valid and associated with an active speaker.
        /// </summary>
        /// <param name="controllerId">The controller ID to check.</param>
        /// <returns>True if the controller ID is valid and has an active speaker, false otherwise.</returns>
        bool IsValidController(byte controllerId);

        /// <summary>
        /// Plays an audio clip at the specified position with the given settings.
        /// </summary>
        /// <param name="key">The unique identifier of the audio clip.</param>
        /// <param name="position">The 3D world-space position for the speaker.</param>
        /// <param name="loop">Whether the audio should loop.</param>
        /// <param name="volume">The playback volume (0.0 to 1.0).</param>
        /// <param name="minDistance">The minimum distance for volume falloff.</param>
        /// <param name="maxDistance">The maximum distance for audibility.</param>
        /// <param name="isSpatial">Whether to use 3D spatial audio.</param>
        /// <param name="priority">The priority for controller ID allocation.</param>
        /// <param name="configureSpeaker">Optional delegate for custom speaker configuration.</param>
        /// <param name="queue">Whether to queue the audio behind the current playback.</param>
        /// <param name="persistent">Whether the speaker state should be persistent for recovery.</param>
        /// <param name="lifespan">Optional lifespan (in seconds) before auto-stopping.</param>
        /// <param name="autoCleanup">Whether to fade out and stop after the lifespan.</param>
        /// <returns>The controller ID assigned to the speaker, or 0 if playback failed.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="volume"/> is not between 0.0 and 1.0, <paramref name="minDistance"/> is negative,
        /// or <paramref name="maxDistance"/> is less than <paramref name="minDistance"/>.
        /// </exception>
        /// <remarks>
        /// This method allocates a controller ID, creates a speaker, and configures it with the specified settings.
        /// If <paramref name="persistent"/> is true, the speaker state is stored for recovery via <see cref="RecoverSpeaker"/>.
        /// The method is thread-safe and uses a lock to ensure consistent state management.
        /// </remarks>
        byte PlayAudio(string key, Vector3 position, bool loop, float volume, float minDistance, float maxDistance, bool isSpatial, AudioPriority priority, Action<ISpeaker> configureSpeaker = null, bool queue = false, bool persistent = false, float? lifespan = null, bool autoCleanup = false);

        /// <summary>
        /// Plays a global audio clip audible to all players with the given settings.
        /// </summary>
        /// <param name="key">The unique identifier of the audio clip.</param>
        /// <param name="loop">Whether the audio should loop.</param>
        /// <param name="volume">The playback volume (0.0 to 1.0).</param>
        /// <param name="priority">The priority for controller ID allocation.</param>
        /// <param name="queue">Whether to queue the audio behind the current playback.</param>
        /// <param name="fadeInDuration">The duration (in seconds) for fading in the audio.</param>
        /// <param name="persistent">Whether the speaker state should be persistent for recovery.</param>
        /// <param name="lifespan">Optional lifespan (in seconds) before auto-stopping.</param>
        /// <param name="autoCleanup">Whether to fade out and stop after the lifespan.</param>
        /// <returns>The controller ID assigned to the speaker, or 0 if playback failed.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="volume"/> is not between 0.0 and 1.0 or <paramref name="fadeInDuration"/> is negative.
        /// </exception>
        /// <remarks>
        /// This method plays audio globally (non-spatial, max distance 999.99f) with a default
        /// player filter limiting playback to <see cref="Player.ReadyList"/>. It supports fade-in
        /// and persistent state for recovery.
        /// </remarks>
        byte PlayGlobalAudio(string key, bool loop, float volume, AudioPriority priority, bool queue = false, float fadeInDuration = 0f, bool persistent = false, float? lifespan = null, bool autoCleanup = false);

        /// <summary>
        /// Sets the volume for the specified speaker dynamically.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker.</param>
        /// <param name="volume">The new volume (0.0 to 1.0).</param>
        /// <returns>True if the volume was set, false if the speaker is invalid.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="volume"/> is not between 0.0 and 1.0.</exception>
        /// <remarks>
        /// This method updates the volume of an active speaker and its persistent state.
        /// Useful for dynamic audio adjustments in SCP:SL, such as fading ambient sounds.
        /// Thread-safe with internal locking.
        /// </remarks>
        bool SetSpeakerVolume(byte controllerId, float volume);

        /// <summary>
        /// Sets the 3D world position for the specified speaker.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker.</param>
        /// <param name="position">The new position in world coordinates.</param>
        /// <returns>
        /// <c>true</c> if the position was successfully updated; <c>false</c> if the speaker was not found.
        /// </returns>
        /// <remarks>
        /// This method is useful for repositioning audio sources dynamically in SCP:SL,
        /// such as moving ambient sound emitters or following an entity.
        /// Thread-safe with internal locking.
        /// </remarks>
        bool SetSpeakerPosition(byte controllerId, Vector3 position);

        /// <summary>
        /// Recovers a persistent speaker using its stored state.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to recover.</param>
        /// <param name="resetPlayback">Whether to reset the playback position to 0.</param>
        /// <returns>True if the speaker was successfully recovered, false otherwise.</returns>
        bool RecoverSpeaker(byte controllerId, bool resetPlayback = false);

        /// <summary>
        /// Pauses playback for the specified speaker.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker.</param>
        void PauseAudio(byte controllerId);

        /// <summary>
        /// Resumes playback for the specified speaker.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker.</param>
        void ResumeAudio(byte controllerId);

        /// <summary>
        /// Skips the specified number of queued clips for the speaker.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker.</param>
        /// <param name="count">The number of clips to skip.</param>
        void SkipAudio(byte controllerId, int count);

        /// <summary>
        /// Stops playback for the specified speaker immediately.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker.</param>
        void StopAudio(byte controllerId);

        /// <summary>
        /// Fades in the audio for the specified speaker over the given duration.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker.</param>
        /// <param name="duration">The fade-in duration (in seconds).</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="duration"/> is negative.</exception>
        void FadeInAudio(byte controllerId, float duration);

        /// <summary>
        /// Fades out the audio for the specified speaker and stops it.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker.</param>
        /// <param name="duration">The fade-out duration (in seconds).</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="duration"/> is negative.</exception>
        void FadeOutAudio(byte controllerId, float duration);

        /// <summary>
        /// Retrieves the current queue status for the specified speaker.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker.</param>
        /// <returns>A tuple containing the number of queued clips and the current clip key, or (0, null) if the speaker is invalid or does not support queuing.</returns>
        /// <remarks>
        /// This method retrieves the number of queued clips and the current clip key for the specified speaker.
        /// Useful for debugging or updating UI elements in SCP:SL, such as displaying the current audio queue.
        /// Thread-safe with internal locking.
        /// </remarks>
        (int queuedCount, string currentClip) GetQueueStatus(byte controllerId);

        /// <summary>
        /// Clears the playback queue for the specified speaker.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker.</param>
        /// <returns>True if the queue was cleared, false if the speaker is invalid or does not support queue clearing.</returns>
        /// <remarks>
        /// This method clears all queued clips for the specified speaker without stopping current playback.
        /// For persistent speakers, the <see cref="SpeakerState.QueuedClips"/> list is also cleared.
        /// Useful for dynamically updating audio sequences in SCP:SL, such as when changing ambient sounds
        /// or canceling queued announcements. Thread-safe with internal locking.
        /// </remarks>
        bool ClearSpeakerQueue(byte controllerId);

        /// <summary>
        /// Destroys the specified speaker and releases its controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker.</param>
        /// <param name="forceRemoveState">Whether to force-remove the speaker's persistent state.</param>
        void DestroySpeaker(byte controllerId, bool forceRemoveState = false);

        /// <summary>
        /// Cleans up all active speakers and releases their controller IDs.
        /// </summary>
        void CleanupAllSpeakers();
        #endregion
    }
}
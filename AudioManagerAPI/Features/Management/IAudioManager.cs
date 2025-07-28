namespace AudioManagerAPI.Features.Management
{
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Features.Speakers;
    using System;
    using System.IO;
    using UnityEngine;

    /// <summary>
    /// Defines the contract for managing audio playback and speaker lifecycle in SCP: Secret Laboratory.
    /// </summary>
    public interface IAudioManager
    {
        #region Events
        /// <summary>
        /// Occurs when a speaker begins audio playback.
        /// </summary>
        event Action<byte> OnPlaybackStarted;

        /// <summary>
        /// Occurs when audio playback is paused for a speaker.
        /// </summary>
        event Action<byte> OnPaused;

        /// <summary>
        /// Occurs when paused audio playback resumes for a speaker.
        /// </summary>
        event Action<byte> OnResumed;

        /// <summary>
        /// Occurs when audio playback is stopped for a speaker.
        /// </summary>
        event Action<byte> OnStop;

        /// <summary>
        /// Occurs when audio clips are skipped for a speaker.
        /// </summary>
        event Action<byte, int> OnSkipped;

        /// <summary>
        /// Occurs when the audio queue for a speaker becomes empty.
        /// </summary>
        event Action<byte> OnQueueEmpty;
        #endregion

        #region Methods
        /// <summary>
        /// Registers an audio clip for playback with a specified key and stream provider.
        /// </summary>
        /// <param name="key">The unique identifier for the audio clip.</param>
        /// <param name="streamProvider">The function providing the audio stream (48kHz, Mono, 16-bit PCM WAV).</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> or <paramref name="streamProvider"/> is null.</exception>
        void RegisterAudio(string key, Func<Stream> streamProvider);

        // TODO
        AudioOptions Options { get; }
        
        /// <summary>
        /// Retrieves the speaker associated with the specified controller ID.
        /// </summary>
        /// <param name="controllerId">The unique identifier of the speaker.</param>
        /// <returns>The <see cref="ISpeaker"/> instance, or null if not found.</returns>
        ISpeaker GetSpeaker(byte controllerId);

        /// <summary>
        /// Checks if the specified controller ID is valid and associated with an active speaker.
        /// </summary>
        /// <param name="controllerId">The unique identifier to check.</param>
        /// <returns>True if the controller ID is valid and has an active speaker; otherwise, false.</returns>
        bool IsValidController(byte controllerId);

        /// <summary>
        /// Plays spatial audio at a specified 3D position with distance-based attenuation.
        /// </summary>
        /// <param name="key">The unique identifier of the audio clip.</param>
        /// <param name="position">The 3D world position where the audio is played.</param>
        /// <param name="loop">Whether the audio should loop.</param>
        /// <param name="volume">The playback volume (0.0 to 1.0).</param>
        /// <param name="minDistance">The minimum distance for volume falloff.</param>
        /// <param name="maxDistance">The maximum distance for audibility.</param>
        /// <param name="isSpatial">Whether to use 3D spatial audio.</param>
        /// <param name="priority">The priority for controller ID allocation.</param>
        /// <param name="configureSpeaker">Optional delegate to configure the speaker, such as setting player filters.</param>
        /// <param name="queue">If true, queues the audio instead of playing immediately.</param>
        /// <param name="persistent">If true, persists the speaker state for recovery.</param>
        /// <param name="lifespan">Optional duration in seconds before auto-stopping the audio.</param>
        /// <param name="autoCleanup">If true, automatically cleans up the speaker after playback.</param>
        /// <returns>The controller ID of the speaker, or 0 if playback fails.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="volume"/> is not between 0.0 and 1.0, <paramref name="minDistance"/> is negative,
        /// or <paramref name="maxDistance"/> is less than <paramref name="minDistance"/>.
        /// </exception>
        byte PlayAudio(string key, Vector3 position, bool loop, float volume, float minDistance, float maxDistance, bool isSpatial, AudioPriority priority, Action<ISpeaker> configureSpeaker = null, bool queue = false, bool persistent = false, float? lifespan = null, bool autoCleanup = false);

        /// <summary>
        /// Plays non-spatial audio globally, audible to all ready players.
        /// </summary>
        /// <param name="key">The unique identifier of the audio clip.</param>
        /// <param name="loop">Whether the audio should loop.</param>
        /// <param name="volume">The playback volume (0.0 to 1.0).</param>
        /// <param name="priority">The priority for controller ID allocation.</param>
        /// <param name="queue">If true, queues the audio instead of playing immediately.</param>
        /// <param name="fadeInDuration">The duration in seconds for fading in the audio.</param>
        /// <param name="persistent">If true, persists the speaker state for recovery.</param>
        /// <param name="lifespan">Optional duration in seconds before auto-stopping the audio.</param>
        /// <param name="autoCleanup">If true, automatically cleans up the speaker after playback.</param>
        /// <returns>The controller ID of the speaker, or 0 if playback fails.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="volume"/> is not between 0.0 and 1.0 or <paramref name="fadeInDuration"/> is negative.
        /// </exception>
        byte PlayGlobalAudio(string key, bool loop, float volume, AudioPriority priority, bool queue = false, float fadeInDuration = 0f, bool persistent = false, float? lifespan = null, bool autoCleanup = false);

        /// <summary>
        /// Plays non-spatial audio globally with custom speaker configuration, such as player filters.
        /// </summary>
        /// <param name="key">The unique identifier of the audio clip.</param>
        /// <param name="loop">Whether the audio should loop.</param>
        /// <param name="volume">The playback volume (0.0 to 1.0).</param>
        /// <param name="priority">The priority for controller ID allocation.</param>
        /// <param name="configureSpeaker">Delegate to configure the speaker, such as setting player filters.</param>
        /// <param name="queue">If true, queues the audio instead of playing immediately.</param>
        /// <param name="fadeInDuration">The duration in seconds for fading in the audio.</param>
        /// <param name="persistent">If true, persists the speaker state for recovery.</param>
        /// <param name="lifespan">Optional duration in seconds before auto-stopping the audio.</param>
        /// <param name="autoCleanup">If true, automatically cleans up the speaker after playback.</param>
        /// <returns>The controller ID of the speaker, or 0 if playback fails.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="volume"/> is not between 0.0 and 1.0 or <paramref name="fadeInDuration"/> is negative.
        /// </exception>
        byte PlayGlobalAudioWithFilter(string key, bool loop, float volume, AudioPriority priority, Action<ISpeaker> configureSpeaker, bool queue = false, float fadeInDuration = 0f, bool persistent = false, float? lifespan = null, bool autoCleanup = false);

        /// <summary>
        /// Sets the volume for the specified speaker.
        /// </summary>
        /// <param name="controllerId">The unique identifier of the speaker.</param>
        /// <param name="volume">The new volume (0.0 to 1.0).</param>
        /// <returns>True if the volume was set; otherwise, false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="volume"/> is not between 0.0 and 1.0.</exception>
        bool SetSpeakerVolume(byte controllerId, float volume);

        /// <summary>
        /// Sets the 3D world position for the specified speaker.
        /// </summary>
        /// <param name="controllerId">The unique identifier of the speaker.</param>
        /// <param name="position">The new position in world coordinates.</param>
        /// <returns>True if the position was set; otherwise, false.</returns>
        bool SetSpeakerPosition(byte controllerId, Vector3 position);

        /// <summary>
        /// Recovers a persistent speaker using its stored state.
        /// </summary>
        /// <param name="controllerId">The unique identifier of the speaker to recover.</param>
        /// <param name="resetPlayback">If true, resets the playback position to 0.</param>
        /// <returns>True if the speaker was recovered; otherwise, false.</returns>
        bool RecoverSpeaker(byte controllerId, bool resetPlayback = false);

        /// <summary>
        /// Pauses playback for the specified speaker.
        /// </summary>
        /// <param name="controllerId">The unique identifier of the speaker.</param>
        void PauseAudio(byte controllerId);

        /// <summary>
        /// Resumes playback for the specified speaker.
        /// </summary>
        /// <param name="controllerId">The unique identifier of the speaker.</param>
        void ResumeAudio(byte controllerId);

        /// <summary>
        /// Skips the specified number of queued clips for the speaker.
        /// </summary>
        /// <param name="controllerId">The unique identifier of the speaker.</param>
        /// <param name="count">The number of clips to skip.</param>
        void SkipAudio(byte controllerId, int count);

        /// <summary>
        /// Stops playback for the specified speaker immediately.
        /// </summary>
        /// <param name="controllerId">The unique identifier of the speaker.</param>
        void StopAudio(byte controllerId);

        /// <summary>
        /// Fades in the audio for the specified speaker over a duration.
        /// </summary>
        /// <param name="controllerId">The unique identifier of the speaker.</param>
        /// <param name="duration">The fade-in duration in seconds.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="duration"/> is negative.</exception>
        void FadeInAudio(byte controllerId, float duration);

        /// <summary>
        /// Fades out the audio for the specified speaker and stops it.
        /// </summary>
        /// <param name="controllerId">The unique identifier of the speaker.</param>
        /// <param name="duration">The fade-out duration in seconds.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="duration"/> is negative.</exception>
        void FadeOutAudio(byte controllerId, float duration);

        /// <summary>
        /// Retrieves the queue status for the specified speaker.
        /// </summary>
        /// <param name="controllerId">The unique identifier of the speaker.</param>
        /// <returns>A tuple containing the number of queued clips and the current clip key, or (0, null) if the speaker is invalid.</returns>
        (int queuedCount, string currentClip) GetQueueStatus(byte controllerId);

        /// <summary>
        /// Clears the playback queue for the specified speaker.
        /// </summary>
        /// <param name="controllerId">The unique identifier of the speaker.</param>
        /// <returns>True if the queue was cleared; otherwise, false.</returns>
        bool ClearSpeakerQueue(byte controllerId);

        /// <summary>
        /// Destroys the specified speaker and releases its controller ID.
        /// </summary>
        /// <param name="controllerId">The unique identifier of the speaker.</param>
        /// <param name="forceRemoveState">If true, forces removal of the speaker's persistent state.</param>
        void DestroySpeaker(byte controllerId, bool forceRemoveState = false);

        /// <summary>
        /// Cleans up all active speakers and releases their controller IDs.
        /// </summary>
        void CleanupAllSpeakers();
        #endregion
    }
}

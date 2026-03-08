namespace AudioManagerAPI.Features.Management
{
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Speakers.State;
    using LabApi.Features.Wrappers; // Dodane dla typu Player
    using System;
    using System.IO;
    using UnityEngine;

    /// <summary>
    /// Defines the contract for managing audio playback and session lifecycle in SCP: Secret Laboratory.
    /// </summary>
    public interface IAudioManager
    {
        #region Events
        /// <summary>
        /// Occurs when a session begins audio playback.
        /// </summary>
        event Action<int> OnPlaybackStarted;

        /// <summary>
        /// Occurs when audio playback is paused for a session.
        /// </summary>
        event Action<int> OnPaused;

        /// <summary>
        /// Occurs when paused audio playback resumes for a session.
        /// </summary>
        event Action<int> OnResumed;

        /// <summary>
        /// Occurs when audio playback is stopped and the session is destroyed.
        /// </summary>
        event Action<int> OnStop;

        /// <summary>
        /// Occurs when audio clips are skipped for a session.
        /// </summary>
        event Action<int, int> OnSkipped;

        /// <summary>
        /// Occurs when the audio queue for a session becomes empty.
        /// </summary>
        event Action<int> OnQueueEmpty;
        #endregion

        #region Methods
        /// <summary>
        /// Registers an audio clip for playback with a specified key and stream provider.
        /// </summary>
        void RegisterAudio(string key, Func<Stream> streamProvider);

        /// <summary>
        /// Gets the audio playback configuration options.
        /// </summary>
        AudioOptions Options { get; }

        /// <summary>
        /// Checks if the specified session ID is valid and exists in the system.
        /// </summary>
        bool IsValidSession(int sessionId);

        /// <summary>
        /// Retrieves the abstract state of a session, regardless of whether it currently holds a physical controller.
        /// </summary>
        SpeakerState GetSessionState(int sessionId);

        /// <summary>
        /// Plays spatial audio at a specified 3D position with distance-based attenuation.
        /// </summary>
        /// <returns>The session ID, or 0 if initialization fails.</returns>
        int PlayAudio(
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
            bool autoCleanup = false);

        /// <summary>
        /// Plays non-spatial audio globally, audible to all ready players (or filtered players).
        /// </summary>
        /// <returns>The session ID, or 0 if initialization fails.</returns>
        int PlayGlobalAudio(
            string key, 
            bool loop = false, 
            float volume = 1f, 
            AudioPriority priority = AudioPriority.Medium, 
            Func<Player, bool> validPlayersFilter = null, 
            bool queue = false, 
            float fadeInDuration = 0f, 
            bool persistent = false, 
            float? lifespan = null, 
            bool autoCleanup = false);

        /// <summary>
        /// Sets the volume for the specified session.
        /// </summary>
        bool SetSessionVolume(int sessionId, float volume);

        /// <summary>
        /// Sets the 3D world position for the specified session.
        /// </summary>
        bool SetSessionPosition(int sessionId, Vector3 position);

        /// <summary>
        /// Dynamically updates the player filter for an active session.
        /// </summary>
        /// <param name="sessionId">The unique identifier of the session.</param>
        /// <param name="filter">A new function that returns true for players who should hear the audio.</param>
        /// <returns>True if the session exists and the filter was updated; otherwise, false.</returns>
        bool SetSessionPlayerFilter(int sessionId, Func<Player, bool> filter);

        /// <summary>
        /// Recovers a persistent session using its stored state.
        /// </summary>
        bool RecoverSession(int sessionId, bool resetPlayback = false);

        /// <summary>
        /// Pauses playback for the specified session.
        /// </summary>
        void PauseAudio(int sessionId);

        /// <summary>
        /// Resumes playback for the specified session.
        /// </summary>
        void ResumeAudio(int sessionId);

        /// <summary>
        /// Skips the specified number of queued clips for the session.
        /// </summary>
        void SkipAudio(int sessionId, int count);

        /// <summary>
        /// Stops playback for the specified session immediately but does not destroy the state.
        /// </summary>
        void StopAudio(int sessionId);

        /// <summary>
        /// Fades in the audio for the specified session over a duration.
        /// </summary>
        void FadeInAudio(int sessionId, float duration);

        /// <summary>
        /// Fades out the audio for the specified session and stops it.
        /// </summary>
        void FadeOutAudio(int sessionId, float duration);

        /// <summary>
        /// Retrieves the queue status for the specified session.
        /// </summary>
        (int queuedCount, string currentClip) GetQueueStatus(int sessionId);

        /// <summary>
        /// Clears the playback queue for the specified session.
        /// </summary>
        bool ClearSessionQueue(int sessionId);

        /// <summary>
        /// Destroys the specified session entirely, stopping audio and releasing resources/state.
        /// </summary>
        void DestroySession(int sessionId);

        /// <summary>
        /// Cleans up all active sessions and releases their controller IDs.
        /// </summary>
        void CleanupAllSessions();
        #endregion
    }
}
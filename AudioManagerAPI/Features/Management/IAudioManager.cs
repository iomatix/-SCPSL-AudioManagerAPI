namespace AudioManagerAPI.Features.Management
{
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Speakers.State;
    using LabApi.Features.Wrappers;
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
        /// Deploys a coordinated dual-channel spatial audio session that resolves ambient phasing artifacts.
        /// It splits rendering into a public world-space channel (excluding the source) and a private isolated
        /// 3D channel dedicated exclusively to the source player, maintaining complete spatial immersion.
        /// </summary>
        /// <param name="key">The verified identity registry key of the audio asset.</param>
        /// <param name="position">The initial world-space coordinates for the sound emission source.</param>
        /// <param name="sourcePlayer">The identity context of the player who triggered the acoustic event.</param>
        /// <param name="priority">The strict hardware allocation and eviction matrix priority tier.</param>
        /// <param name="lifespan">An optional strict time horizon boundary before automatic structural eviction.</param>
        /// <returns>A compound value layout containing both assigned session handles (world channel and source channel).</returns>
        (int worldSessionId, int sourceSessionId) PlaySpatialSmart(string key, Vector3 position, Player sourcePlayer, AudioPriority priority = AudioPriority.Medium, float? lifespan = null);

        /// <summary>
        /// Instantiates an automated spatial session that dynamically updates its acoustic panning vectors
        /// by tracking a frame-by-frame coordinate generator loop.
        /// </summary>
        /// <param name="key">The verified identity registry key of the audio asset.</param>
        /// <param name="positionProvider">A functional delegate returning the live world coordinates of the moving target.</param>
        /// <param name="validationCheck">A functional delegate assessing if the tracked instance context remains running and operationally valid.</param>
        /// <param name="priority">The strict hardware allocation and eviction matrix priority tier.</param>
        /// <param name="lifespan">The required explicit lifespan duration limit of the tracking routine pipeline loop.</param>
        /// <param name="targetPlayerFilter">An optional predicate filter controlling visual/auditory client perception visibility maps.</param>
        /// <returns>A unique network audio session handle identifier for tracking or runtime modification.</returns>
        int PlayTrackingAudio(string key, Func<Vector3> positionProvider, Func<bool> validationCheck, AudioPriority priority = AudioPriority.Medium, float? lifespan = null, Func<Player, bool> targetPlayerFilter = null);

        /// <summary>
        /// Deploys a spatial audio session that dynamically orbits around a moving or static coordinate provider
        /// using real-time trigonometric wave calculations.
        /// </summary>
        /// <param name="key">The verified identity registry key of the audio asset.</param>
        /// <param name="positionProvider">A functional delegate returning the live world coordinates of the orbit center point.</param>
        /// <param name="validationCheck">A functional delegate assessing if the tracked context layer remains running and operationally valid.</param>
        /// <param name="volume">The baseline scalar volume multiplier profile.</param>
        /// <param name="minDistance">The physical distance threshold inside which attenuation processing is bypassed.</param>
        /// <param name="maxDistance">The spatial horizon boundary beyond which volume drops to absolute zero.</param>
        /// <param name="priority">The strict hardware allocation and eviction matrix priority tier.</param>
        /// <param name="lifespan">The required explicit lifespan duration limit of the orbiting routine pipeline loop.</param>
        /// <param name="targetPlayerFilter">An optional predicate filter controlling visual/auditory client perception visibility maps.</param>
        /// <param name="maxRadius">The maximum radial boundary expansion distance for the orbit pattern.</param>
        /// <param name="minRadius">The minimum radial contraction compression distance for the orbit pattern.</param>
        /// <param name="angularSpeed">The velocity scalar defining spatial rotation speed around the origin matrix.</param>
        /// <param name="approachSpeed">The frequency factor managing the radial breathing effect via harmonic wave functions.</param>
        /// <param name="heightOffset">A static elevation adjust factor maintaining ear-level projection relative to the center vector.</param>
        /// <returns>A unique network audio session handle identifier for tracking or runtime modification.</returns>
        int PlayOrbitingAudio(
            string key,
            Func<Vector3> positionProvider,
            Func<bool> validationCheck,
            float volume,
            float minDistance,
            float maxDistance,
            AudioPriority priority = AudioPriority.Medium,
            float? lifespan = null,
            Func<Player, bool> targetPlayerFilter = null,
            float maxRadius = 3.2f,
            float minRadius = 0.6f,
            float angularSpeed = 1.1f,
            float approachSpeed = 1.5f,
            float heightOffset = 0.85f);

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

        #region Audio Streaming

        /// <summary>
        /// Appends raw PCM audio data (float, -1..1) to the playback queue of an existing session.
        /// This is the primary real-time streaming API.
        /// </summary>
        void AppendPcmData(int sessionId, float[] samples);

        /// <summary>
        /// Legacy overload for backward compatibility. Converts short PCM to float internally.
        /// </summary>
        [Obsolete("Use AppendPcmData(int, float[]) instead. This overload exists only for backward compatibility.")]
        void AppendPcmData(int sessionId, short[] pcm);

        /// <summary>
        /// Creates a new audio stream session at the specified 3D position.
        /// </summary>
        int CreateStreamSession(
            Vector3 position,
            bool isSpatial,
            float minDistance,
            float maxDistance,
            float volume,
            AudioPriority priority = AudioPriority.Medium,
            Func<Player, bool> validPlayersFilter = null,
            bool persistent = false,
            float? lifespan = null,
            bool autoCleanup = false);

        #endregion


        #endregion
    }
}
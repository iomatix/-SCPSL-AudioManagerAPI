namespace AudioManagerAPI.Features.Management
{
    using System;
    using System.IO;
    using UnityEngine;
    using AudioManagerAPI.Features.Speakers;
    using AudioManagerAPI.Features.Enums;

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
        /// Registers an audio stream provider for a given key.
        /// </summary>
        /// <param name="key">The unique key for the audio.</param>
        /// <param name="streamProvider">A function that provides the audio stream.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> or <paramref name="streamProvider"/> is null.</exception>
        void RegisterAudio(string key, Func<Stream> streamProvider);

        /// <summary>
        /// Retrieves the speaker instance for the specified controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker.</param>
        /// <returns>The speaker instance, or <c>null</c> if not found.</returns>
        ISpeaker GetSpeaker(byte controllerId);

        /// <summary>
        /// Determines whether a given controller ID refers to a valid, active speaker.
        /// </summary>
        /// <param name="controllerId">The controller ID to validate.</param>
        /// <returns><c>true</c> if the controller ID is valid; otherwise, <c>false</c>.</returns>
        bool IsValidController(byte controllerId);

        /// <summary>
        /// Plays audio at the specified position with optional configuration.
        /// </summary>
        /// <param name="key">The key identifying the audio to play.</param>
        /// <param name="position">The 3D position for playback.</param>
        /// <param name="loop">Whether the audio should loop.</param>
        /// <param name="volume">The volume level (0.0 to 1.0).</param>
        /// <param name="minDistance">The minimum distance where audio starts to fall off.</param>
        /// <param name="maxDistance">The maximum distance where audio falls to zero.</param>
        /// <param name="isSpatial">Whether to use 3D spatial audio.</param>
        /// <param name="priority">The priority of the audio.</param>
        /// <param name="configureSpeaker">An optional action to configure the speaker before playback.</param>
        /// <param name="queue">
        /// If <c>true</c>, adds the audio clip to the playback queue without interrupting the current clip.
        /// If <c>false</c>, replaces any currently playing audio.
        /// </param>
        /// <param name="persistent">
        /// If <c>true</c>, retains speaker state for possible recovery after playback ends or during scene reload.
        /// </param>
        /// <param name="lifespan">
        /// Optional duration (in seconds) after which the speaker will automatically stop playback.
        /// Used only if <paramref name="autoCleanup"/> is enabled.
        /// </param>
        /// <param name="autoCleanup">
        /// If <c>true</c> and <paramref name="lifespan"/> is set, playback will stop automatically and optionally fade out.
        /// </param>
        /// <returns>The controller ID of the speaker, or 0 if playback fails.</returns>
        byte PlayAudio(string key, Vector3 position, bool loop, float volume, float minDistance, float maxDistance, bool isSpatial, AudioPriority priority, Action<ISpeaker> configureSpeaker = null, bool queue = false, bool persistent = false, float? lifespan = null, bool autoCleanup = false);

        /// <summary>
        /// Plays audio globally, making it audible to all players who are ready and within range.
        /// </summary>
        /// <param name="key">The key identifying the audio to play.</param>
        /// <param name="loop">Whether the audio should loop continuously.</param>
        /// <param name="volume">The volume level (0.0 to 1.0).</param>
        /// <param name="priority">The priority of the audio, affecting ID allocation and playback competition.</param>
        /// <param name="queue">
        /// If <c>true</c>, the audio clip is added to the playback queue without interrupting any currently playing audio.  
        /// If <c>false</c>, it replaces the active clip immediately.
        /// </param>
        /// <param name="fadeInDuration">
        /// Duration of the fade-in effect, in seconds.  
        /// Set to <c>0</c> for instant playback with no fade.
        /// </param>
        /// <param name="persistent">
        /// If <c>true</c>, retains the speaker state after playback ends, allowing for recovery or reuse across scenes or sessions.
        /// </param>
        /// <param name="lifespan">
        /// Optional time limit (in seconds) for the speaker’s existence.  
        /// After expiration, the speaker is automatically stopped and optionally cleaned up.
        /// </param>
        /// <param name="autoCleanup">
        /// If <c>true</c> and <paramref name="lifespan"/> is provided, the speaker fades out and is destroyed when time expires.
        /// </param>
        /// <returns>The controller ID assigned to the global speaker instance, or <c>0</c> if playback fails.</returns>
        byte PlayGlobalAudio(string key, bool loop, float volume, AudioPriority priority, bool queue = false, float fadeInDuration = 0f, bool persistent = false, float? lifespan = null, bool autoCleanup = false);

        /// <summary>
        /// Recovers a previously persistent speaker by reconstructing its state and resuming playback.
        /// Useful for dynamic scene reloads or persistence across player sessions.
        /// </summary>
        /// <param name="controllerId">The controller ID associated with the speaker's saved state.</param>
        /// <param name="resetPlayback">
        /// If <c>true</c>, resets the playback position to the beginning of the clip.
        /// </param>
        /// <returns>
        /// <c>true</c> if speaker recovery succeeds; <c>false</c> if the state is invalid or recovery fails.
        /// </returns>
        bool RecoverSpeaker(byte controllerId, bool resetPlayback = false);

        /// <summary>
        /// Stops the audio associated with the specified controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to stop.</param>
        void StopAudio(byte controllerId);

        /// <summary>
        /// Pauses the audio associated with the specified controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to pause.</param>
        void PauseAudio(byte controllerId);

        /// <summary>
        /// Resumes paused audio associated with the specified controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to resume.</param>
        void ResumeAudio(byte controllerId);

        /// <summary>
        /// Skips a specified number of audio clips in the playback queue for the given speaker.
        /// </summary>
        /// <param name="controllerId">The controller ID associated with the speaker.</param>
        /// <param name="count">
        /// The number of audio clips to skip.  
        /// This includes the currently playing clip if applicable.
        /// </param>
        void SkipAudio(byte controllerId, int count);


        /// <summary>
        /// Fades in audio associated with the specified controller ID for specified duration.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to fade in.</param>
        /// <param name="duration">The duration of the fade-in effect in seconds.</param>
        void FadeInAudio(byte controllerId, float duration);

        /// <summary>
        /// Fades out audio associated with the specified controller ID for specified duration.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to fade out.</param>
        /// <param name="duration">The duration of the fade-out effect in seconds.</param>
        void FadeOutAudio(byte controllerId, float duration);

        /// <summary>
        /// Stops playback, destroys the speaker instance tied to the provided controller ID,  
        /// and optionally removes its state permanently from memory.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to destroy.</param>
        /// <param name="forceRemoveState">
        /// If <c>true</c>, the state associated with the controller is forcibly removed,
        /// even if flagged as persistent.
        /// </param>
        void DestroySpeaker(byte controllerId, bool forceRemoveState = false);

        /// <summary>
        /// Cleans up all active speakers and releases their controller IDs.
        /// </summary>
        void CleanupAllSpeakers();

        #endregion
    }
}
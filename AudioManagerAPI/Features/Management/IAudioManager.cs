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
        /// <summary>
        /// Registers an audio stream provider for a given key.
        /// </summary>
        /// <param name="key">The unique key for the audio.</param>
        /// <param name="streamProvider">A function that provides the audio stream.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> or <paramref name="streamProvider"/> is null.</exception>
        void RegisterAudio(string key, Func<Stream> streamProvider);

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
        /// <returns>The controller ID of the speaker, or 0 if playback fails.</returns>
        byte PlayAudio(string key, Vector3 position, bool loop, float volume, float minDistance, float maxDistance, bool isSpatial, AudioPriority priority, Action<ISpeaker> configureSpeaker = null, bool queue = false);

        /// <summary>
        /// Plays audio globally, audible to all ready players.
        /// </summary>
        /// <param name="key">The key identifying the audio to play.</param>
        /// <param name="loop">Whether the audio should loop.</param>
        /// <param name="volume">The volume level (0.0 to 1.0).</param>
        /// <param name="priority">The priority of the audio.</param>
        /// <param name="queue">Whether to queue the audio instead of playing immediately.</param>
        /// <param name="fadeInDuration">The duration of the fade-in effect in seconds (0 for no fade).</param>
        /// <returns>The controller ID of the speaker, or 0 if playback fails.</returns>
        byte PlayGlobalAudio(string key, bool loop, float volume, AudioPriority priority, bool queue = false, float fadeInDuration = 0f);

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
        /// Skips the current or queued audio clips for the specified controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker.</param>
        /// <param name="count">The number of clips to skip, including the current one.</param>
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
        /// Destroys the speaker with the specified controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to destroy.</param>
        void DestroySpeaker(byte controllerId);

        /// <summary>
        /// Cleans up all active speakers and releases their controller IDs.
        /// </summary>
        void CleanupAllSpeakers();

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
    }
}
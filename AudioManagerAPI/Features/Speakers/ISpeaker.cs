namespace AudioManagerAPI.Features.Speakers
{
    using System;
    using UnityEngine;

    /// <summary>
    /// Represents a physical hardware speaker capable of playing audio samples,
    /// managing volume, and spatial positioning.
    /// </summary>
    public interface ISpeaker
    {
        /// <summary>
        /// Occurs when the speaker's playback queue has finished processing all audio clips.
        /// </summary>
        event Action QueueEmpty;

        /// <summary>
        /// Plays the specified audio samples starting from the given playback position.
        /// </summary>
        void Play(float[] samples, bool loop, float playbackPosition = 0f);

        /// <summary>
        /// Queues additional audio samples to play after the current ones.
        /// </summary>
        void Queue(float[] samples, bool loop);

        /// <summary>
        /// Stops the current audio playback.
        /// </summary>
        void Stop();

        /// <summary>
        /// Skips the current or queued audio clips.
        /// </summary>
        void Skip(int count);

        /// <summary>
        /// Pauses the current audio playback.
        /// </summary>
        void Pause();

        /// <summary>
        /// Resumes the paused audio playback.
        /// </summary>
        void Resume();

        /// <summary>
        /// Fades in the audio volume over the specified duration.
        /// </summary>
        void FadeIn(float duration);

        /// <summary>
        /// Fades out the audio volume over the specified duration and stops playback.
        /// </summary>
        void FadeOut(float duration, Action onComplete = null);

        /// <summary>
        /// Destroys the speaker, releasing all associated LabAPI and Unity resources.
        /// </summary>
        void Destroy();

        // Moved from ISpeakerWithPlayerFilter to base:

        /// <summary>
        /// Sets the volume of the audio (0.0 to 1.0).
        /// </summary>
        void SetVolume(float volume);

        /// <summary>
        /// Sets the minimum distance for audio falloff in Unity units.
        /// </summary>
        void SetMinDistance(float minDistance);

        /// <summary>
        /// Sets the maximum distance for audio falloff in Unity units.
        /// </summary>
        void SetMaxDistance(float maxDistance);

        /// <summary>
        /// Sets whether the audio is spatialized (3D audio).
        /// </summary>
        void SetSpatialization(bool isSpatial);

        /// <summary>
        /// Sets the 3D world position of the physical speaker.
        /// </summary>
        void SetPosition(Vector3 position);
    }
}
namespace AudioManagerAPI.Features.Speakers
{
    /// <summary>
    /// Represents a speaker capable of playing audio samples at a specific position.
    /// </summary>
    public interface ISpeaker
    {
        /// <summary>
        /// Plays the specified audio samples.
        /// </summary>
        /// <param name="samples">The PCM audio samples to play.</param>
        /// <param name="loop">Whether the audio should loop.</param>
        void Play(float[] samples, bool loop);

        /// <summary>
        /// Queues additional audio samples to play after the current ones.
        /// </summary>
        /// <param name="samples">The PCM audio samples to queue.</param>
        /// <param name="loop">Whether the queued audio should loop.</param>
        void Queue(float[] samples, bool loop);

        /// <summary>
        /// Stops the current audio playback.
        /// </summary>
        void Stop();

        /// <summary>
        /// Skips the current or queued audio clips.
        /// </summary>
        /// <param name="count">The number of clips to skip, including the current one.</param>
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
        /// <param name="duration">The duration of the fade-in in seconds.</param>
        void FadeIn(float duration);

        /// <summary>
        /// Fades out the audio volume over the specified duration and stops playback.
        /// </summary>
        /// <param name="duration">The duration of the fade-out in seconds.</param>
        void FadeOut(float duration);

        /// <summary>
        /// Destroys the speaker, releasing all associated resources.
        /// </summary>
        void Destroy();
    }
}



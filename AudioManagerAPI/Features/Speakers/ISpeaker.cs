namespace AudioManagerAPI
{
    /// <summary>
    /// Represents a speaker capable of playing audio samples at a specific position.
    /// </summary>
    public interface ISpeaker
    {
        /// <summary>
        /// Plays the provided audio samples with an option to loop.
        /// </summary>
        /// <param name="samples">The audio samples to play.</param>
        /// <param name="loop">Whether the audio should loop.</param>
        void Play(float[] samples, bool loop);

        /// <summary>
        /// Stops the currently playing audio.
        /// </summary>
        void Stop();

        /// <summary>
        /// Destroys the speaker, releasing all associated resources.
        /// </summary>
        void Destroy();
    }
}
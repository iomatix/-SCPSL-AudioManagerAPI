namespace AudioManagerAPI.Defaults
{
    using AudioManagerAPI.Features.Speakers;
    using LabAPI;       // adjust to your actual LabAPI namespace
    using System.Numerics;
    using UnityEngine;  // for Vector3 and AudioSource

    /// <summary>
    /// Adapts LabAPI’s SpeakerToy to the ISpeaker interface, providing built-in
    /// pause and resume support out of the box.
    /// </summary>
    public class DefaultSpeakerToyAdapter : ISpeaker
    {
        /// <summary>
        /// Gets a value indicating whether this speaker supports pause/resume.
        /// </summary>
        public bool CanPause { get; } = true;

        private readonly SpeakerToy toy;
        private byte[] samples;
        private bool loop;
        private float pausedTime;
        private bool isPaused;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultSpeakerToyAdapter"/> class.
        /// </summary>
        /// <param name="position">The 3D world position where the speaker will play audio.</param>
        /// <param name="controllerId">The unique controller ID allocated by AudioManager.</param>
        public DefaultSpeakerToyAdapter(Vector3 position, byte controllerId)
        {
            toy = new SpeakerToy(position, controllerId);
        }

        /// <summary>
        /// Plays the specified PCM sample data, optionally looping.
        /// </summary>
        /// <param name="samples">Raw PCM samples as a byte array.</param>
        /// <param name="loop">True to loop playback; false to play once.</param>
        public void Play(byte[] samples, bool loop)
        {
            this.samples = samples;
            this.loop = loop;
            isPaused = false;
            toy.Play(samples, loop);
        }

        /// <summary>
        /// Stops playback immediately and resets pause state.
        /// </summary>
        public void Stop()
        {
            toy.Stop();
            isPaused = false;
        }

        /// <summary>
        /// Pauses playback at the current time position, if supported.
        /// </summary>
        public void Pause()
        {
            if (!CanPause || isPaused)
                return;

            // Capture current playback position then stop
            pausedTime = toy.AudioSource.time;
            toy.Stop();
            isPaused = true;
        }

        /// <summary>
        /// Resumes playback from the last paused position, if supported.
        /// </summary>
        public void Resume()
        {
            if (!CanPause || !isPaused)
                return;

            toy.Play(samples, loop);
            toy.AudioSource.time = pausedTime;
            isPaused = false;
        }
    }
}
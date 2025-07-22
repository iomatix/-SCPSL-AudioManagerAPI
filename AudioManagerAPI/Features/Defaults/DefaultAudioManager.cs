namespace AudioManagerAPI.Defaults
{
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Features.Management;

    /// <summary>
    /// Static entry point for a ready-to-use AudioManager wired up with
    /// the default LabAPI-based speaker implementation. Call RegisterDefaults()
    /// once at startup, then use the convenience methods for play/pause/resume/stop.
    /// </summary>
    public static class DefaultAudioManager
    {
        /// <summary>
        /// The singleton instance of <see cref="IAudioManager"/>
        /// configured with <see cref="DefaultSpeakerFactory"/>.
        /// </summary>
        public static IAudioManager Instance { get; private set; }

        /// <summary>
        /// Initializes the default AudioManager using
        /// <see cref="DefaultSpeakerFactory"/> and an <see cref="AudioCache"/>.
        /// Must be called before invoking any other methods on this class.
        /// </summary>
        /// <param name="cacheSize">
        /// Maximum number of loaded audio samples to keep in memory.
        /// Defaults to 50.
        /// </param>
        public static void RegisterDefaults(int cacheSize = 50)
        {
            var factory = new DefaultSpeakerFactory();
            Instance = new AudioManager(factory, cacheSize);
        }

        /// <summary>
        /// Plays the audio registered under the given key with default parameters:
        /// non-spatial, full volume, no looping, at world origin, low priority.
        /// </summary>
        /// <param name="key">The unique key of a previously registered audio stream.</param>
        /// <returns>
        /// The controller ID allocated for this playback instance, or 0 if playback failed.
        /// </returns>
        public static byte Play(string key)
            => Instance.PlayAudio(
                key,
                position: Vector3.zero,
                loop: false,
                volume: 1f,
                minDistance: 1f,
                maxDistance: 10f,
                isSpatial: false,
                priority: AudioPriority.Low
            );

        /// <summary>
        /// Pauses playback of the audio associated with the specified controller ID,
        /// if the underlying speaker supports pause/resume.
        /// </summary>
        /// <param name="id">Controller ID of the speaker to pause.</param>
        public static void Pause(byte id)
            => Instance.PauseAudio(id);

        /// <summary>
        /// Resumes playback of the paused audio associated with the specified controller ID,
        /// if the underlying speaker supports pause/resume.
        /// </summary>
        /// <param name="id">Controller ID of the speaker to resume.</param>
        public static void Resume(byte id)
            => Instance.ResumeAudio(id);

        /// <summary>
        /// Stops playback and destroys the speaker associated with the specified controller ID.
        /// </summary>
        /// <param name="id">Controller ID of the speaker to stop.</param>
        public static void Stop(byte id)
            => Instance.StopAudio(id);
    }
}
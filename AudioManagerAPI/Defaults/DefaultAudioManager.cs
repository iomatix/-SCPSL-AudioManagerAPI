namespace AudioManagerAPI.Defaults
{
    using System;
    using System.IO;
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Features.Management;

    /// <summary>
    /// Static entry point for a ready-to-use AudioManager configured with
    /// the default LabAPI-based speaker implementation and customizable settings
    /// loaded from an external configuration file on first access.
    /// <example>
    /// // Plugin code
    /// DefaultAudioManager.RegisterAudio("explosionSound", () => Assembly.GetExecutingAssembly()
    ///     .GetManifestResourceStream("MyPlugin.Audio.explosion.wav"));
    ///
    /// byte id = DefaultAudioManager.Play("explosionSound", queue: true, fadeInDuration: 2f);
    /// DefaultAudioManager.Pause(id);
    /// DefaultAudioManager.Resume(id);
    /// DefaultAudioManager.Skip(id, 1);
    /// DefaultAudioManager.FadeOut(id, 2f);
    /// DefaultAudioManager.Stop(id);
    /// </example>
    /// The configuration file (e.g. AudioConfig.json) is auto-created on first launch
    /// with default settings such as speaker factory type and cache size.
    /// </summary>
    public static class DefaultAudioManager
    {
        
        /// <summary>
        /// Singleton AudioManager instance initialized automatically on first access
        /// using configuration settings from AudioConfig.json.
        /// </summary>
        public static IAudioManager Instance { get; }

        public static AudioOptions Options => (Instance as AudioManager)?.Options ?? throw new InvalidOperationException("DefaultAudioManager.Instance is not AudioManager.");
        
        static DefaultAudioManager()
        {
            var config = AudioConfigLoader.LoadOrCreate();
            ISpeakerFactory factory = config.UseDefaultSpeakerFactory
                ? new DefaultSpeakerFactory()
                : StaticSpeakerFactory.Instance;
        
            Instance = new AudioManager(factory);
        }

        /// <summary>
        /// Registers an audio stream provider for a given key.
        /// </summary>
        /// <param name="key">The unique key for the audio.</param>
        /// <param name="streamProvider">A function that provides the audio stream.</param>
        public static void RegisterAudio(string key, Func<Stream> streamProvider)
        {
            Instance.RegisterAudio(key, streamProvider);
        }

        /// <summary>
        /// Plays the audio registered under the given key with default parameters:
        /// non-spatial, full volume, no looping, low priority, audible to all ready players.
        /// </summary>
        /// <param name="key">The unique key of a previously registered audio stream.</param>
        /// <param name="queue">Whether to queue the audio instead of playing immediately.</param>
        /// <param name="fadeInDuration">The duration of the fade-in effect in seconds (0 for no fade).</param>
        /// <returns>
        /// The controller ID allocated for this playback instance, or 0 if playback failed.
        /// </returns>
        public static byte Play(string key, bool queue = false, float fadeInDuration = 0f)
            => Instance.PlayGlobalAudio(
                key,
                loop: false,
                volume: 1f,
                priority: AudioPriority.Low,
                queue: queue,
                fadeInDuration: fadeInDuration
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
        /// Skips the current or queued audio clips for the specified controller ID.
        /// </summary>
        /// <param name="id">Controller ID of the speaker.</param>
        /// <param name="count">The number of clips to skip, including the current one.</param>
        public static void Skip(byte id, int count = 1)
            => Instance.SkipAudio(id, count);

        /// <summary>
        /// Fades in the audio volume for the specified controller ID over the given duration.
        /// </summary>
        /// <param name="id">Controller ID of the speaker to fade in.</param>
        /// <param name="duration">The duration of the fade-in in seconds.</param>
        public static void FadeIn(byte id, float duration)
            => Instance.FadeInAudio(id, duration);

        /// <summary>
        /// Fades out the audio volume for the specified controller ID over the given duration and stops playback.
        /// </summary>
        /// <param name="id">Controller ID of the speaker to fade out.</param>
        /// <param name="duration">The duration of the fade-out in seconds.</param>
        public static void FadeOut(byte id, float duration)
            => Instance.FadeOutAudio(id, duration);

        /// <summary>
        /// Stops playback and destroys the speaker associated with the specified controller ID,
        /// releasing the controller ID back to the pool.
        /// </summary>
        /// <param name="id">Controller ID of the speaker to stop and destroy.</param>
        public static void Stop(byte id)
            => Instance.DestroySpeaker(id);
    }
}

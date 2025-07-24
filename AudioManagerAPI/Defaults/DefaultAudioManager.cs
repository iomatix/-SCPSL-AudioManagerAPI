namespace AudioManagerAPI.Defaults
{
    using System;
    using System.IO;
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Features.Management;

    /// <summary>
    /// Static entry point for a ready-to-use AudioManager wired up with
    /// the default LabAPI-based speaker implementation. Call RegisterDefaults()
    /// once at startup, then use the convenience methods for play/pause/resume/stop.
    /// <example>
    /// // Plugin startup
    /// DefaultAudioManager.RegisterDefaults();
    ///
    /// // Anywhere in plugin code
    /// DefaultAudioManager.RegisterAudio("explosionSound", () => Assembly.GetExecutingAssembly().GetManifestResourceStream("MyPlugin.Audio.explosion.wav"));
    /// byte id = DefaultAudioManager.Play("explosionSound", queue: true, fadeInDuration: 2f);
    /// DefaultAudioManager.Pause(id);
    /// DefaultAudioManager.Resume(id);
    /// DefaultAudioManager.Skip(id, 1);
    /// DefaultAudioManager.FadeOut(id, 2f);
    /// DefaultAudioManager.Stop(id);
    /// </example>
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
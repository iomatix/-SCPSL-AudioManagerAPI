namespace AudioManagerAPI.Defaults
{
    using AudioManagerAPI.Config;
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Features.Management;
    using AudioManagerAPI.Features.Speakers;
    using AudioManagerAPI.Features.Static;
    using System;
    using System.IO;

    /// <summary>
    /// Provides a static, ready-to-use entry point for the AudioManager.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The manager is preconfigured with the default LabAPI-based speaker
    /// implementation. Configuration settings are automatically loaded
    /// from an external configuration file on first access.
    /// </para>
    ///
    /// <para>
    /// If the configuration file does not exist (e.g. <c>AudioConfig.json</c>),
    /// it will be generated automatically on the first launch with default
    /// values such as the speaker factory type and audio cache size.
    /// </para>
    /// 
    /// <example>
    /// Example usage in a plugin:
    /// <code>
    /// DefaultAudioManager.RegisterAudio(
    ///     "explosionSound",
    ///     () => Assembly.GetExecutingAssembly()
    ///         .GetManifestResourceStream("MyPlugin.Audio.explosion.wav"));
    ///
    /// int sessionId = DefaultAudioManager.Play(
    ///     "explosionSound",
    ///     queue: true,
    ///     fadeInDuration: 2f);
    ///
    /// DefaultAudioManager.Pause(sessionId);
    /// DefaultAudioManager.Resume(sessionId);
    /// DefaultAudioManager.Skip(sessionId, 1);
    /// DefaultAudioManager.FadeOut(sessionId, 2f);
    /// DefaultAudioManager.Stop(sessionId);
    /// </code>
    /// </example>
    /// </remarks>
    public static class DefaultAudioManager
    {
        public static AudioOptions Options => (Instance as AudioManager)?.Options ?? throw new InvalidOperationException("DefaultAudioManager.Instance is not AudioManager.");

        /// <summary>
        /// Singleton AudioManager instance initialized lazily on first access
        /// using configuration settings from AudioConfig.json.
        /// </summary>
        public static IAudioManager Instance => _lazyInstance.Value;

        private static readonly Lazy<IAudioManager> _lazyInstance = new Lazy<IAudioManager>(() =>
        {
            var config = AudioConfigLoader.LoadOrCreate();
            ISpeakerFactory factory = config.UseDefaultSpeakerFactory
                ? new DefaultSpeakerFactory()
                : StaticSpeakerFactory.Instance;

            return new AudioManager(factory);
        }, isThreadSafe: true);

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
        /// The session ID allocated for this playback request, or 0 if initialization failed.
        /// </returns>
        public static int Play(string key, bool queue = false, float fadeInDuration = 0f)
            => Instance.PlayGlobalAudio(
                key,
                loop: false,
                volume: 1f,
                priority: AudioPriority.Low,
                validPlayersFilter: null,
                queue: queue,
                fadeInDuration: fadeInDuration
            );

        /// <summary>
        /// Pauses playback of the audio associated with the specified session ID.
        /// </summary>
        /// <param name="sessionId">The session ID to pause.</param>
        public static void Pause(int sessionId)
            => Instance.PauseAudio(sessionId);

        /// <summary>
        /// Resumes playback of the paused audio associated with the specified session ID.
        /// </summary>
        /// <param name="sessionId">The session ID to resume.</param>
        public static void Resume(int sessionId)
            => Instance.ResumeAudio(sessionId);

        /// <summary>
        /// Skips the current or queued audio clips for the specified session ID.
        /// </summary>
        /// <param name="sessionId">The session ID to skip clips on.</param>
        /// <param name="count">The number of clips to skip, including the current one.</param>
        public static void Skip(int sessionId, int count = 1)
            => Instance.SkipAudio(sessionId, count);

        /// <summary>
        /// Fades in the audio volume for the specified session ID over the given duration.
        /// </summary>
        /// <param name="sessionId">The session ID to fade in.</param>
        /// <param name="duration">The duration of the fade-in in seconds.</param>
        public static void FadeIn(int sessionId, float duration)
            => Instance.FadeInAudio(sessionId, duration);

        /// <summary>
        /// Fades out the audio volume for the specified session ID over the given duration and stops playback.
        /// </summary>
        /// <param name="sessionId">The session ID to fade out.</param>
        /// <param name="duration">The duration of the fade-out in seconds.</param>
        public static void FadeOut(int sessionId, float duration)
            => Instance.FadeOutAudio(sessionId, duration);

        /// <summary>
        /// Stops playback and destroys the session entirely, releasing resources and states.
        /// </summary>
        /// <param name="sessionId">The session ID to stop and destroy.</param>
        public static void Stop(int sessionId)
            => Instance.DestroySession(sessionId);
    }
}
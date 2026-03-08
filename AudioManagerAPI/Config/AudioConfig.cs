namespace AudioManagerAPI.Config
{
    /// <summary>
    /// Configuration settings loaded from a JSON file (AudioConfig.json).
    /// <para>
    /// Used to initialize the <see cref="AudioManagerAPI.Features.Management.AudioManager"/> and define default audio system behavior.
    /// </para>
    /// </summary>
    public class AudioConfig
    {
        /// <summary>
        /// Gets or sets the maximum number of audio clips stored in the LRU cache.
        /// Higher values consume more RAM but reduce disk I/O for frequently played sounds.
        /// Default is 50.
        /// </summary>
        public int CacheSize { get; set; } = 50;

        /// <summary>
        /// Gets or sets a value indicating whether to use the built-in LabAPI SpeakerToy adapter factory.
        /// If false, a custom factory must be provided or injected.
        /// Default is true.
        /// </summary>
        public bool UseDefaultSpeakerFactory { get; set; } = true;

        /// <summary>
        /// Gets or sets the default duration (in seconds) for fade-in effects when using global play methods.
        /// Default is 1.0 second.
        /// </summary>
        public float DefaultFadeInDuration { get; set; } = 1f;

        /// <summary>
        /// Gets or sets the default duration (in seconds) for fade-out effects when audio is automatically stopped.
        /// Default is 1.0 second.
        /// </summary>
        public float DefaultFadeOutDuration { get; set; } = 1f;
    }
}
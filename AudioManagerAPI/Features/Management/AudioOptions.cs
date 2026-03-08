namespace AudioManagerAPI.Features.Management
{
    /// <summary>
    /// Represents the read-only runtime audio playback settings currently used by the <see cref="AudioManager"/>.
    /// These values are loaded from the configuration file during initialization and dictate the system's behavior.
    /// </summary>
    public class AudioOptions
    {
        /// <summary>
        /// Gets the maximum number of audio clips stored in the LRU cache.
        /// </summary>
        public int CacheSize { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the built-in default speaker factory is being used.
        /// </summary>
        public bool UseDefaultSpeakerFactory { get; internal set; }

        /// <summary>
        /// Gets the default duration (in seconds) for fade-in effects across the API.
        /// </summary>
        public float DefaultFadeInDuration { get; internal set; }

        /// <summary>
        /// Gets the default duration (in seconds) for fade-out effects across the API.
        /// </summary>
        public float DefaultFadeOutDuration { get; internal set; }
    }
}
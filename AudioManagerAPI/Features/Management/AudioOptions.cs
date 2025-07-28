namespace AudioManagerAPI.Features.Management
{
    /// <summary>
    /// Represents runtime audio playback settings used by <see cref="AudioManager"/>.
    /// Includes cache size, default speaker factory preference, and fade durations.
    /// </summary>
    public class AudioOptions
    {
        public int CacheSize { get; set; }
        public bool UseDefaultSpeakerFactory { get; set; }
        public float DefaultFadeInDuration { get; set; }
        public float DefaultFadeOutDuration { get; set; }

    }
}
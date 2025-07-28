namespace AudioManagerAPI.Config
{
    /// <summary>
    /// Configuration settings loaded from a JSON file (AudioConfig.json).
    /// Used to initialize the <see cref="AudioManager"/> and define default audio system behavior.
    /// </summary>
    public class AudioConfig
    {
        public int CacheSize { get; set; } = 50;
        public bool UseDefaultSpeakerFactory { get; set; } = true;
        public float DefaultFadeInDuration { get; set; } = 1f;
        public float DefaultFadeOutDuration { get; set; } = 1f;
        
    }
}

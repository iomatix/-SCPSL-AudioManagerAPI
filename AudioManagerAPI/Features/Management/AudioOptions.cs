    public class AudioOptions
    {
        public int CacheSize { get; set; }
        public bool UseDefaultSpeakerFactory { get; set; }
        public float DefaultFadeInDuration { get; set; }
        public float DefaultFadeOutDuration { get; set; }

        public Dictionary<string, int> RequiredCacheSizes { get; set; }
    }

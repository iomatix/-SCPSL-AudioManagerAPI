namespace AudioManagerAPI.Config
{
    using UnityEngine;

    /// <summary>
    /// Configuration data profile representing system capabilities and engine defaults.
    /// </summary>
    public class AudioConfig
    {
        public int CacheSize { get; set; } = 50;
        public bool UseDefaultSpeakerFactory { get; set; } = true;
        public float DefaultFadeInDuration { get; set; } = 1f;
        public float DefaultFadeOutDuration { get; set; } = 1f;

        /// <summary>
        /// Validates parameters defensively and forces hard system clamps 
        /// to guarantee downstream pipeline operations never throw exceptions.
        /// </summary>
        public void Validate()
        {
            // Defensive alignment: AudioCache capacity must be strictly positive.
            if (CacheSize <= 0)
            {
                CacheSize = 50;
            }

            // Clamping time horizons to non-negative scalars to preserve math integrity in MEC coroutines.
            DefaultFadeInDuration = Mathf.Max(0f, DefaultFadeInDuration);
            DefaultFadeOutDuration = Mathf.Max(0f, DefaultFadeOutDuration);
        }
    }
}
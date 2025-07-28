namespace AudioManagerAPI.Config
{
    using System;
    using System.IO;
    using System.Text.Json;

    using JsonSerializer = System.Text.Json.JsonSerializer;

    public static class AudioConfigLoader
    {
        private const string ConfigPath = "Configs/AudioConfig.json";

        /// <summary>
        /// Loads the audio configuration from disk or creates a new file with default settings.
        /// </summary>
        /// <returns>The loaded or newly created <see cref="AudioConfig"/> instance.</returns>
        public static AudioConfig LoadOrCreate()
        {
            if (!File.Exists(ConfigPath))
            {
                var defaultConfig = new AudioConfig { CacheSize = 50, UseDefaultSpeakerFactory = true };
                string directory = Path.GetDirectoryName(ConfigPath);
                if (directory == null)
                    throw new InvalidOperationException("Invalid config path: no directory name found.");


                Directory.CreateDirectory(directory);
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true }));
                return defaultConfig;
            }

            string json = File.ReadAllText(ConfigPath);
            AudioConfig config = JsonSerializer.Deserialize<AudioConfig>(json);
            if (config == null)
                throw new InvalidOperationException("Failed to parse AudioConfig.json.");
            return config;
        }
    }
}
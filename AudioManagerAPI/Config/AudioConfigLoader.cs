namespace AudioManagerAPI.Config
{
    using System;
    using System.IO;
    using System.Text.Json;

    using LabApi.Features.Console;

    /// <summary>
    /// <para>
    /// Provides utility methods for loading and initializing the <see cref="AudioConfig"/> configuration file.
    /// </para>
    ///
    /// <para>
    /// This class ensures that the audio configuration used by the AudioManager system
    /// always resolves to a valid instance. On load, it attempts to read the configuration
    /// from <c>Configs/AudioConfig.json</c> located in the server's base directory.
    /// </para>
    ///
    /// <para>
    /// Loading behavior:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// If the configuration file does not exist, a new one is created using default values.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// If the file exists but deserialization results in <c>null</c>, the file is rewritten
    /// with default values to restore a valid configuration.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// If the file contains invalid JSON, the error is logged and a default configuration
    /// instance is returned without overwriting the user's file.
    /// </description>
    /// </item>
    /// </list>
    /// </summary>
    public static class AudioConfigLoader
    {
        // Using AppDomain.CurrentDomain.BaseDirectory ensures the path is absolute and anchored to the server's root.
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "AudioConfig.json");

        /// <summary>
        /// Loads the audio configuration from disk.
        /// If the file does not exist or is corrupted, it creates a new file (or overwrites the corrupted one)
        /// with default settings and returns a safe fallback instance.
        /// </summary>
        /// <returns>The loaded or newly created <see cref="AudioConfig"/> instance.</returns>
        public static AudioConfig LoadOrCreate()
        {
            var defaultConfig = new AudioConfig(); // Relies on defaults defined inside the POCO

            try
            {
                if (!File.Exists(ConfigPath))
                {
                    SaveConfig(defaultConfig);
                    Logger.Info($"[AudioConfigLoader] Created new configuration file at: {ConfigPath}");
                    return defaultConfig;
                }

                string json = File.ReadAllText(ConfigPath);
                AudioConfig config = JsonSerializer.Deserialize<AudioConfig>(json);

                if (config == null)
                {
                    Logger.Warn("[AudioConfigLoader] AudioConfig.json parsed to null. Using and rewriting default settings.");
                    SaveConfig(defaultConfig);
                    return defaultConfig;
                }

                return config;
            }
            catch (JsonException ex)
            {
                Logger.Error($"[AudioConfigLoader] Failed to parse AudioConfig.json (Invalid JSON format). Using default settings. Error: {ex.Message}");
                // We do not overwrite here to avoid deleting the user's broken config they might be trying to fix.
                return defaultConfig;
            }
            catch (Exception ex)
            {
                Logger.Error($"[AudioConfigLoader] Unexpected error loading AudioConfig.json. Using default settings. Error: {ex.Message}");
                return defaultConfig;
            }
        }

        /// <summary>
        /// Helper method to serialize and save the configuration to disk, ensuring directory existence.
        /// </summary>
        private static void SaveConfig(AudioConfig config)
        {
            try
            {
                string directory = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, options));
            }
            catch (Exception ex)
            {
                Logger.Error($"[AudioConfigLoader] Failed to write default configuration to disk. Error: {ex.Message}");
            }
        }
    }
}
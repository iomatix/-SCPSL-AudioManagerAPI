namespace AudioManagerAPI.Config
{
    using System;
    using System.IO;
    using System.Text.Json;
    using LabApi.Features.Console;

    /// <summary>
    /// Thread-safe I/O orchestration engine responsible for serialization, 
    /// deserialization, and defensive recovery of system configuration files.
    /// </summary>
    public static class AudioConfigLoader
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "AudioConfig.json");
        private static readonly object FileLock = new object();

        /// <summary>
        /// Resolves the system configuration from disk with atomic fallback integrity.
        /// </summary>
        public static AudioConfig LoadOrCreate()
        {
            var defaultConfig = new AudioConfig();

            // Broad synchronization barrier to protect the OS file system handle from concurrent access tasks.
            lock (FileLock)
            {
                try
                {
                    if (!File.Exists(ConfigPath))
                    {
                        SaveConfigInternal(defaultConfig);
                        Logger.Info($"[AudioConfigLoader] Generated missing configuration manifest at: {ConfigPath}");
                        return defaultConfig;
                    }

                    string json = File.ReadAllText(ConfigPath);
                    AudioConfig loadedConfig = JsonSerializer.Deserialize<AudioConfig>(json);

                    if (loadedConfig == null)
                    {
                        Logger.Warn("[AudioConfigLoader] AudioConfig.json evaluated to null. Rewriting file with fallback defaults.");
                        SaveConfigInternal(defaultConfig);
                        return defaultConfig;
                    }

                    // CRITICAL STEP: Sanitize parameters before exposing the object instance to engine modules.
                    loadedConfig.Validate();
                    return loadedConfig;
                }
                catch (JsonException ex)
                {
                    Logger.Error($"[AudioConfigLoader] Critical syntax corruption detected inside AudioConfig.json. Falling back to memory defaults to isolate crash. Error: {ex.Message}");
                    return defaultConfig;
                }
                catch (Exception ex)
                {
                    Logger.Error($"[AudioConfigLoader] Unhandled I/O pipeline exception while accessing configuration disk blocks: {ex.Message}");
                    return defaultConfig;
                }
            }
        }

        /// <summary>
        /// Explicitly pushes a runtime configuration instance to memory disk blocks.
        /// </summary>
        public static void SaveConfig(AudioConfig config)
        {
            if (config == null) return;

            lock (FileLock)
            {
                SaveConfigInternal(config);
            }
        }

        /// <summary>
        /// Internal lock-free serialization pass. Must be guarded by the calling scope.
        /// </summary>
        private static void SaveConfigInternal(AudioConfig config)
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
                Logger.Error($"[AudioConfigLoader] Internal system failure while writing configuration layout payload: {ex.Message}");
            }
        }
    }
}
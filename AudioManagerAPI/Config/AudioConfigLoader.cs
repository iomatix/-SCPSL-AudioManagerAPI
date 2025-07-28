public static class AudioConfigLoader
{
    private const string ConfigPath = "Configs/AudioConfig.json";

    public static AudioConfig LoadOrCreate()
    {
        if (!File.Exists(ConfigPath))
        {
            var defaultConfig = new AudioConfig();
            var json = JsonConvert.SerializeObject(defaultConfig, Formatting.Indented);
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, json);
            return defaultConfig;
        }

        var existingJson = File.ReadAllText(ConfigPath);
        return JsonConvert.DeserializeObject<AudioConfig>(existingJson)!;
    }
}

using System.Text.Json;

namespace DocRag.Shared;

public static class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigPaths.ConfigFilePath))
        {
            return new AppConfig();
        }

        var json = File.ReadAllText(ConfigPaths.ConfigFilePath);
        return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }

    public static void Save(AppConfig config)
    {
        Directory.CreateDirectory(ConfigPaths.ConfigDirectory);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPaths.ConfigFilePath, json);
    }
}

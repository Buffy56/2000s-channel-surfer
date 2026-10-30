using System.Text.Json;
using ChannelSurfer.Models;

namespace ChannelSurfer.Services;

/// <summary>Reads/writes settings.json in the config directory, falling back to defaults on any read failure.</summary>
public class SettingsService
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };
    private readonly string _path;

    public SettingsService(string configDir)
    {
        Directory.CreateDirectory(configDir);
        _path = Path.Combine(configDir, "settings.json");
    }

    public AppSettings Get()
    {
        if (!File.Exists(_path)) return new AppSettings();
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings(); // corrupt file: never crash the app over bad config
        }
    }

    public void Save(AppSettings settings)
        => File.WriteAllText(_path, JsonSerializer.Serialize(settings, WriteOptions));
}

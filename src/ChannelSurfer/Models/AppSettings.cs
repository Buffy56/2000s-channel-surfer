namespace ChannelSurfer.Models;

/// <summary>User-editable settings persisted to settings.json on the config volume.</summary>
public class AppSettings
{
    public string? PlaylistSource { get; set; }
    public string? XmltvSource { get; set; }
    public int RefreshMinutes { get; set; } = 60;
    public bool CrtEnabled { get; set; } = true;
}

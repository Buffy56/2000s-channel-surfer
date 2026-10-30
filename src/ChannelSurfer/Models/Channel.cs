namespace ChannelSurfer.Models;

/// <summary>A channel from the m3u playlist. Number is tvg-chno when present, else 1-based playlist order.</summary>
public record Channel(string? TvgId, int Number, string Name, string? LogoUrl, string? Group, string StreamUrl);

namespace ChannelSurfer.Models;

/// <summary>A channel from the m3u playlist. Number is tvg-chno when present, else 1-based playlist order.</summary>
public record Channel(string? TvgId, int Number, string Name, string? LogoUrl, string? Group, string StreamUrl)
{
    /// <summary>True when the stream is raw MPEG-TS (.ts) rather than HLS — not playable on iOS Safari.</summary>
    public bool IsMpegTs => StreamUrl.Split('?')[0].EndsWith(".ts", StringComparison.OrdinalIgnoreCase);
}

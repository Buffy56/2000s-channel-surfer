using System.Text.RegularExpressions;
using ChannelSurfer.Models;

namespace ChannelSurfer.Parsing;

/// <summary>Parses #EXTM3U playlists into channels.</summary>
public static partial class PlaylistParser
{
    [GeneratedRegex("([\\w-]+)=\"([^\"]*)\"")]
    private static partial Regex AttrRegex();

    public static IReadOnlyList<Channel> Parse(string content)
    {
        var channels = new List<Channel>();
        string? extinf = null;

        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                extinf = line;                 // newest wins; a dangling EXTINF is simply overwritten
                continue;
            }
            if (line.StartsWith('#')) continue; // #EXTM3U and any other directive

            if (extinf is null) continue;       // URL with no preceding #EXTINF
            channels.Add(Build(extinf, line, channels.Count + 1));
            extinf = null;
        }

        return channels;
    }

    private static Channel Build(string extinf, string url, int order)
    {
        var attrs = AttrRegex().Matches(extinf)
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value, StringComparer.OrdinalIgnoreCase);

        attrs.TryGetValue("tvg-id", out var tvgId);
        attrs.TryGetValue("tvg-logo", out var logo);
        attrs.TryGetValue("group-title", out var group);
        var number = attrs.TryGetValue("tvg-chno", out var chno) && int.TryParse(chno, out var n) ? n : order;

        return new Channel(
            Nullify(tvgId), number, ExtractName(extinf),
            Nullify(logo), Nullify(group), url);
    }

    /// <summary>Display name is the text after the first comma that is outside quotes.</summary>
    private static string ExtractName(string extinf)
    {
        var inQuote = false;
        for (var i = 0; i < extinf.Length; i++)
        {
            if (extinf[i] == '"') inQuote = !inQuote;
            else if (extinf[i] == ',' && !inQuote) return extinf[(i + 1)..].Trim();
        }
        return "";
    }

    private static string? Nullify(string? s) => string.IsNullOrEmpty(s) ? null : s;
}

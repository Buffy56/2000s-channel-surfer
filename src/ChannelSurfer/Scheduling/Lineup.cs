using ChannelSurfer.Models;

namespace ChannelSurfer.Scheduling;

/// <summary>
/// The ordered channel list joined to its EPG. Programmes link by tvg-id, falling back to
/// a normalized-name match against the XMLTV display-names.
/// </summary>
public sealed class Lineup
{
    private readonly Dictionary<string, List<Programme>> _byChannelKey;

    public IReadOnlyList<Channel> Channels { get; }

    private Lineup(IReadOnlyList<Channel> channels, Dictionary<string, List<Programme>> byChannelKey)
    {
        Channels = channels;
        _byChannelKey = byChannelKey;
    }

    public static Lineup Build(
        IReadOnlyList<Channel> channels,
        IReadOnlyList<XmltvChannel> epgChannels,
        IReadOnlyList<Programme> programmes)
    {
        var progsByEpgId = programmes
            .GroupBy(p => p.ChannelId)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Start).ToList());

        var epgIdByName = new Dictionary<string, string>();
        foreach (var ec in epgChannels)
        {
            var norm = Normalize(ec.DisplayName);
            if (norm.Length > 0) epgIdByName[norm] = ec.Id;
        }

        var ordered = channels.OrderBy(c => c.Number).ToList();
        var byChannelKey = new Dictionary<string, List<Programme>>();
        foreach (var c in ordered)
        {
            List<Programme>? progs = null;
            if (c.TvgId is not null)
                progsByEpgId.TryGetValue(c.TvgId, out progs);
            if (progs is null && epgIdByName.TryGetValue(Normalize(c.Name), out var id))
                progsByEpgId.TryGetValue(id, out progs);
            byChannelKey[Key(c)] = progs ?? [];
        }

        return new Lineup(ordered, byChannelKey);
    }

    public IReadOnlyList<Programme> Programmes(Channel channel)
        => _byChannelKey.TryGetValue(Key(channel), out var p) ? p : [];

    /// <summary>The programme airing at <paramref name="at"/> and the one after it. Now is null in a gap.</summary>
    public (Programme? Now, Programme? Next) NowNext(Channel channel, DateTimeOffset at)
    {
        var progs = Programmes(channel);
        for (var i = 0; i < progs.Count; i++)
        {
            var p = progs[i];
            if (p.Start <= at && at < p.Stop)
                return (p, i + 1 < progs.Count ? progs[i + 1] : null);
            if (p.Start > at)
                return (null, p); // in a gap before this programme (list is sorted by Start)
        }
        return (null, null);
    }

    public static double Progress(Programme now, DateTimeOffset at)
    {
        var total = (now.Stop - now.Start).TotalSeconds;
        if (total <= 0) return 0;
        return Math.Clamp((at - now.Start).TotalSeconds / total, 0, 1);
    }

    private static string Key(Channel c) => c.TvgId ?? $"#{c.Number}/{c.Name}";

    private static string Normalize(string s)
        => new(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}

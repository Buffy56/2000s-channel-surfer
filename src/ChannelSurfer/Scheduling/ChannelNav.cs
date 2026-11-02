using ChannelSurfer.Models;

namespace ChannelSurfer.Scheduling;

public static class ChannelNav
{
    /// <summary>
    /// The index to land on after flipping <paramref name="delta"/> from <paramref name="current"/>.
    /// When <paramref name="restrictToHls"/> (iOS), skips MPEG-TS channels it can't play; returns
    /// <paramref name="current"/> unchanged if nothing is playable.
    /// </summary>
    public static int NextPlayableIndex(IReadOnlyList<Channel> channels, int current, int delta, bool restrictToHls)
    {
        var n = channels.Count;
        if (n == 0) return current;
        if (!restrictToHls) return (current + delta + n) % n;

        var idx = current;
        for (var step = 0; step < n; step++)
        {
            idx = (idx + delta + n) % n;
            if (!channels[idx].IsMpegTs) return idx;
        }
        return current; // nothing playable
    }
}

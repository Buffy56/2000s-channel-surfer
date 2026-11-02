using ChannelSurfer.Models;
using ChannelSurfer.Scheduling;
using Xunit;

namespace ChannelSurfer.Tests;

public class ChannelNavTests
{
    private static Channel Ch(int number, string streamUrl)
        => new(null, number, "C" + number, null, null, streamUrl);

    [Fact]
    public void IsMpegTs_TrueForTs_FalseForM3u8()
    {
        Assert.True(Ch(1, "http://h/ch/1.ts").IsMpegTs);
        Assert.False(Ch(2, "http://h/ch/2.m3u8").IsMpegTs);
        Assert.True(Ch(3, "http://h/ch/3.ts?mode=x").IsMpegTs); // query ignored
    }

    [Fact]
    public void NextPlayableIndex_NoRestriction_StepsNormally()
    {
        var chs = new[] { Ch(1, "a.ts"), Ch(2, "b.ts"), Ch(3, "c.ts") };
        Assert.Equal(1, ChannelNav.NextPlayableIndex(chs, 0, +1, restrictToHls: false));
        Assert.Equal(2, ChannelNav.NextPlayableIndex(chs, 0, -1, restrictToHls: false)); // wraps
    }

    [Fact]
    public void NextPlayableIndex_SkipsMpegTs_WhenRestricted()
    {
        var chs = new[] { Ch(1, "a.m3u8"), Ch(2, "b.ts"), Ch(3, "c.m3u8") };
        Assert.Equal(2, ChannelNav.NextPlayableIndex(chs, 0, +1, restrictToHls: true)); // skip ts(1)
        Assert.Equal(0, ChannelNav.NextPlayableIndex(chs, 2, +1, restrictToHls: true)); // wrap to hls(0)
    }

    [Fact]
    public void NextPlayableIndex_ReturnsCurrent_WhenNonePlayable()
    {
        var chs = new[] { Ch(1, "a.ts"), Ch(2, "b.ts") };
        Assert.Equal(0, ChannelNav.NextPlayableIndex(chs, 0, +1, restrictToHls: true));
    }
}

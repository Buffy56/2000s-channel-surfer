using System;
using System.Linq;
using ChannelSurfer.Models;
using ChannelSurfer.Scheduling;
using Xunit;

namespace ChannelSurfer.Tests;

public class LineupTests
{
    private static Channel Ch(int number, string? tvgId = null, string name = "C")
        => new(tvgId, number, name, null, null, $"http://s/{number}");

    private static Programme Prog(string channelId, string title, DateTimeOffset start, DateTimeOffset stop)
        => new(channelId, title, null, start, stop, null);

    private static readonly DateTimeOffset T20 = new(2026, 10, 30, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Build_OrdersByNumber()
    {
        var lineup = Lineup.Build(new[] { Ch(204, "a"), Ch(202, "b") },
            Array.Empty<XmltvChannel>(), Array.Empty<Programme>());
        Assert.Equal(new[] { 202, 204 }, lineup.Channels.Select(c => c.Number));
    }

    [Fact]
    public void NowNext_PicksCurrentAndFollowing()
    {
        var c = Ch(1, "c");
        var progs = new[]
        {
            Prog("c", "First", T20, T20.AddMinutes(30)),
            Prog("c", "Second", T20.AddMinutes(30), T20.AddHours(1)),
        };
        var lineup = Lineup.Build(new[] { c }, Array.Empty<XmltvChannel>(), progs);
        var (now, next) = lineup.NowNext(c, T20.AddMinutes(15));
        Assert.Equal("First", now?.Title);
        Assert.Equal("Second", next?.Title);
    }

    [Fact]
    public void NowNext_NullWhenChannelHasNoProgrammes()
    {
        var c = Ch(1, "c");
        var lineup = Lineup.Build(new[] { c }, Array.Empty<XmltvChannel>(), Array.Empty<Programme>());
        var (now, next) = lineup.NowNext(c, T20);
        Assert.Null(now);
        Assert.Null(next);
    }

    [Fact]
    public void Build_MatchesByNameWhenNoTvgId()
    {
        var c = Ch(1, tvgId: null, name: "ESPN HD");
        var epg = new[] { new XmltvChannel("espn.us", "ESPN HD") };
        var progs = new[] { Prog("espn.us", "SportsCenter", T20, T20.AddMinutes(30)) };
        var lineup = Lineup.Build(new[] { c }, epg, progs);
        var (now, _) = lineup.NowNext(c, T20.AddMinutes(5));
        Assert.Equal("SportsCenter", now?.Title);
    }

    [Fact]
    public void Progress_IsHalfwayAtMidpoint()
    {
        var p = Prog("c", "X", T20, T20.AddHours(1));
        Assert.Equal(0.5, Lineup.Progress(p, T20.AddMinutes(30)), 3);
    }
}

using System;
using System.Linq;
using ChannelSurfer.Models;
using ChannelSurfer.Scheduling;
using Xunit;

namespace ChannelSurfer.Tests;

public class GuideModelTests
{
    private static readonly DateTimeOffset T20 = new(2026, 10, 30, 20, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Half = TimeSpan.FromMinutes(30);

    private static Channel Ch(int n, string id) => new(id, n, "C" + n, null, null, "http://s");
    private static Programme Prog(string cid, string title, DateTimeOffset s, DateTimeOffset e)
        => new(cid, title, null, s, e, null);

    [Fact]
    public void Build_PlacesProgrammeAcrossSpannedSlots()
    {
        var c = Ch(1, "c");
        var lineup = Lineup.Build(new[] { c }, Array.Empty<XmltvChannel>(),
            new[] { Prog("c", "Movie", T20, T20.AddHours(1)) });
        var g = GuideModel.Build(lineup, T20, 2, Half);
        var cell = g.Rows.Single().Cells.First();
        Assert.Equal(0, cell.StartSlotIndex);
        Assert.Equal(2, cell.SpanSlots);
        Assert.Equal("Movie", cell.Title);
    }

    [Fact]
    public void Build_FillsGapWithNoInfo()
    {
        var c = Ch(1, "c");
        var lineup = Lineup.Build(new[] { c }, Array.Empty<XmltvChannel>(), Array.Empty<Programme>());
        var g = GuideModel.Build(lineup, T20, 3, Half);
        Assert.All(g.Rows.Single().Cells, cell => Assert.Equal("No info", cell.Title));
    }

    [Fact]
    public void Build_EmitsSlotCountHeaders()
    {
        var lineup = Lineup.Build(Array.Empty<Channel>(), Array.Empty<XmltvChannel>(), Array.Empty<Programme>());
        var g = GuideModel.Build(lineup, T20, 4, Half);
        Assert.Equal(4, g.Headers.Count);
    }
}

using System.Linq;
using ChannelSurfer.Models;
using ChannelSurfer.Parsing;
using Xunit;

namespace ChannelSurfer.Tests;

public class PlaylistParserTests
{
    [Fact]
    public void Parse_ExtractsAttributes()
    {
        var p = PlaylistParser.Parse(
            "#EXTM3U\n#EXTINF:-1 tvg-id=\"espn.us\" tvg-chno=\"204\" tvg-logo=\"http://x/e.png\" group-title=\"Sports\",ESPN HD\nhttp://host/espn.m3u8\n");
        var c = Assert.Single(p);
        Assert.Equal("espn.us", c.TvgId);
        Assert.Equal(204, c.Number);
        Assert.Equal("ESPN HD", c.Name);
        Assert.Equal("http://x/e.png", c.LogoUrl);
        Assert.Equal("Sports", c.Group);
        Assert.Equal("http://host/espn.m3u8", c.StreamUrl);
    }

    [Fact]
    public void Parse_NumbersByOrder_WhenNoChno()
    {
        var p = PlaylistParser.Parse("#EXTM3U\n#EXTINF:-1,A\nhttp://a\n#EXTINF:-1,B\nhttp://b\n");
        Assert.Equal(new[] { 1, 2 }, p.Select(c => c.Number));
    }

    [Fact]
    public void Parse_SkipsMalformed()
    {
        var p = PlaylistParser.Parse("#EXTM3U\n\n#EXTINF:-1,Dangling\n#EXTINF:-1,Good\nhttp://good\n");
        Assert.Equal("Good", Assert.Single(p).Name);
    }
}

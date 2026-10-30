using System;
using ChannelSurfer.Parsing;
using Xunit;

namespace ChannelSurfer.Tests;

public class XmltvParserTests
{
    [Fact]
    public void Parse_ReadsChannelsAndProgrammes()
    {
        var xml = "<tv><channel id=\"espn.us\"><display-name>ESPN</display-name></channel>"
            + "<programme start=\"20261030200000 +0000\" stop=\"20261030203000 +0000\" channel=\"espn.us\">"
            + "<title>SportsCenter</title><desc>Highlights</desc><category>Sports</category></programme></tv>";
        var (chs, progs) = XmltvParser.Parse(xml);
        Assert.Equal("ESPN", Assert.Single(chs).DisplayName);
        var pr = Assert.Single(progs);
        Assert.Equal("SportsCenter", pr.Title);
        Assert.Equal("espn.us", pr.ChannelId);
        Assert.Equal(new DateTimeOffset(2026, 10, 30, 20, 0, 0, TimeSpan.Zero), pr.Start);
    }

    [Fact]
    public void Parse_SkipsProgrammeWithUnparseableTime()
    {
        var xml = "<tv>"
            + "<programme start=\"20261030\" stop=\"20261030\" channel=\"c\"><title>Bad</title></programme>"
            + "<programme start=\"20261030200000 +0000\" stop=\"20261030203000 +0000\" channel=\"c\"><title>Good</title></programme>"
            + "</tv>";
        var (_, progs) = XmltvParser.Parse(xml);
        Assert.Equal("Good", Assert.Single(progs).Title);
    }

    [Fact]
    public void Parse_HonorsTimezoneOffset()
    {
        var xml = "<tv><programme start=\"20261030200000 +0100\" stop=\"20261030210000 +0100\" channel=\"c\">"
            + "<title>T</title></programme></tv>";
        var (_, progs) = XmltvParser.Parse(xml);
        Assert.Equal(new DateTimeOffset(2026, 10, 30, 19, 0, 0, TimeSpan.Zero),
            Assert.Single(progs).Start.ToUniversalTime());
    }
}

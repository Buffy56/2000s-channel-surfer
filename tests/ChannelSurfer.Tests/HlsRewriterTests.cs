using System;
using ChannelSurfer.Proxy;
using Xunit;

namespace ChannelSurfer.Tests;

public class HlsRewriterTests
{
    [Fact]
    public void Rewrite_ResolvesRelativeAndAbsoluteUris()
    {
        var pl = "#EXTM3U\n#EXTINF:6,\nseg1.ts\n#EXTINF:6,\nhttp://cdn/seg2.ts\n";
        var outp = HlsRewriter.Rewrite(pl, new Uri("http://host/live/x.m3u8"),
            u => "/proxy/seg?u=" + Uri.EscapeDataString(u.ToString()));
        Assert.Contains("/proxy/seg?u=" + Uri.EscapeDataString("http://host/live/seg1.ts"), outp);
        Assert.Contains("/proxy/seg?u=" + Uri.EscapeDataString("http://cdn/seg2.ts"), outp);
        Assert.StartsWith("#EXTM3U", outp); // directive lines untouched
    }

    [Fact]
    public void Rewrite_RewritesVariantSubPlaylists()
    {
        var pl = "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=1\nvariant.m3u8\n";
        var outp = HlsRewriter.Rewrite(pl, new Uri("http://host/master.m3u8"),
            u => "/p?u=" + Uri.EscapeDataString(u.ToString()));
        Assert.Contains("/p?u=" + Uri.EscapeDataString("http://host/variant.m3u8"), outp);
    }
}

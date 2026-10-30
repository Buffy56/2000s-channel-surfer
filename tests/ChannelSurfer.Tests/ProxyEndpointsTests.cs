using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChannelSurfer.Tests;

public class ProxyEndpointsTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(respond(request));
    }

    private static WebApplicationFactory<Program> Factory(Func<HttpRequestMessage, HttpResponseMessage> stub)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddSingleton(new HttpClient(new StubHandler(stub)))));

    [Fact]
    public async Task Stream_RewritesPlaylistThroughProxy()
    {
        using var factory = Factory(_ =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("#EXTM3U\n#EXTINF:6,\nseg1.ts\n"),
            };
            resp.Content.Headers.ContentType = new("application/vnd.apple.mpegurl");
            return resp;
        });
        var client = factory.CreateClient();

        var body = await client.GetStringAsync("/proxy/stream?u=" + Uri.EscapeDataString("http://host/live/x.m3u8"));

        Assert.Contains("/proxy/stream?u=" + Uri.EscapeDataString("http://host/live/seg1.ts"), body);
    }

    [Fact]
    public async Task Stream_RejectsNonHttpUrl()
    {
        using var factory = Factory(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = factory.CreateClient();

        var resp = await client.GetAsync("/proxy/stream?u=" + Uri.EscapeDataString("file:///etc/passwd"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Stream_UsesFinalUrlAfterRedirectAsBase()
    {
        using var factory = Factory(_ =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("#EXTM3U\n#EXTINF:6,\nseg1.ts\n"),
            };
            resp.Content.Headers.ContentType = new("application/vnd.apple.mpegurl");
            resp.RequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://cdn7/abc/x.m3u8");
            return resp;
        });
        var client = factory.CreateClient();

        var body = await client.GetStringAsync("/proxy/stream?u=" + Uri.EscapeDataString("http://host/live/x.m3u8"));

        // Relative seg1.ts must resolve against the REDIRECTED base, not the requested URL.
        Assert.Contains(Uri.EscapeDataString("http://cdn7/abc/seg1.ts"), body);
    }

    [Fact]
    public async Task Stream_BlocksLinkLocalMetadataIp()
    {
        using var factory = Factory(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = factory.CreateClient();

        var resp = await client.GetAsync("/proxy/stream?u=" + Uri.EscapeDataString("http://169.254.169.254/latest/meta-data/"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Stream_RejectsOversizedPlaylist()
    {
        // The size cap guards the buffered playlist branch; media streams through uncapped.
        using var factory = Factory(_ =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[1024]) };
            resp.Content.Headers.ContentType = new("application/vnd.apple.mpegurl");
            resp.Content.Headers.ContentLength = 200L * 1024 * 1024; // declares 200 MB
            return resp;
        });
        var client = factory.CreateClient();

        var resp = await client.GetAsync("/proxy/stream?u=" + Uri.EscapeDataString("http://host/big.m3u8"));

        Assert.Equal(HttpStatusCode.BadGateway, resp.StatusCode);
    }

    [Fact]
    public async Task Stream_PassesThroughMpegTsBody()
    {
        using var factory = Factory(_ =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 0x47, 0x01, 0x02, 0x03 }), // 0x47 = TS sync byte
            };
            resp.Content.Headers.ContentType = new("video/mp2t");
            return resp;
        });
        var client = factory.CreateClient();

        var r = await client.GetAsync("/proxy/stream?u=" + Uri.EscapeDataString("http://host/channel/2.ts"));

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("video/mp2t", r.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new byte[] { 0x47, 0x01, 0x02, 0x03 }, await r.Content.ReadAsByteArrayAsync());
    }
}

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
}

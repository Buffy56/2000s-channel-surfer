using System.Text;

namespace ChannelSurfer.Proxy;

/// <summary>Proxies HLS playlists/segments and channel logos so the browser never hits an upstream IPTV origin.</summary>
public static class ProxyEndpoints
{
    public static void MapProxyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/proxy/stream", StreamAsync);
        app.MapGet("/proxy/logo", LogoAsync);
    }

    private static async Task<IResult> StreamAsync(string u, HttpClient http, HttpContext ctx)
    {
        if (!TryAbsoluteHttp(u, out var uri)) return Results.BadRequest("invalid url");

        using var resp = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
        var contentType = resp.Content.Headers.ContentType?.MediaType ?? "";
        // ponytail: buffer each segment in memory (segments are small); stream it if memory ever matters.
        var bytes = await resp.Content.ReadAsByteArrayAsync(ctx.RequestAborted);

        if (IsPlaylist(contentType, bytes))
        {
            var rewritten = HlsRewriter.Rewrite(Encoding.UTF8.GetString(bytes), uri,
                seg => "/proxy/stream?u=" + Uri.EscapeDataString(seg.ToString()));
            return Results.Text(rewritten, "application/vnd.apple.mpegurl");
        }

        return Results.Bytes(bytes, string.IsNullOrEmpty(contentType) ? "application/octet-stream" : contentType);
    }

    private static async Task<IResult> LogoAsync(string u, HttpClient http, HttpContext ctx)
    {
        if (!TryAbsoluteHttp(u, out var uri)) return Results.BadRequest("invalid url");

        using var resp = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
        var bytes = await resp.Content.ReadAsByteArrayAsync(ctx.RequestAborted);
        return Results.Bytes(bytes, resp.Content.Headers.ContentType?.MediaType ?? "image/png");
    }

    private static bool IsPlaylist(string contentType, byte[] bytes)
        => contentType.Contains("mpegurl", StringComparison.OrdinalIgnoreCase)
        || (bytes.Length >= 7 && Encoding.ASCII.GetString(bytes, 0, 7) == "#EXTM3U");

    private static bool TryAbsoluteHttp(string u, out Uri uri)
        => Uri.TryCreate(u, UriKind.Absolute, out uri!)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

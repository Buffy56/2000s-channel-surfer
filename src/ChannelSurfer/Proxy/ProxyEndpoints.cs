using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ChannelSurfer.Proxy;

/// <summary>Proxies HLS playlists/segments and channel logos so the browser never hits an upstream IPTV origin.</summary>
public static class ProxyEndpoints
{
    private const long MaxBytes = 100L * 1024 * 1024; // cap per proxied response (segments are small)

    public static void MapProxyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/proxy/stream", StreamAsync);
        app.MapGet("/proxy/logo", LogoAsync);
    }

    private static async Task<IResult> StreamAsync(string u, HttpClient http, HttpContext ctx)
    {
        if (!TryAllowedHttp(u, out var uri)) return Results.BadRequest("invalid url");

        var resp = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
        try
        {
            // Redirects may land on a different host: validate against the FINAL url.
            var finalUri = resp.RequestMessage?.RequestUri ?? uri;
            if (!IsAllowedHost(finalUri)) return Results.BadRequest("blocked host");

            var contentType = resp.Content.Headers.ContentType?.MediaType ?? "";

            // An HLS playlist is small and must be rewritten so its segment URIs come back through us.
            if (IsPlaylist(contentType, finalUri))
            {
                if (DeclaresTooLarge(resp)) return Results.StatusCode(StatusCodes.Status502BadGateway);
                var bytes = await ReadCappedAsync(resp, ctx.RequestAborted);
                if (bytes is null) return Results.StatusCode(StatusCodes.Status502BadGateway);
                var rewritten = HlsRewriter.Rewrite(Encoding.UTF8.GetString(bytes), finalUri,
                    seg => "/proxy/stream?u=" + Uri.EscapeDataString(seg.ToString()));
                return Results.Text(rewritten, "application/vnd.apple.mpegurl");
            }

            // Media — HLS segments and live MPEG-TS (ErsatzTV) — streams straight through,
            // never buffered: a live .ts never ends.
            ctx.Response.ContentType = string.IsNullOrEmpty(contentType) ? "application/octet-stream" : contentType;
            await using var stream = await resp.Content.ReadAsStreamAsync(ctx.RequestAborted);
            try
            {
                await stream.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                // Client flipped the channel or navigated away — expected.
            }
            return Results.Empty;
        }
        finally
        {
            resp.Dispose();
        }
    }

    private static async Task<IResult> LogoAsync(string u, HttpClient http, HttpContext ctx)
    {
        if (!TryAllowedHttp(u, out var uri)) return Results.BadRequest("invalid url");

        using var resp = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
        if (!IsAllowedHost(resp.RequestMessage?.RequestUri ?? uri)) return Results.BadRequest("blocked host");
        if (DeclaresTooLarge(resp)) return Results.StatusCode(StatusCodes.Status502BadGateway);

        var bytes = await ReadCappedAsync(resp, ctx.RequestAborted);
        if (bytes is null) return Results.StatusCode(StatusCodes.Status502BadGateway);
        return Results.Bytes(bytes, resp.Content.Headers.ContentType?.MediaType ?? "image/png");
    }

    private static bool IsPlaylist(string contentType, Uri uri)
        => contentType.Contains("mpegurl", StringComparison.OrdinalIgnoreCase)
        || uri.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase);

    private static bool TryAllowedHttp(string u, out Uri uri)
        => Uri.TryCreate(u, UriKind.Absolute, out uri!)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && IsAllowedHost(uri);

    // Block loopback and link-local literals (e.g. cloud metadata 169.254.169.254). Private LAN ranges
    // stay allowed on purpose — home IPTV tuners/restreamers legitimately live on 10/172.16/192.168.
    private static bool IsAllowedHost(Uri uri)
    {
        if (IPAddress.TryParse(uri.Host, out var ip))
            return !(IPAddress.IsLoopback(ip) || IsLinkLocal(ip));
        return true; // hostnames allowed; DNS-name SSRF is out of scope (see ledger)
    }

    private static bool IsLinkLocal(IPAddress ip)
    {
        if (ip.IsIPv6LinkLocal) return true;
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 169 && b[1] == 254; // 169.254.0.0/16
        }
        return false;
    }

    private static bool DeclaresTooLarge(HttpResponseMessage resp)
        => resp.Content.Headers.ContentLength is long len && len > MaxBytes;

    private static async Task<byte[]?> ReadCappedAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxBytes) return null; // undeclared oversize
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }
}

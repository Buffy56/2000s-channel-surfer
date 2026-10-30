using System.Text;
using System.Text.RegularExpressions;

namespace ChannelSurfer.Proxy;

/// <summary>Rewrites segment and sub-playlist URIs in an HLS playlist to point back through the proxy.</summary>
public static partial class HlsRewriter
{
    [GeneratedRegex("URI=\"([^\"]*)\"")]
    private static partial Regex UriAttr();

    public static string Rewrite(string playlist, Uri baseUri, Func<Uri, string> toProxyPath)
    {
        var sb = new StringBuilder();
        foreach (var raw in playlist.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                sb.Append('\n');
                continue;
            }

            if (line.StartsWith('#'))
            {
                // Tags like #EXT-X-MEDIA / #EXT-X-KEY carry a URI="..." attribute; rewrite that.
                sb.Append(UriAttr().Replace(line,
                    m => $"URI=\"{toProxyPath(new Uri(baseUri, m.Groups[1].Value))}\""));
            }
            else
            {
                // A bare resource line: a segment or a sub-playlist.
                sb.Append(toProxyPath(new Uri(baseUri, line)));
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }
}

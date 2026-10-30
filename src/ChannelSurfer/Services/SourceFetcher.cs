using System.IO.Compression;
using System.Text;

namespace ChannelSurfer.Services;

/// <summary>Fetches a source's text from a local file path or an http(s) URL, decompressing gzip transparently.</summary>
public class SourceFetcher(HttpClient http)
{
    public async Task<string> FetchTextAsync(string source, bool gunzip)
    {
        var bytes = IsLocalPath(source)
            ? await File.ReadAllBytesAsync(source)
            : await http.GetByteArrayAsync(source);

        // XMLTV is usually gzipped; the gzip magic number is more reliable than the caller's hint.
        _ = gunzip;
        if (bytes.Length >= 2 && bytes[0] == 0x1f && bytes[1] == 0x8b)
        {
            using var input = new MemoryStream(bytes);
            using var gz = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            await gz.CopyToAsync(output);
            bytes = output.ToArray();
        }

        // Strip a leading UTF-8 BOM — it survives GetString and breaks XDocument.Parse.
        var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        return Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
    }

    private static bool IsLocalPath(string s)
        => !s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        && !s.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}

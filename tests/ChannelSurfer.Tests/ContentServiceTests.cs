using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ChannelSurfer.Models;
using ChannelSurfer.Services;
using Xunit;

namespace ChannelSurfer.Tests;

public class ContentServiceTests
{
    private static string WriteTemp(string content, string ext = ".txt")
    {
        var f = Path.Combine(Path.GetTempPath(), "cs-" + Guid.NewGuid().ToString("N") + ext);
        File.WriteAllText(f, content);
        return f;
    }

    private static string WriteTempGzip(string content)
    {
        var f = Path.Combine(Path.GetTempPath(), "cs-" + Guid.NewGuid().ToString("N") + ".gz");
        using (var fs = File.Create(f))
        using (var gz = new GZipStream(fs, CompressionMode.Compress))
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            gz.Write(bytes, 0, bytes.Length);
        }
        return f;
    }

    [Fact]
    public async Task Fetch_ReadsLocalFile()
    {
        var f = WriteTemp("#EXTM3U\n#EXTINF:-1,A\nhttp://a\n");
        var text = await new SourceFetcher(new HttpClient()).FetchTextAsync(f, gunzip: false);
        Assert.Contains("#EXTM3U", text);
    }

    [Fact]
    public async Task Fetch_StripsUtf8Bom()
    {
        var path = Path.Combine(Path.GetTempPath(), "cs-" + Guid.NewGuid().ToString("N") + ".xml");
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        File.WriteAllBytes(path, bom.Concat(Encoding.UTF8.GetBytes("<tv></tv>")).ToArray());

        var text = await new SourceFetcher(new HttpClient()).FetchTextAsync(path, gunzip: false);

        Assert.Equal("<tv></tv>", text); // ordinal: fails if a BOM char survived
    }

    [Fact]
    public async Task Fetch_GunzipsXmltv()
    {
        var f = WriteTempGzip("<tv></tv>");
        var text = await new SourceFetcher(new HttpClient()).FetchTextAsync(f, gunzip: true);
        Assert.Equal("<tv></tv>", text);
    }

    [Fact]
    public async Task Reload_BuildsLineupFromSources()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var m3u = WriteTemp("#EXTM3U\n#EXTINF:-1 tvg-id=\"c\",A\nhttp://a\n", ".m3u");
        var xml = WriteTemp("<tv><channel id=\"c\"><display-name>A</display-name></channel></tv>", ".xml");
        var settings = new SettingsService(dir);
        settings.Save(new AppSettings { PlaylistSource = m3u, XmltvSource = xml });

        var content = new ContentService(settings, new SourceFetcher(new HttpClient()));
        await content.ReloadAsync();

        Assert.NotEmpty(content.Current.Channels);
    }
}

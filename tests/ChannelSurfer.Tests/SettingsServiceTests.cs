using System;
using System.IO;
using ChannelSurfer.Models;
using ChannelSurfer.Services;
using Xunit;

namespace ChannelSurfer.Tests;

public class SettingsServiceTests
{
    private static string TempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "cs-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public void Get_ReturnsDefaults_WhenFileMissing()
    {
        var s = new SettingsService(TempDir()).Get();
        Assert.Equal(60, s.RefreshMinutes);
        Assert.True(s.CrtEnabled);
    }

    [Fact]
    public void Get_ReturnsDefaults_WhenFileCorrupt()
    {
        var dir = TempDir();
        File.WriteAllText(Path.Combine(dir, "settings.json"), "{ not json");
        Assert.NotNull(new SettingsService(dir).Get());
    }

    [Fact]
    public void Save_Then_Get_RoundTrips()
    {
        var dir = TempDir();
        new SettingsService(dir).Save(new AppSettings
        {
            PlaylistSource = "http://x/p.m3u",
            XmltvSource = "/config/epg.xml",
            CrtEnabled = false,
        });
        var s = new SettingsService(dir).Get();
        Assert.Equal("http://x/p.m3u", s.PlaylistSource);
        Assert.False(s.CrtEnabled);
    }
}

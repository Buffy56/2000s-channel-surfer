using ChannelSurfer.Models;
using ChannelSurfer.Parsing;
using ChannelSurfer.Scheduling;

namespace ChannelSurfer.Services;

/// <summary>Holds the current parsed lineup; rebuilds it from the configured sources on demand.</summary>
public class ContentService(SettingsService settings, SourceFetcher fetcher)
{
    public Lineup Current { get; private set; } =
        Lineup.Build(Array.Empty<Channel>(), Array.Empty<XmltvChannel>(), Array.Empty<Programme>());

    public DateTimeOffset LastLoaded { get; private set; }
    public string? LastError { get; private set; }

    public async Task ReloadAsync()
    {
        try
        {
            var s = settings.Get();

            IReadOnlyList<Channel> channels = string.IsNullOrWhiteSpace(s.PlaylistSource)
                ? Array.Empty<Channel>()
                : PlaylistParser.Parse(await fetcher.FetchTextAsync(s.PlaylistSource, gunzip: false));

            IReadOnlyList<XmltvChannel> epgChannels = Array.Empty<XmltvChannel>();
            IReadOnlyList<Programme> programmes = Array.Empty<Programme>();
            if (!string.IsNullOrWhiteSpace(s.XmltvSource))
                (epgChannels, programmes) = XmltvParser.Parse(await fetcher.FetchTextAsync(s.XmltvSource, gunzip: true));

            Current = Lineup.Build(channels, epgChannels, programmes);
            LastLoaded = DateTimeOffset.Now;
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message; // keep the previous lineup on failure
        }
    }
}

/// <summary>Reloads content at startup and then every RefreshMinutes.</summary>
public class ContentRefresher(ContentService content, SettingsService settings) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await content.ReloadAsync();
        while (!stoppingToken.IsCancellationRequested)
        {
            var minutes = Math.Max(1, settings.Get().RefreshMinutes);
            try { await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken); }
            catch (TaskCanceledException) { break; }
            await content.ReloadAsync();
        }
    }
}

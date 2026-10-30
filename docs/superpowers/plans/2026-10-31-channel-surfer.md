# 2000s Channel Surfer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A containerized ASP.NET Core + Blazor Server IPTV client with a CRT-styled channel-surf zapper and a blue Prevue-style grid guide, driven by an m3u playlist and an XMLTV schedule.

**Architecture:** One ASP.NET Core project. Pure parsing/scheduling logic (m3u, XMLTV, now/next, lineup, HLS-rewrite) sits in plain C# classes with full unit tests. Services fetch + cache those sources from a file path or URL and persist settings to one JSON file on a mounted volume. Minimal-API endpoints proxy HLS playlists/segments and logos to dodge CORS. Blazor components render the surf view (with `hls.js` interop) and the guide; a CSS overlay supplies the CRT coat.

**Tech Stack:** .NET 10, ASP.NET Core, Blazor Server (Interactive Server), xUnit, `hls.js`, Docker.

**Spec:** `docs/superpowers/specs/2026-10-30-channel-surfer-design.md`

## Global Constraints

- **.NET 10** (current LTS); single project `ChannelSurfer`, single container.
- **Blazor Server** (Interactive Server render mode), C# end to end.
- **No database.** Only persistence is `settings.json` in the config directory (env `CONFIG_DIR`, default `/config`).
- **Single user, no auth.**
- **All streams and logos are proxied** through the backend; the browser never fetches an upstream IPTV URL directly.
- m3u and XMLTV sources may each be a **local file path or a remote URL**.
- **Keyboard is the input contract** (media remote emits keys); mouse optional in the guide.
- Playback via **`hls.js`**, falling back to native `<video>` when the browser plays HLS natively.
- Work happens on a feature branch. The directory is **not yet a git repo and has no remote**, so Task 1 runs `git init` and branches locally (nothing to pull).
- Unit tests are required at minimum; parsing and scheduling logic carry real tests, UI tasks verify by running the app.

## Review Focus

Inputs the spec implies but that a naive task set would not exercise — each has a test pinned to the owning task below:

- **m3u channel with no `tvg-id`** → matched to EPG by normalized name, not dropped. (Task 4)
- **Gzipped XMLTV** (the common case) → transparently decompressed before parse. (Task 7)
- **Missing or corrupt `settings.json`** on first run → fall back to defaults, never crash. (Task 6)
- **Channel present in m3u but absent from XMLTV** → now/next is null and the guide shows a "No info" cell instead of blank/crash. (Task 4 logic, Task 10 grid cell)
- **XMLTV timestamps with a timezone offset** (`20261030200000 +0100`) → parsed to the correct instant. (Task 3)

Additionally, a **stream the browser cannot play** must surface an on-screen error in the surf view rather than a silent black screen — verified manually in Task 9 (browser-only behavior).

---

### Task 1: Project skeleton + repo

**Files:**
- Create: `.gitignore`, `ChannelSurfer.sln`, `src/ChannelSurfer/ChannelSurfer.csproj`, `src/ChannelSurfer/Program.cs` (from template), `tests/ChannelSurfer.Tests/ChannelSurfer.Tests.csproj`

- [ ] **Step 1: Init repo and branch**

```bash
git init
printf '%s\n' 'bin/' 'obj/' '.superpowers/' '.vs/' 'appsettings.*.local.json' > .gitignore
git add .gitignore && git commit -m "chore: init repo"
git switch -c feat/channel-surfer
```

- [ ] **Step 2: Scaffold projects**

Run: `dotnet new blazor -n ChannelSurfer -o src/ChannelSurfer --interactivity Server --empty` (if `--empty` unsupported on the installed SDK, use the default template). Then `dotnet new xunit -n ChannelSurfer.Tests -o tests/ChannelSurfer.Tests`, `dotnet new sln -n ChannelSurfer`, and add both projects to the solution. Add a project reference from the test project to `ChannelSurfer`.

- [ ] **Step 3: Verify build and test run**

Run: `dotnet build` then `dotnet test`
Expected: build succeeds; test run succeeds with 0 tests (or the template's sample).

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "chore: scaffold Blazor Server project and test project"
```

---

### Task 2: m3u parser

**Files:**
- Create: `src/ChannelSurfer/Models/Channel.cs`, `src/ChannelSurfer/Parsing/PlaylistParser.cs`
- Test: `tests/ChannelSurfer.Tests/PlaylistParserTests.cs`

**Interfaces:**
- Produces: `record Channel(string? TvgId, int Number, string Name, string? LogoUrl, string? Group, string StreamUrl)`; `static IReadOnlyList<Channel> PlaylistParser.Parse(string content)`.

- [ ] **Step 1: Write the failing tests**

```csharp
// Parses attributes and the URL line following #EXTINF
[Fact] void Parse_ExtractsAttributes() {
  var p = PlaylistParser.Parse("#EXTM3U\n#EXTINF:-1 tvg-id=\"espn.us\" tvg-chno=\"204\" tvg-logo=\"http://x/e.png\" group-title=\"Sports\",ESPN HD\nhttp://host/espn.m3u8\n");
  var c = Assert.Single(p);
  Assert.Equal("espn.us", c.TvgId); Assert.Equal(204, c.Number);
  Assert.Equal("ESPN HD", c.Name); Assert.Equal("http://x/e.png", c.LogoUrl);
  Assert.Equal("Sports", c.Group); Assert.Equal("http://host/espn.m3u8", c.StreamUrl);
}
// Number falls back to 1-based playlist order when tvg-chno absent
[Fact] void Parse_NumbersByOrder_WhenNoChno() {
  var p = PlaylistParser.Parse("#EXTM3U\n#EXTINF:-1,A\nhttp://a\n#EXTINF:-1,B\nhttp://b\n");
  Assert.Equal(new[]{1,2}, p.Select(c => c.Number));
}
// Blank lines, comments and an #EXTINF with no following URL are skipped
[Fact] void Parse_SkipsMalformed() {
  var p = PlaylistParser.Parse("#EXTM3U\n\n#EXTINF:-1,Dangling\n#EXTINF:-1,Good\nhttp://good\n");
  Assert.Equal("Good", Assert.Single(p).Name);
}
```

- [ ] **Step 2: Run tests, verify they fail** — Run: `dotnet test --filter PlaylistParserTests`; Expected: FAIL (type/method missing).

- [ ] **Step 3: Implement `PlaylistParser.Parse`** in `Parsing/PlaylistParser.cs` and the `Channel` record. Parse line-by-line: on `#EXTINF:` read the attribute pairs (regex over `key="value"`) and the display name after the last comma, then attach the next non-comment line as `StreamUrl`. `Number` = parsed `tvg-chno` or running 1-based index. Skip an `#EXTINF` whose next content line is another directive.

- [ ] **Step 4: Run tests, verify pass** — Run: `dotnet test --filter PlaylistParserTests`; Expected: PASS.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: m3u playlist parser"`

---

### Task 3: XMLTV parser

**Files:**
- Create: `src/ChannelSurfer/Models/Programme.cs`, `src/ChannelSurfer/Parsing/XmltvParser.cs`
- Test: `tests/ChannelSurfer.Tests/XmltvParserTests.cs`

**Interfaces:**
- Produces: `record Programme(string ChannelId, string Title, string? Desc, DateTimeOffset Start, DateTimeOffset Stop, string? Category)`; `record XmltvChannel(string Id, string DisplayName)`; `static (IReadOnlyList<XmltvChannel> Channels, IReadOnlyList<Programme> Programmes) XmltvParser.Parse(string xml)`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] void Parse_ReadsChannelsAndProgrammes() {
  var xml = "<tv><channel id=\"espn.us\"><display-name>ESPN</display-name></channel>"
    + "<programme start=\"20261030200000 +0000\" stop=\"20261030203000 +0000\" channel=\"espn.us\">"
    + "<title>SportsCenter</title><desc>Highlights</desc><category>Sports</category></programme></tv>";
  var (chs, progs) = XmltvParser.Parse(xml);
  Assert.Equal("ESPN", Assert.Single(chs).DisplayName);
  var pr = Assert.Single(progs);
  Assert.Equal("SportsCenter", pr.Title); Assert.Equal("espn.us", pr.ChannelId);
  Assert.Equal(new DateTimeOffset(2026,10,30,20,0,0,TimeSpan.Zero), pr.Start);
}
// Timestamp with a non-UTC offset parses to the correct instant (Review Focus)
[Fact] void Parse_HonorsTimezoneOffset() {
  var xml = "<tv><programme start=\"20261030200000 +0100\" stop=\"20261030210000 +0100\" channel=\"c\"><title>T</title></programme></tv>";
  var (_, progs) = XmltvParser.Parse(xml);
  Assert.Equal(new DateTimeOffset(2026,10,30,19,0,0,TimeSpan.Zero), Assert.Single(progs).Start.ToUniversalTime());
}
```

- [ ] **Step 2: Run tests, verify they fail** — Run: `dotnet test --filter XmltvParserTests`; Expected: FAIL.

- [ ] **Step 3: Implement `XmltvParser.Parse`** using `System.Xml.Linq`. Parse `channel`/`display-name` and `programme` elements. Parse the `start`/`stop` XMLTV datetime (`yyyyMMddHHmmss zzz`, offset may be present) with `DateTimeOffset.ParseExact` across the known formats. The parser takes XML text only; gzip is handled by the fetch layer (Task 7).

- [ ] **Step 4: Run tests, verify pass** — Expected: PASS.

- [ ] **Step 5: Commit** — `git commit -am "feat: XMLTV parser"`

---

### Task 4: Lineup — ordering, EPG matching, now/next

**Files:**
- Create: `src/ChannelSurfer/Scheduling/Lineup.cs`
- Test: `tests/ChannelSurfer.Tests/LineupTests.cs`

**Interfaces:**
- Consumes: `Channel` (Task 2), `Programme`/`XmltvChannel` (Task 3).
- Produces: `static Lineup Lineup.Build(IReadOnlyList<Channel> channels, IReadOnlyList<Programme> programmes)`; `IReadOnlyList<Channel> Lineup.Channels` (ordered by `Number`); `(Programme? Now, Programme? Next) Lineup.NowNext(Channel channel, DateTimeOffset at)`; `IReadOnlyList<Programme> Lineup.Programmes(Channel channel)`; `static double Lineup.Progress(Programme now, DateTimeOffset at)` (0..1). The UI only ever holds `Channel` objects, so these take a `Channel`; `Lineup` keeps the channel→programmes map internally. The builder links programmes to channels by `TvgId`, falling back to normalized-name (lowercased, trimmed, non-alphanumerics removed) equality against `Channel.Name`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] void Build_OrdersByNumber() { /* channels 204,202 -> Channels == [202,204] */ }
[Fact] void NowNext_PicksCurrentAndFollowing() {
  // two back-to-back programmes; at a time inside the first -> Now=first, Next=second
}
[Fact] void NowNext_NullWhenChannelHasNoProgrammes() {
  // NowNext(channelWithNoMatches, at) -> (null, null)   (Review Focus)
}
[Fact] void Build_MatchesByNameWhenNoTvgId() {
  // Channel(TvgId=null, Name="ESPN HD"); XmltvChannel display-name "ESPN HD" carries programmes
  // -> NowNext(thatChannel, at) resolves the programme   (Review Focus)
}
[Fact] void Progress_IsHalfwayAtMidpoint() {
  // 20:00-21:00 programme at 20:30 -> ~0.5
}
```

- [ ] **Step 2: Run tests, verify they fail** — Run: `dotnet test --filter LineupTests`; Expected: FAIL.

- [ ] **Step 3: Implement `Lineup`.** Build an ordered channel list and a per-channel-key sorted programme list. Matching: index programmes by `ChannelId`; for each channel resolve its key via `TvgId`, else match a programme-channel whose normalized display-name equals the channel's normalized name. `NowNext` binary/linear-scans the channel's sorted programmes for the one containing `at`. `Progress` = clamp((at−Start)/(Stop−Start), 0, 1).

- [ ] **Step 4: Run tests, verify pass** — Expected: PASS.

- [ ] **Step 5: Commit** — `git commit -am "feat: channel lineup, EPG matching, now/next"`

---

### Task 5: HLS playlist rewriter

**Files:**
- Create: `src/ChannelSurfer/Proxy/HlsRewriter.cs`
- Test: `tests/ChannelSurfer.Tests/HlsRewriterTests.cs`

**Interfaces:**
- Produces: `static string HlsRewriter.Rewrite(string playlist, Uri baseUri, Func<Uri,string> toProxyPath)`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] void Rewrite_ResolvesRelativeAndAbsoluteUris() {
  var pl = "#EXTM3U\n#EXTINF:6,\nseg1.ts\n#EXTINF:6,\nhttp://cdn/seg2.ts\n";
  var outp = HlsRewriter.Rewrite(pl, new Uri("http://host/live/x.m3u8"), u => "/proxy/seg?u=" + Uri.EscapeDataString(u.ToString()));
  Assert.Contains("/proxy/seg?u=" + Uri.EscapeDataString("http://host/live/seg1.ts"), outp);
  Assert.Contains("/proxy/seg?u=" + Uri.EscapeDataString("http://cdn/seg2.ts"), outp);
  Assert.StartsWith("#EXTM3U", outp); // directive lines untouched
}
[Fact] void Rewrite_RewritesVariantSubPlaylists() {
  // a master playlist line "variant.m3u8" is rewritten too
}
```

- [ ] **Step 2: Run tests, verify they fail** — Expected: FAIL.

- [ ] **Step 3: Implement `HlsRewriter.Rewrite`.** For each non-blank line that is not a `#` directive (and for `URI="..."` attributes inside `#EXT-X-` tags), resolve against `baseUri` with `new Uri(baseUri, line)` and replace with `toProxyPath(resolved)`. Leave all `#` directive lines otherwise intact.

- [ ] **Step 4: Run tests, verify pass** — Expected: PASS.

- [ ] **Step 5: Commit** — `git commit -am "feat: HLS playlist URI rewriter"`

---

### Task 6: SettingsService

**Files:**
- Create: `src/ChannelSurfer/Models/AppSettings.cs`, `src/ChannelSurfer/Services/SettingsService.cs`
- Test: `tests/ChannelSurfer.Tests/SettingsServiceTests.cs`

**Interfaces:**
- Produces: `class AppSettings { string? PlaylistSource; string? XmltvSource; int RefreshMinutes = 60; bool CrtEnabled = true; }`; `class SettingsService(string configDir)` with `AppSettings Get()` and `void Save(AppSettings s)`, persisting to `Path.Combine(configDir, "settings.json")`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] void Get_ReturnsDefaults_WhenFileMissing() {
  var s = new SettingsService(TempDir()).Get();
  Assert.Equal(60, s.RefreshMinutes); Assert.True(s.CrtEnabled);   // Review Focus
}
[Fact] void Get_ReturnsDefaults_WhenFileCorrupt() {
  var dir = TempDir(); File.WriteAllText(Path.Combine(dir,"settings.json"), "{ not json");
  Assert.NotNull(new SettingsService(dir).Get());                  // Review Focus: no throw
}
[Fact] void Save_Then_Get_RoundTrips() {
  var dir = TempDir(); var svc = new SettingsService(dir);
  svc.Save(new AppSettings{ PlaylistSource="http://x/p.m3u", XmltvSource="/config/epg.xml", CrtEnabled=false });
  var s = new SettingsService(dir).Get();
  Assert.Equal("http://x/p.m3u", s.PlaylistSource); Assert.False(s.CrtEnabled);
}
```

- [ ] **Step 2: Run tests, verify they fail** — Expected: FAIL.

- [ ] **Step 3: Implement `SettingsService`** with `System.Text.Json`. `Get` returns defaults when the file is absent or deserialization throws; `Save` writes indented JSON, creating `configDir` if needed.

- [ ] **Step 4: Run tests, verify pass** — Expected: PASS.

- [ ] **Step 5: Commit** — `git commit -am "feat: settings persistence"`

---

### Task 7: Content services — fetch, cache, refresh

**Files:**
- Create: `src/ChannelSurfer/Services/SourceFetcher.cs`, `src/ChannelSurfer/Services/ContentService.cs`
- Test: `tests/ChannelSurfer.Tests/ContentServiceTests.cs`
- Modify: `src/ChannelSurfer/Program.cs` (register `HttpClient`, `SettingsService`, `ContentService` as singleton; start refresh timer)

**Interfaces:**
- Consumes: `PlaylistParser` (T2), `XmltvParser` (T3), `Lineup` (T4), `SettingsService` (T6).
- Produces: `class SourceFetcher(HttpClient http)` with `Task<string> FetchTextAsync(string source, bool gunzip)` — reads a local path when `source` is a rooted file path, else GETs the URL; gunzips when `gunzip` or the bytes start with the gzip magic `0x1f 0x8b`. `class ContentService` with `Lineup Current { get; }`, `Task ReloadAsync()`, and a `DateTimeOffset LastLoaded`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] async Task Fetch_ReadsLocalFile() {
  var f = WriteTemp("#EXTM3U\n#EXTINF:-1,A\nhttp://a\n");
  Assert.Contains("#EXTM3U", await new SourceFetcher(new HttpClient()).FetchTextAsync(f, gunzip:false));
}
[Fact] async Task Fetch_GunzipsXmltv() {                            // Review Focus
  var f = WriteTempGzip("<tv></tv>");
  Assert.Equal("<tv></tv>", await new SourceFetcher(new HttpClient()).FetchTextAsync(f, gunzip:true));
}
[Fact] async Task Reload_BuildsLineupFromSources() {
  // settings point at two temp files; after ReloadAsync, Current.Channels is non-empty
}
```

- [ ] **Step 2: Run tests, verify they fail** — Expected: FAIL.

- [ ] **Step 3: Implement `SourceFetcher` and `ContentService`.** `ContentService.ReloadAsync` reads both sources via `SourceFetcher`, parses, and rebuilds `Current = Lineup.Build(...)`. Register in `Program.cs` and kick a periodic reload every `RefreshMinutes` via a `PeriodicTimer` hosted service; reload once at startup.

- [ ] **Step 4: Run tests, verify pass** — Expected: PASS.

- [ ] **Step 5: Commit** — `git commit -am "feat: source fetch, cache and scheduled refresh"`

---

### Task 8: Proxy endpoints

**Files:**
- Create: `src/ChannelSurfer/Proxy/ProxyEndpoints.cs`
- Modify: `src/ChannelSurfer/Program.cs` (map endpoints)
- Test: `tests/ChannelSurfer.Tests/ProxyEndpointsTests.cs`

**Interfaces:**
- Consumes: `HlsRewriter` (T5).
- Produces: routes `GET /proxy/stream?u={url}` (if the body is an m3u8, rewrite segment/sub-playlist URIs back through `/proxy/stream`; otherwise stream bytes through with the upstream content-type) and `GET /proxy/logo?u={url}` (stream image bytes). Both validate `u` is an absolute http/https URL.

- [ ] **Step 1: Write the failing test** (integration via `WebApplicationFactory` with a stubbed `HttpMessageHandler` returning a known m3u8)

```csharp
[Fact] async Task Stream_RewritesPlaylistThroughProxy() {
  // GET /proxy/stream?u=http://host/live/x.m3u8 -> body contains "/proxy/stream?u=" for seg1.ts
}
[Fact] async Task Stream_RejectsNonHttpUrl() {
  // u=file:///etc/passwd -> 400
}
```

- [ ] **Step 2: Run tests, verify they fail** — Expected: FAIL.

- [ ] **Step 3: Implement `ProxyEndpoints`.** Map the two routes; detect m3u8 by content-type or `#EXTM3U` prefix and apply `HlsRewriter.Rewrite(body, upstreamUri, u => $"/proxy/stream?u={Uri.EscapeDataString(u.ToString())}")`; otherwise copy the response stream. Reject non-absolute or non-http(s) `u` with 400.

- [ ] **Step 4: Run tests, verify pass** — Expected: PASS.

- [ ] **Step 5: Commit** — `git commit -am "feat: HLS and logo proxy endpoints"`

---

### Task 9: Surf view — video playback, banner, keyboard

**Files:**
- Create: `src/ChannelSurfer/wwwroot/js/hls-interop.js`, `src/ChannelSurfer/wwwroot/lib/hls.min.js` (vendored), `src/ChannelSurfer/Components/Banner.razor`, `src/ChannelSurfer/Components/Pages/Surf.razor`
- Modify: `src/ChannelSurfer/Components/App.razor` (reference the scripts), `src/ChannelSurfer/Components/Routes.razor` / routing so `/` is `Surf`.

**Interfaces:**
- Consumes: `ContentService.Current` (T7); proxied stream URL = `/proxy/stream?u={escaped channel.StreamUrl}`; logo URL = `/proxy/logo?u={escaped channel.LogoUrl}`; `Lineup.NowNext`/`Progress` (T4).
- Produces: `window.hlsInterop.attach(videoEl, url)` and `window.hlsInterop.destroy(videoEl)` (use `hls.js` when `Hls.isSupported()`, else set `video.src` for native HLS, else raise an error callback); `Banner` component parameters `(Channel Channel, Programme? Now, Programme? Next, double Progress)`.

- [ ] **Step 1: Implement `hls-interop.js`** — `attach` creates an `Hls`, loads the url, attaches to the media element, and plays; on fatal error invokes a .NET callback so the page can show a message. `destroy` tears the instance down.

- [ ] **Step 2: Implement `Banner.razor`** — renders channel number, proxied logo, name, `Now.Title`, `Next` start time, and a progress bar width = `Progress*100%`, matching the mockup's banner markup/classes.

- [ ] **Step 3: Implement `Surf.razor`** — holds current channel index into `ContentService.Current.Channels`; `@onkeydown` handles: `+`/`PageUp`/`ArrowUp`→next channel, `-`/`PageDown`/`ArrowDown`→previous, digits→accumulate then `Enter`→tune by number, `g`→navigate to `/guide`, `f`→fullscreen, `Escape`→hide banner. On channel change: call `destroy` then `attach` with the proxied url, recompute now/next, show the banner, and start a ~5s auto-hide timer.

- [ ] **Step 4: Verify by running the app** — Run: `dotnet run --project src/ChannelSurfer` with a real m3u/XMLTV configured. Confirm: a channel plays; `+`/`-` flips and the banner slides up with correct number/now/next then auto-hides; a deliberately bad stream URL shows an on-screen error, not a silent black screen (Review Focus).

- [ ] **Step 5: Commit** — `git commit -am "feat: surf view with hls.js playback, banner and keyboard control"`

---

### Task 10: Guide grid

**Files:**
- Create: `src/ChannelSurfer/Scheduling/GuideModel.cs`, `src/ChannelSurfer/Components/Pages/Guide.razor`
- Test: `tests/ChannelSurfer.Tests/GuideModelTests.cs`

**Interfaces:**
- Consumes: `Lineup` (T4).
- Produces: `static GuideModel GuideModel.Build(Lineup lineup, DateTimeOffset start, int slotCount, TimeSpan slot)` giving column headers (`start`, `start+slot`, …) and, per channel, cells with `Title`, `StartSlotIndex`, `SpanSlots`; a channel/time pair with no programme yields a single-slot cell titled `"No info"`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] void Build_PlacesProgrammeAcrossSpannedSlots() {
  // a 20:00-21:00 programme with 30-min slots starting 20:00 -> SpanSlots == 2, StartSlotIndex == 0
}
[Fact] void Build_FillsGapWithNoInfo() {                            // Review Focus
  // channel with no programmes -> every cell Title == "No info"
}
[Fact] void Build_EmitsSlotCountHeaders() { /* headers.Count == slotCount */ }
```

- [ ] **Step 2: Run tests, verify they fail** — Expected: FAIL.

- [ ] **Step 3: Implement `GuideModel.Build`** — generate `slotCount` headers; for each channel in `lineup.Channels` walk `lineup.Programmes(channel)`, map each overlapping programme to `StartSlotIndex`/`SpanSlots` clamped to the window, and emit `"No info"` cells for uncovered ranges.

- [ ] **Step 4: Run tests, verify pass** — Expected: PASS.

- [ ] **Step 5: Implement `Guide.razor`** — render the grid from `GuideModel` (channels down the side, slot headers across the top, cells spanning columns), a yellow highlight moved by arrow keys (and time-window paging at the edges), a preview pane bound to the highlighted cell, and `Enter`→navigate to `/` tuning that channel; `Escape`/`g`→back to surf. Matches the mockup's guide markup/classes.

- [ ] **Step 6: Verify by running the app** — highlight moves with arrows, preview updates, Enter tunes and returns to surf.

- [ ] **Step 7: Commit** — `git commit -am "feat: grid guide with EPG slots and keyboard navigation"`

---

### Task 11: Settings page

**Files:**
- Create: `src/ChannelSurfer/Components/Pages/Settings.razor`
- Modify: `src/ChannelSurfer/Components/Layout/MainLayout.razor` or nav as needed
- Consumes: `SettingsService` (T6), `ContentService.ReloadAsync` (T7).

- [ ] **Step 1: Implement `Settings.razor`** — form bound to `AppSettings` (playlist source, XMLTV source, refresh minutes, CRT toggle); **Save** persists via `SettingsService` and calls `ContentService.ReloadAsync`; show `LastLoaded` and channel count, and any reload error.

- [ ] **Step 2: Verify by running the app** — change the playlist path to a bad value, Save, see the error and old lineup retained; fix it, Save, see the count update. Restart the container/process and confirm the saved values persist.

- [ ] **Step 3: Commit** — `git commit -am "feat: settings page with live reload"`

---

### Task 12: CRT styling to match the approved mockup

**Files:**
- Create: `src/ChannelSurfer/wwwroot/css/crt.css` (and component styles as needed)
- Modify: `src/ChannelSurfer/Components/App.razor` (link css), root layout (apply/remove `crt` class from `AppSettings.CrtEnabled`)
- Reference: `.superpowers/brainstorm/*/content/look-v2-crt.html` (visual contract)

- [ ] **Step 1: Port the styles** — bring the mockup's palette, banner, grid, scanline/glow/vignette/flicker overlay into app CSS; gate the overlay behind a root `crt` class toggled by the CRT setting. Keep the overlay off the `<video>` element so the picture stays sharp.

- [ ] **Step 2: Verify by running the app** — surf view and guide visually match the mockup; the Settings CRT toggle turns the overlay on/off.

- [ ] **Step 3: Commit** — `git commit -am "feat: CRT visual styling"`

---

### Task 13: Dockerfile + container

**Files:**
- Create: `Dockerfile`, `.dockerignore`
- Modify: `README.md` (run instructions)

- [ ] **Step 1: Write the Dockerfile** — multi-stage: `sdk:10.0` build/publish, `aspnet:10.0` runtime; expose the app port; default `CONFIG_DIR=/config` and declare the `/config` volume; entrypoint runs the app.

- [ ] **Step 2: Build and run** — Run: `docker build -t channel-surfer . && docker run --rm -p 8080:8080 -v $PWD/config:/config channel-surfer`. Open `http://localhost:8080`, set sources in Settings, confirm playback and guide work.

- [ ] **Step 3: Verify persistence** — restart the container with the same volume; settings and sources are retained.

- [ ] **Step 4: Commit** — `git commit -am "feat: containerize with /config volume"`

---

## Notes

- UI tasks (9, 11, 12, and the razor steps of 10) verify by running the app, because Blazor-Server video/keyboard behavior isn't meaningfully unit-testable without a browser; all parsing and scheduling logic — the spec's test priority — is covered by xUnit tasks 2–8 and 10.
- `hls.js` is vendored into `wwwroot/lib` rather than added as an npm/CDN dependency, keeping the container self-contained and the browser off third-party origins.

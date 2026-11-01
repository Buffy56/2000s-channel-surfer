# 2000s Channel Surfer

A containerized IPTV client with a CRT-styled channel-surf zapper and a blue
Prevue-style grid guide, driven by your own m3u playlist and XMLTV schedule.

- **Surf view** (`/`): channel up/down flips channels and slides up a chunky CRT
  banner (number, logo, now/next, progress). Type digits + Enter to jump to a
  channel number. `g` opens the guide, `f` toggles fullscreen.
- **Guide** (`/guide`): blue grid — channels down the side, 30-minute columns
  across the top. Arrow keys move the highlight, Enter tunes, Esc/`g` returns.
- **Settings** (`/settings`): set the playlist and XMLTV sources (file path or
  URL), the refresh interval, and the CRT toggle. Saved to the config volume.

Streams are played in the browser: **HLS** (`.m3u8`) via `hls.js` and live
**MPEG-TS** (`.ts`, e.g. ErsatzTV) via `mpegts.js`. The backend proxies streams
and logos so the browser never talks to the IPTV server directly.

Built with ASP.NET Core + Blazor Server (.NET 10).

## Run with Docker

```bash
docker build -t channel-surfer .
docker run --rm -p 8080:8080 -v "$PWD/config:/config" channel-surfer
```

Open <http://localhost:8080>, go to **Settings**, point the playlist and guide
at your m3u and XMLTV (a path under `/config`, or a URL), and Save.

Settings persist in `./config/settings.json` on the mounted volume.

## Develop

```bash
dotnet run --project src/ChannelSurfer      # serves on http://localhost:5xxx
dotnet test                                 # unit tests for parsing/scheduling
```

In development, config defaults to `./src/ChannelSurfer/config` (override with
the `CONFIG_DIR` environment variable).

## License

[MIT](LICENSE)

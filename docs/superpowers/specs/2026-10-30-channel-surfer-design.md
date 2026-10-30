# 2000s Channel Surfer — Design

**Date:** 2026-10-30
**Status:** Draft for approval

## Intent & why

A containerized IPTV client for the living-room PC that brings back the
lean-back, 2000s cable experience: flip channels and watch the chunky banner
slide up, or pull up the big blue grid guide and surf it. The user already has
an m3u playlist and an XMLTV schedule; existing IPTV front-ends are either
utilitarian or phone-first. This one is deliberately couch-first and nostalgic —
the CRT-styled zapper and the Prevue-style grid are the whole point, not a skin
bolted on afterward.

## Goals (observable acceptance)

1. **Runs as one container.** `docker run` with the config volume mounted brings
   up the app; opening the mapped port in a living-room browser shows live TV.
2. **Surf view (zapper).** Full-screen video of the current channel. Channel
   up/down flips channels; each flip slides up the **CRT banner** for a few
   seconds showing channel number, logo, channel name, now-playing title,
   next-at time, and a progress bar for the current program. Banner auto-hides.
3. **Grid guide.** Blue, CRT-styled grid: channels down the left (number +
   name/logo), 30-minute columns across the top, programs in their time slots.
   A yellow highlight moves with the arrow keys; a preview pane shows the
   highlighted program's title/time/description. **Enter** tunes that channel and
   returns to surf view.
4. **Real schedule.** Now/next and the grid come from the parsed XMLTV, matched
   to channels by `tvg-id` (name-match fallback when `tvg-id` is missing).
5. **Settings page.** Shows the current m3u and XMLTV sources (file path or URL)
   and lets the user edit them and trigger a reload — for troubleshooting when a
   source changes or breaks. Changes persist across container restarts.
6. **CRT coat.** Scanlines, phosphor glow, vignette, subtle flicker as a CSS
   overlay over the whole UI, with a toggle to turn it off. The overlay never
   touches the decoded video — the stream stays sharp underneath.

## Non-goals (v1)

- Multi-user, accounts, or authentication (single couch, single user).
- Transcoding / ffmpeg. We assume streams are browser-playable HLS; if a stream
  won't play in the browser, that's reported, not transcoded.
- DVR / recording / catch-up / timeshift.
- Favorites, search, channel reordering, parental controls.
- Channel-flip static/snow animation (explicitly deferred; easy to add later).
- Mobile / responsive / touch layout. Big-screen keyboard-driven only.

## Anti-goals (would make it a failure even if it "works")

- A database or heavyweight persistence. In-memory caches + one `settings.json`
  on a volume is the whole state story.
- A generic plugin/provider abstraction. There is exactly one source type each
  for playlist and schedule.
- A phone-style or "modern dashboard" look that loses the 2000s cable feel.
- Requiring the user to edit config files or rebuild the image to change sources —
  that's what the settings page is for.

## Constraints

- **Stack:** ASP.NET Core + **Blazor Server**, C# end to end, single project,
  single container. **.NET 10** (current LTS).
- **Playback:** live HLS via `hls.js` with a thin JS-interop shim; fall back to
  the native `<video>` element where the browser plays HLS natively.
- **Target browser:** current Chrome/Edge on a living-room PC, large screen.
- **Input:** keyboard / media remote that emits keys. No mouse-first flows
  required (mouse may work in the guide, but keyboard is the contract).
- **Config volume:** a mounted directory (e.g. `/config`) holds `settings.json`;
  m3u/XMLTV may themselves be files under a mounted path **or** remote URLs.

## The "how" that's decided

### Streams are proxied through the backend
IPTV HLS sources commonly lack permissive CORS headers, and a Blazor Server page
served over one origin can't reliably fetch cross-origin playlists/segments from
the browser. So the backend exposes a **stream proxy**: it fetches the HLS master
/ variant playlists, rewrites segment and sub-playlist URIs to point back through
the proxy, and streams the `.ts`/segment bytes to the browser. Channel logos and
the m3u/XMLTV sources are fetched server-side the same way. One code path, always
proxied — no per-channel direct/proxy toggle in v1.

> ponytail: proxy-everything is more bytes through the container than strictly
> necessary, but it's the one reliable path and the simplest to reason about.
> Upgrade path: a "try direct first" per-channel probe if bandwidth matters.

### State model
- **Channel:** id (`tvg-id`), number (`tvg-chno` if present, else playlist order),
  name, logo URL, group-title, stream URL.
- **Programme:** channel id, title, description, start, stop, category.
- Channels ↔ programmes matched on `tvg-id`; fallback to normalized name match.
- Playlist and schedule are parsed into **in-memory caches**, refreshed on a
  timer and on demand from the settings page. No DB.

### Services (backend)
- `PlaylistService` — fetch + parse m3u (`#EXTINF` attributes: `tvg-id`,
  `tvg-name`, `tvg-logo`, `tvg-chno`, `group-title`).
- `EpgService` — fetch + parse XMLTV (handle gzip; it's commonly gzipped),
  build the schedule, compute now/next per channel.
- `SettingsService` — read/write `settings.json` on the config volume
  (m3u source, XMLTV source, refresh interval, CRT on/off).
- Stream/asset proxy endpoint(s).

### UI (Blazor components)
- `SurfView` — video host + `Banner`; owns keyboard handling for flip, number
  entry, open-guide, fullscreen.
- `GuideGrid` — virtualized channels × time grid, highlight navigation, preview
  pane, Enter-to-tune.
- `Banner` — channel number, logo, name, now/next, progress bar; auto-hide timer.
- `Settings` page — source paths/URLs, reload button, CRT toggle.
- CRT overlay as shared CSS (the approved look in
  `.superpowers/brainstorm/.../look-v2-crt.html`).

### Keyboard map (initial)
- **Ch Up / Ch Down** (or `+` / `-` / PageUp / PageDown): flip channel, show banner.
- **Digits + Enter:** jump to channel number.
- **Guide key** (e.g. `g` / a dedicated key / arrow-up from surf): open grid.
- **Arrows:** move grid highlight (and scroll time/channels).
- **Enter:** tune highlighted channel, return to surf.
- **Esc / Back:** close guide / hide banner.
- **F:** fullscreen.

### Testing
Unit tests at minimum (user requirement), with first priority on the parsing and
scheduling logic that everything else depends on:
- m3u parser (attribute extraction, number fallback, malformed lines).
- XMLTV parser (gzip, time-zone handling, channel/programme linkage).
- now/next computation and tvg-id↔name matching.

## What's left to the builder

- Exact component tree, state container, and how surf↔guide state is shared.
- Styling the real components to match the approved CRT mockup (the mockup is the
  visual contract; pixel values are the builder's).
- Stream-proxy edge cases: relative vs absolute segment URIs, variant playlists,
  redirects, auth query params on segment URLs, timeouts/retries.
- How channel numbers are derived when neither `tvg-chno` nor a clean order exists.
- Exact keys for "open guide" and number entry on the user's remote/keyboard.
- Dockerfile, base image, port, and the `/config` volume layout; `.gitignore`
  (add `.superpowers/` and build output).
- Refresh interval default and whether a stale schedule shows a warning.

## Open decisions settled as defaults (change if wrong)

- **Single user, no auth.**
- **.NET 10 / Blazor Server.**
- **Proxy all streams** through the backend.
- **Keyboard-driven**; mouse optional in the guide.
- **settings.json on a volume** as the only persistence.
- m3u/XMLTV sources may be **local mounted paths or remote URLs**.

---

> Note: this project directory is **not yet a git repository**. Per the user's
> workflow (branch before building), the build session should `git init`, make
> the initial commit, and branch before writing product code — all of which are
> build-gated actions for a later session.

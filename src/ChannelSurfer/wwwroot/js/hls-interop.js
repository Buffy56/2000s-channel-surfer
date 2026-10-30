// Bridge between Blazor and the browser video players.
// kind "ts"  -> mpegts.js (live MPEG-TS, e.g. ErsatzTV)
// kind "hls" -> hls.js (.m3u8), falling back to native HLS
window.hlsInterop = (function () {
    const instances = new WeakMap(); // video -> { kind, inst }

    function detach(video) {
        const entry = instances.get(video);
        if (!entry) return;
        try {
            if (entry.kind === "mpegts") {
                entry.inst.pause();
                entry.inst.unload();
                entry.inst.detachMediaElement();
                entry.inst.destroy();
            } else if (entry.kind === "hls") {
                entry.inst.destroy();
            }
        } catch (e) { /* ignore */ }
        instances.delete(video);
    }

    function fail(dotnet, msg) {
        if (dotnet) dotnet.invokeMethodAsync('OnStreamError', String(msg));
    }

    return {
        attach: function (video, url, kind, dotnet) {
            if (!video) return;
            detach(video);

            if (kind === "ts") {
                if (window.mpegts && window.mpegts.isSupported()) {
                    const player = window.mpegts.createPlayer(
                        { type: 'mpegts', isLive: true, url: url },
                        { liveBufferLatencyChasing: true });
                    player.on(window.mpegts.Events.ERROR, function (type, detail) {
                        fail(dotnet, (type || 'error') + ': ' + (detail || ''));
                    });
                    player.attachMediaElement(video);
                    player.load();
                    player.play().catch(function () {});
                    instances.set(video, { kind: "mpegts", inst: player });
                } else {
                    fail(dotnet, 'MPEG-TS playback not supported in this browser');
                }
                return;
            }

            if (window.Hls && window.Hls.isSupported()) {
                const hls = new window.Hls({ enableWorker: true, lowLatencyMode: true });
                hls.on(window.Hls.Events.ERROR, function (_evt, data) {
                    if (data.fatal) fail(dotnet, data.details || data.type || 'error');
                });
                hls.on(window.Hls.Events.MANIFEST_PARSED, function () { video.play().catch(function () {}); });
                hls.loadSource(url);
                hls.attachMedia(video);
                instances.set(video, { kind: "hls", inst: hls });
            } else if (video.canPlayType('application/vnd.apple.mpegurl')) {
                video.src = url;
                video.addEventListener('loadedmetadata', function () { video.play().catch(function () {}); });
                video.addEventListener('error', function () { fail(dotnet, 'native playback error'); });
            } else {
                fail(dotnet, 'HLS not supported in this browser');
            }
        },

        destroy: function (video) {
            detach(video);
            if (video) { try { video.removeAttribute('src'); video.load(); } catch (e) { /* ignore */ } }
        },

        fullscreen: function () {
            if (document.fullscreenElement) { document.exitFullscreen(); }
            else { document.documentElement.requestFullscreen().catch(function () {}); }
        }
    };
})();

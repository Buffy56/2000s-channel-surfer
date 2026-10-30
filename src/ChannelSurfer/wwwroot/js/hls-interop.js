// Thin bridge between Blazor and hls.js. Keeps one Hls instance per <video>.
window.hlsInterop = (function () {
    const instances = new WeakMap();

    function detach(video) {
        const hls = instances.get(video);
        if (hls) { hls.destroy(); instances.delete(video); }
    }

    return {
        attach: function (video, url, dotnet) {
            if (!video) return;
            detach(video);

            if (window.Hls && window.Hls.isSupported()) {
                const hls = new window.Hls({ enableWorker: true, lowLatencyMode: true });
                hls.on(window.Hls.Events.ERROR, function (_evt, data) {
                    if (data.fatal && dotnet) {
                        dotnet.invokeMethodAsync('OnStreamError', String(data.details || data.type || 'error'));
                    }
                });
                hls.on(window.Hls.Events.MANIFEST_PARSED, function () { video.play().catch(function () {}); });
                hls.loadSource(url);
                hls.attachMedia(video);
                instances.set(video, hls);
            } else if (video.canPlayType('application/vnd.apple.mpegurl')) {
                video.src = url;
                video.addEventListener('loadedmetadata', function () { video.play().catch(function () {}); });
                video.addEventListener('error', function () {
                    if (dotnet) dotnet.invokeMethodAsync('OnStreamError', 'native playback error');
                });
            } else if (dotnet) {
                dotnet.invokeMethodAsync('OnStreamError', 'HLS not supported in this browser');
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

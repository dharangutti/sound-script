// Offline-rendered WAV playback for SoundScript V4 timbre output.

window.SoundScriptAudio = (function () {
    let audioContext = null;
    let activeSource = null;

    function ensureContext() {
        if (!audioContext) {
            audioContext = new (window.AudioContext || window.webkitAudioContext)();
        }
        return audioContext;
    }

    async function playWavBytes(bytes) {
        stop();
        const ctx = ensureContext();
        const buffer = await ctx.decodeAudioData(bytes.slice(0).buffer);
        const source = ctx.createBufferSource();
        source.buffer = buffer;
        source.connect(ctx.destination);
        source.start(0);
        activeSource = source;
        source.onended = () => {
            if (activeSource === source) {
                activeSource = null;
            }
        };
        return buffer.duration;
    }

    function stop() {
        if (activeSource) {
            try {
                activeSource.stop();
            } catch (_) {
                // already stopped
            }
            activeSource = null;
        }
    }

    function download(base64, filename) {
        const link = document.createElement('a');
        link.href = `data:audio/wav;base64,${base64}`;
        link.download = filename;
        link.click();
    }

    return { playWavBytes, stop, download };
})();

window.startWavPlayback = (bytes) => window.SoundScriptAudio.playWavBytes(bytes);

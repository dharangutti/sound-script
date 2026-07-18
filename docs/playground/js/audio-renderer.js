// Offline-rendered WAV playback for SoundScript V4 timbre output.

window.SoundScriptAudio = (function () {
    let audioContext = null;
    let activeSource = null;

    // WAV header stores the sample rate as a little-endian uint32 at byte
    // offset 24 (RIFF/WAVE "fmt " chunk). Reading it lets us pin the
    // AudioContext to the file's actual rate instead of the device's default
    // output rate (often 48000 Hz), which otherwise makes decodeAudioData
    // silently resample the transient/noise-heavy synthesized speech and
    // audibly soften it compared to a media player playing the same bytes.
    function readWavSampleRate(bytes) {
        if (bytes.length < 28)
            return null;
        return (bytes[24] | (bytes[25] << 8) | (bytes[26] << 16) | (bytes[27] << 24)) >>> 0;
    }

    function ensureContext(sampleRate) {
        if (audioContext && sampleRate && audioContext.sampleRate !== sampleRate) {
            audioContext.close();
            audioContext = null;
        }
        if (!audioContext) {
            const options = sampleRate ? { sampleRate } : undefined;
            audioContext = new (window.AudioContext || window.webkitAudioContext)(options);
        }
        return audioContext;
    }

    async function playWavBytes(bytes) {
        stop();
        const ctx = ensureContext(readWavSampleRate(bytes));
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
        window.SoundScriptDownload.fromBase64(base64, filename || 'soundscript.wav', 'audio/wav');
    }

    return { playWavBytes, stop, download };
})();

window.startWavPlayback = (bytes) => window.SoundScriptAudio.playWavBytes(bytes);

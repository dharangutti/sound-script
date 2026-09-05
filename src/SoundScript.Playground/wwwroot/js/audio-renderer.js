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

    async function playWavBytesFromOffset(bytes, offsetSeconds = 0) {
        stop();
        const ctx = ensureContext(readWavSampleRate(bytes));
        await ctx.resume();
        if (ctx.state === 'suspended') {
            throw new Error('AudioContext is unavailable. Interact with the page and try again.');
        }

        const buffer = await ctx.decodeAudioData(bytes.slice(0).buffer);
        const offset = Math.max(0, Math.min(buffer.duration, Number(offsetSeconds) || 0));
        const source = ctx.createBufferSource();
        source.buffer = buffer;
        source.connect(ctx.destination);
        const startAt = ctx.currentTime + 0.05;
        source.start(startAt, offset);
        activeSource = source;
        source.onended = () => {
            if (activeSource === source) {
                activeSource = null;
            }
        };
        return {
            startDelayMs: Math.max(0, (startAt - ctx.currentTime) * 1000),
            durationSeconds: Math.max(0, buffer.duration - offset)
        };
    }

    async function playWavBytes(bytes) {
        const playback = await playWavBytesFromOffset(bytes, 0);
        return playback.durationSeconds;
    }

    // Prepares a deterministic WAV rail for MediaRecorder without starting it.
    // The caller must invoke `started()` only after its video recorder is ready;
    // that function schedules both rails around the same explicit AudioContext
    // time instead of relying on a hidden player start-ahead offset.
    async function startExportPlayback(bytes) {
        stop();
        const ctx = ensureContext(readWavSampleRate(bytes));
        await ctx.resume();
        if (ctx.state === 'suspended') {
            throw new Error('AudioContext is unavailable. Interact with the page and try export again.');
        }

        const buffer = await ctx.decodeAudioData(bytes.slice(0).buffer);
        const source = ctx.createBufferSource();
        source.buffer = buffer;

        const mediaDestination = ctx.createMediaStreamDestination();
        source.connect(mediaDestination);
        source.connect(ctx.destination);

        let hasStarted = false;
        let stoppedBeforeStart = false;
        source.onended = () => {
            if (activeSource === source) {
                activeSource = null;
            }
        };

        return {
            stream: mediaDestination.stream,
            durationSeconds: buffer.duration,
            started(startAtAudioTime) {
                if (hasStarted) {
                    throw new Error('The audio export session has already started.');
                }
                if (stoppedBeforeStart) {
                    throw new Error('The audio export session was stopped before it could start.');
                }

                // A short lead gives MediaRecorder time to attach to the audio
                // track. It is returned so video can begin at the same semantic
                // instant, rather than at recorder setup time.
                const requested = Number(startAtAudioTime);
                const minimumStart = ctx.currentTime + 0.05;
                const startAt = Number.isFinite(requested)
                    ? Math.max(requested, minimumStart)
                    : minimumStart;

                const scheduledAtPerformanceTime = performance.now();
                source.start(startAt);
                activeSource = source;
                hasStarted = true;
                return {
                    audioContextTime: startAt,
                    scheduledAtPerformanceTime,
                    leadInMilliseconds: Math.max(0, (startAt - ctx.currentTime) * 1000)
                };
            },
            stopBeforeStart() {
                if (!hasStarted) {
                    stoppedBeforeStart = true;
                }
            }
        };
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

    return { playWavBytes, playWavBytesFromOffset, startExportPlayback, stop, download };
})();

window.startWavPlayback = (bytes) => window.SoundScriptAudio.playWavBytes(bytes);

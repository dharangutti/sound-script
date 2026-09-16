// Browser codec boundary. The decoded buffer stays local and is replaced on each import.
window.SoundScriptTranscription = (() => {
    let buffer, originalUrl, excerptPlayer, stopAtEnd;
    function stopOriginal() {
        document.getElementById("transcription-original")?.pause();
        if (excerptPlayer && stopAtEnd) excerptPlayer.removeEventListener("timeupdate", stopAtEnd);
        excerptPlayer = stopAtEnd = null;
    }
    async function playOriginalExcerpt(start, duration) {
        if (!Number.isFinite(start) || start < 0 || !Number.isFinite(duration) || duration <= 0 || duration > 120)
            throw new Error("Choose a valid excerpt of at most 120 seconds.");
        stopOriginal();
        const player = document.getElementById("transcription-original");
        if (!player) throw new Error("Select an original recording first.");
        excerptPlayer = player;
        stopAtEnd = () => { if (player.currentTime >= start + duration) stopOriginal(); };
        player.addEventListener("timeupdate", stopAtEnd);
        player.currentTime = start;
        try { await player.play(); }
        catch (error) { stopOriginal(); throw new Error("Original playback failed. Try PCM WAV supported by your browser. " + error.message); }
    }
    function releaseOriginal() {
        if (originalUrl) { stopOriginal(); URL.revokeObjectURL(originalUrl); originalUrl = null; }
    }
    async function inspect(bytes) {
        buffer = null;
        const data = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
        if (data.byteLength > 64 * 1024 * 1024) throw new Error("Media exceeds 64 MiB.");
        const context = new OfflineAudioContext(1, 1, 16000);
        try { buffer = await context.decodeAudioData(data.buffer.slice(data.byteOffset, data.byteOffset + data.byteLength)); }
        catch { throw new Error("This browser cannot decode the selected media. Try PCM WAV, or use the CLI with FFmpeg for video and other codecs."); }
        return { durationSeconds: buffer.duration, sampleRate: buffer.sampleRate || 16000, channels: buffer.numberOfChannels };
    }
    function excerpt(start = 0, duration = null) {
        if (!buffer) throw new Error("Select and decode media first.");
        duration = duration ?? buffer.duration - start;
        if (!Number.isFinite(start) || start < 0 || start >= buffer.duration || !Number.isFinite(duration) || duration <= 0)
            throw new Error("Choose a finite, positive excerpt within the recording.");
        if (duration > 120) throw new Error(`Media duration ${buffer.duration.toFixed(3)} seconds; selected duration ${duration.toFixed(3)} seconds exceeds the 120-second analysis limit. Select a shorter excerpt.`);
        if (start + duration > buffer.duration + .001) throw new Error("The selected excerpt extends beyond the media duration.");
        const rate = buffer.sampleRate || 16000;
        const begin = Math.round(start * rate), end = Math.min(buffer.length, Math.round((start + duration) * rate));
        const mono = new Float32Array(end - begin);
        for (let channel = 0; channel < buffer.numberOfChannels; channel++) {
            const samples = buffer.getChannelData(channel);
            for (let i = 0; i < mono.length; i++) mono[i] += samples[begin + i] / buffer.numberOfChannels;
        }
        for (let i = 0; i < mono.length; i++) mono[i] = Math.max(-1, Math.min(1, mono[i]));
        return new Uint8Array(mono.buffer);
    }
    return { inspect, excerpt, stopOriginal, playOriginalExcerpt,
        sourceUrl(bytes, type) { releaseOriginal(); originalUrl = URL.createObjectURL(new Blob([bytes], { type: type || "audio/wav" })); return originalUrl; },
        clear() { buffer = null; releaseOriginal(); }, async decode(bytes) { await inspect(bytes); return excerpt(); } };
})();

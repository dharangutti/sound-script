// Codec-dependent browser input adapter. Analysis always receives mono float32 / 16 kHz.
window.SoundScriptTranscription = {
    async decode(bytes) {
        const data = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
        if (data.byteLength > 64 * 1024 * 1024) throw new Error("Media exceeds 64 MiB.");
        const context = new OfflineAudioContext(1, 1, 16000);
        let buffer;
        try { buffer = await context.decodeAudioData(data.buffer.slice(data.byteOffset, data.byteOffset + data.byteLength)); }
        catch { throw new Error("This browser cannot decode the selected media. Try PCM WAV, or use the CLI with FFmpeg for video and other codecs."); }
        if (buffer.duration > 120) throw new Error("Audio exceeds the 120-second analysis limit.");
        const mono = new Float32Array(buffer.length);
        for (let channel = 0; channel < buffer.numberOfChannels; channel++) {
            const samples = buffer.getChannelData(channel);
            for (let i = 0; i < mono.length; i++) mono[i] += samples[i] / buffer.numberOfChannels;
        }
        for (let i = 0; i < mono.length; i++) mono[i] = Math.max(-1, Math.min(1, mono[i]));
        // IJSStreamReference return marshalling creates the stream reference itself.
        return new Uint8Array(mono.buffer);
    }
};

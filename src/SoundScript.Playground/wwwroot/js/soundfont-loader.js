// Local soundfont loader — fetches WAV samples from /playground/soundfont/samples/
// No CDN, no external calls. Twelve pitch classes at octave 3, pitch-shifted per note.

window.SoundScriptSoundfont = (function () {
    const PITCH_CLASSES = ['C', 'Cs', 'D', 'Ds', 'E', 'F', 'Fs', 'G', 'Gs', 'A', 'As', 'B'];
    const BASE_MIDI = 48; // C3
    const SAMPLE_ROOT = 'soundfont/samples/';

    let audioContext = null;
    const buffers = new Array(12).fill(null);
    let loadPromise = null;

    async function load(context) {
        if (buffers[0]) {
            audioContext = context;
            return;
        }

        if (loadPromise) {
            await loadPromise;
            audioContext = context;
            return;
        }

        audioContext = context;
        loadPromise = (async () => {
            for (let i = 0; i < PITCH_CLASSES.length; i++) {
                const url = SAMPLE_ROOT + PITCH_CLASSES[i] + '.wav';
                const response = await fetch(url);
                if (!response.ok) {
                    throw new Error('Failed to load soundfont sample: ' + url);
                }
                const data = await response.arrayBuffer();
                buffers[i] = await audioContext.decodeAudioData(data);
            }
        })();

        await loadPromise;
    }

    function playNote(midiNote, velocity, startTime, duration, destination) {
        const pitchClass = ((midiNote % 12) + 12) % 12;
        const buffer = buffers[pitchClass];
        if (!buffer || !audioContext) {
            return null;
        }

        const baseMidi = BASE_MIDI + pitchClass;
        const playbackRate = Math.pow(2, (midiNote - baseMidi) / 12);
        const source = audioContext.createBufferSource();
        const gain = audioContext.createGain();

        source.buffer = buffer;
        source.playbackRate.value = playbackRate;
        gain.gain.value = Math.max(0.01, Math.min(1, velocity / 127));

        source.connect(gain);
        gain.connect(destination);

        const playDuration = Math.min(duration, buffer.duration / playbackRate);
        source.start(startTime, 0, playDuration);

        const stopAt = startTime + duration;
        gain.gain.setValueAtTime(gain.gain.value, stopAt - 0.02);
        gain.gain.linearRampToValueAtTime(0, stopAt);

        return { source, gain };
    }

    return {
        load,
        playNote
    };
})();

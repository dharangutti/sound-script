// Local GM soundfont loader — fetches per-program WAV samples from /playground/soundfont/samples/
// No CDN, no external calls. Twelve pitch classes at octave 3, pitch-shifted per note.

window.SoundScriptSoundfont = (function () {
    const PITCH_CLASSES = ['C', 'Cs', 'D', 'Ds', 'E', 'F', 'Fs', 'G', 'Gs', 'A', 'As', 'B'];
    const BASE_MIDI = 48; // C3
    const DEFAULT_PROGRAM = 0;
    const PROGRAMS = [0, 19, 24, 32, 40, 42, 56, 73, 80];
    const PROGRAM_SET = new Set(PROGRAMS);
    const SAMPLE_ROOT = 'soundfont/samples/';

    let audioContext = null;
    const buffers = {};
    const rawBuffers = {};
    const fetchPromises = {};
    const loadPromises = {};

    function resolveProgram(program) {
        return PROGRAM_SET.has(program) ? program : DEFAULT_PROGRAM;
    }

    function normalizePrograms(programs) {
        const requested = Array.isArray(programs) && programs.length > 0 ? programs : [DEFAULT_PROGRAM];
        return [...new Set(requested.map(resolveProgram))];
    }

    function programBuffers(program) {
        if (!buffers[program]) {
            buffers[program] = new Array(12).fill(null);
        }
        return buffers[program];
    }

    function programRawBuffers(program) {
        if (!rawBuffers[program]) {
            rawBuffers[program] = new Array(12).fill(null);
        }
        return rawBuffers[program];
    }

    async function fetchProgram(program) {
        const resolvedProgram = resolveProgram(program);
        const rawSamples = programRawBuffers(resolvedProgram);
        if (rawSamples[0]) {
            return;
        }

        if (fetchPromises[resolvedProgram]) {
            await fetchPromises[resolvedProgram];
            return;
        }

        fetchPromises[resolvedProgram] = Promise.all(PITCH_CLASSES.map(async (pitch, index) => {
            const url = SAMPLE_ROOT + resolvedProgram + '/' + pitch + '.wav';
            const response = await fetch(url);
            if (!response.ok) {
                throw new Error('Failed to load soundfont sample: ' + url);
            }
            rawSamples[index] = await response.arrayBuffer();
        }));

        await fetchPromises[resolvedProgram];
    }

    async function loadProgram(program) {
        const resolvedProgram = resolveProgram(program);
        const samples = programBuffers(resolvedProgram);
        if (samples[0]) {
            return;
        }

        if (loadPromises[resolvedProgram]) {
            await loadPromises[resolvedProgram];
            return;
        }

        loadPromises[resolvedProgram] = (async () => {
            await fetchProgram(resolvedProgram);
            const rawSamples = programRawBuffers(resolvedProgram);
            await Promise.all(rawSamples.map(async (data, index) => {
                samples[index] = await audioContext.decodeAudioData(data.slice(0));
            }));
        })();

        await loadPromises[resolvedProgram];
    }

    async function prefetch(programs) {
        await Promise.all(normalizePrograms(programs).map(fetchProgram));
    }

    async function load(context, programs) {
        audioContext = context;
        await Promise.all(normalizePrograms(programs).map(loadProgram));
    }

    function playNote(midiNote, velocity, startTime, duration, destination, program) {
        const resolvedProgram = resolveProgram(program);
        const samples = programBuffers(resolvedProgram);
        const pitchClass = ((midiNote % 12) + 12) % 12;
        const buffer = samples[pitchClass];
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
        prefetch,
        playNote
    };
})();

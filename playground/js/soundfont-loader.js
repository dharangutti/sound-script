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
    const loopBuffers = new WeakMap();

    function sustainBuffer(buffer) {
        if (loopBuffers.has(buffer)) return loopBuffers.get(buffer);
        const copy = audioContext.createBuffer(buffer.numberOfChannels, buffer.length, buffer.sampleRate);
        const start = Math.floor(buffer.length * 0.30);
        const end = Math.floor(buffer.length * 0.80);
        const fade = Math.max(1, Math.min(Math.floor(buffer.sampleRate * 0.03), Math.floor((end - start) / 4)));
        for (let channel = 0; channel < buffer.numberOfChannels; channel++) {
            const original = buffer.getChannelData(channel);
            const data = copy.getChannelData(channel);
            data.set(original);
            for (let i = 0; i < fade; i++) {
                const weight = i / fade;
                data[end - fade + i] = original[end - fade + i] * (1 - weight) + original[start + i] * weight;
            }
        }
        const result = { buffer: copy, start: (start + fade) / buffer.sampleRate, end: end / buffer.sampleRate };
        loopBuffers.set(buffer, result);
        return result;
    }

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

    function playNote(midiNote, velocity, startTime, duration, destination, program, performance = null) {
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

        source.playbackRate.value = playbackRate;
        gain.gain.value = Math.max(0.01, Math.min(1, velocity / 127));

        source.connect(gain);
        gain.connect(destination);

        if (performance) {
            let offset = (performance.elapsed || 0) * playbackRate;
            let playbackBuffer = buffer;
            if (performance.sustained && buffer.duration > 0.1) {
                const loop = sustainBuffer(buffer);
                playbackBuffer = loop.buffer;
                source.loop = true;
                source.loopStart = loop.start;
                source.loopEnd = loop.end;
                if (performance.connected) offset += loop.start;
                if (offset >= loop.end) offset = loop.start + (offset - loop.start) % (loop.end - loop.start);
            } else {
                offset = Math.min(offset, Math.max(0, buffer.duration - 0.001));
            }
            const peak = gain.gain.value;
            const attack = Math.min(performance.attack, duration * 0.25);
            const fullDuration = performance.originalDuration || duration;
            const elapsed = performance.elapsed || 0;
            const levelAt = t => {
                const p = Math.min(1, (elapsed + t) / fullDuration);
                return peak * (1 + (performance.gainEnd - 1) * p) *
                    (1 + performance.evolution * Math.sin(Math.PI * p));
            };
            gain.gain.setValueAtTime(0, startTime);
            gain.gain.linearRampToValueAtTime(levelAt(attack), startTime + attack);
            const steps = Math.max(2, Math.min(512, Math.ceil(duration / 0.025)));
            for (let i = 1; i <= steps; i++) {
                const t = attack + (duration - attack) * i / steps;
                gain.gain.linearRampToValueAtTime(levelAt(t), startTime + t);
            }
            gain.gain.linearRampToValueAtTime(0, startTime + duration + performance.release);
            source.buffer = playbackBuffer;
            source.start(startTime, offset);
            source.stop(startTime + duration + performance.release);
            return { source, gain };
        }

        const playDuration = Math.min(duration, buffer.duration / playbackRate);
        source.buffer = buffer;
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

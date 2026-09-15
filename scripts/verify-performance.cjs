// Run after ExpressivePerformanceTests with SOUNDSCRIPT_PERFORMANCE_ARTIFACTS set.
// Checks actual exported MIDI against interpreter measurements and Web Audio scheduling.
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const output = path.resolve(process.argv[2] || path.join(root, 'artifacts/performance'));
const keys = ['harbor', 'bells', 'ode', 'clockwork', 'lanterns'].map(x => `performance-${x}`);

function buffer(channels = 1, length = 22050, sampleRate = 44100) {
    const data = Array.from({ length: channels }, () => Float32Array.from({ length }, (_, i) => Math.sin(i * 2 * Math.PI * 220 / sampleRate) * 0.5));
    return { numberOfChannels: channels, length, sampleRate, duration: length / sampleRate, getChannelData: c => data[c] };
}
class Context {
    constructor() { this.currentTime = 0; this.state = 'running'; this.destination = {}; this.sources = []; this.gains = []; }
    async resume() {}
    createBuffer(channels, length, rate) { return buffer(channels, length, rate); }
    async decodeAudioData() { return buffer(); }
    createGain() {
        const points = [];
        const gain = { gain: { value: 1, setValueAtTime: (v, t) => points.push([v, t]), linearRampToValueAtTime: (v, t) => points.push([v, t]) }, connect() {}, points };
        this.gains.push(gain);
        return gain;
    }
    createBufferSource() {
        const source = { playbackRate: { value: 1 }, connect() {}, start(...args) { this.started = args; }, stop(t) { this.stopped = t; } };
        let assigned;
        Object.defineProperty(source, 'buffer', { get: () => assigned, set: value => {
            assert.equal(assigned, undefined, 'Web Audio source buffers can only be assigned once');
            assigned = value;
        } });
        this.sources.push(source);
        return source;
    }
}
async function main() {
    let schedules = 0;
    for (const key of keys) {
        const scheduled = [];
        const soundfont = { load: async () => {}, playNote: (...args) => { scheduled.push(args); return null; } };
        const context = vm.createContext({ window: { AudioContext: Context }, SoundScriptSoundfont: soundfont,
            TextDecoder, Uint8Array, console, setTimeout: () => 1, clearTimeout() {} });
        vm.runInContext(fs.readFileSync(path.join(root, 'src/SoundScript.Playground/wwwroot/js/midi-player.js'), 'utf8'), context);
        const bytes = fs.readFileSync(path.join(output, `${key}-after.mid`));
        await context.window.startPlayback(bytes);
        const report = JSON.parse(fs.readFileSync(path.join(output, `${key}-after.json`)));
        const expected = report.midi.flatMap(t => t.Notes).sort((a, b) => a.StartSeconds - b.StartSeconds || a.MidiNumber - b.MidiNumber || a.Velocity - b.Velocity);
        scheduled.sort((a, b) => a[2] - b[2] || a[0] - b[0] || a[1] - b[1]);
        assert.equal(scheduled.length, expected.length, `${key}: no notes lost or merged`);
        for (let i = 0; i < expected.length; i++) {
            const actual = scheduled[i], n = expected[i];
            assert.equal(actual[0], n.MidiNumber, `${key}: pitch`);
            assert.ok(Math.abs(actual[2] - 0.05 - n.StartSeconds) < 0.003, `${key}: onset within MIDI tick precision`);
            assert.ok(Math.abs(actual[3] - n.DurationSeconds) < 0.003, `${key}: duration within MIDI tick precision`);
            assert.ok(actual[6], `${key}: envelope survived MIDI serialization`);
        }
        schedules += scheduled.length;
    }

    const context = vm.createContext({ window: {}, fetch: async () => ({ ok: true, arrayBuffer: async () => new ArrayBuffer(8) }) });
    vm.runInContext(fs.readFileSync(path.join(root, 'src/SoundScript.Playground/wwwroot/js/soundfont-loader.js'), 'utf8'), context);
    const audio = new Context();
    const soundfont = context.window.SoundScriptSoundfont;
    await soundfont.load(audio, [73]);
    const performance = { attack: 0.035, release: 0.04, gainEnd: 1.3, evolution: 0.04, sustained: true, connected: true };
    const played = soundfont.playNote(72, 80, 1, 4, {}, 73, performance);
    assert.equal(played.source.loop, true, 'sustained sample must survive its original half-second duration');
    assert.equal(played.source.stopped, 5.04, 'release is scheduled after the entire written sustain');
    assert.equal(played.source.started.length, 2, 'source is not truncated to sample duration');
    const points = played.gain.points;
    assert.equal(points[0][0], 0);
    assert.equal(points.at(-1)[0], 0);
    assert.ok(points.at(-2)[0] > points[1][0], 'continuous crescendo reaches the end of the note');
    assert.ok(points.every((p, i) => Number.isFinite(p[0]) && (i === 0 || p[1] >= points[i - 1][1])), 'envelope scheduling is finite and ordered');
    const legacy = soundfont.playNote(72, 80, 1, 4, {}, 73);
    assert.equal(legacy.source.loop, undefined, 'legacy sample path remains opt-out');
    assert.equal(legacy.source.started.length, 3, 'legacy start duration remains unchanged');
    console.log(`PASS: ${keys.length} MIDI/browser comparisons, ${schedules} scheduled notes; sustained sample, gain envelope, and legacy-path checks.`);
}
main().catch(error => { console.error(error); process.exitCode = 1; });

// No npm packages: node --test scripts/transcription-browser-input.test.cjs
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../src/SoundScript.Playground/wwwroot/js/transcription-input.js'), 'utf8');
function adapter(channels, duration = 1, failure = false) {
    const context = { window: {}, Uint8Array, Float32Array, OfflineAudioContext: class {
        constructor(count, length, rate) { assert.equal(rate, 16000); }
        async decodeAudioData() {
            if (failure) throw new Error('unsupported codec');
            return { duration, length: channels[0].length, numberOfChannels: channels.length, getChannelData: c => channels[c] };
        }
    }};
    vm.runInNewContext(source, context);
    return context.window.SoundScriptTranscription;
}
test('browser adapter returns raw bytes for IJSStreamReference and downmixes stereo', async () => {
    const bytes = await adapter([[.5, -.5], [.25, .25]]).decode(new Uint8Array([1]));
    assert.ok(bytes instanceof Uint8Array);
    assert.deepEqual(Array.from(new Float32Array(bytes.buffer)), [.375, -.125]);
});
test('browser codec errors explain the supported fallback', async () => {
    await assert.rejects(adapter([[0]], 1, true).decode(new Uint8Array([1])), /PCM WAV.*CLI with FFmpeg/);
});
test('decoded duration limit is enforced', async () => {
    await assert.rejects(adapter([[0]], 121).decode(new Uint8Array([1])), /120-second/);
});
test('codec overshoot is bounded before the canonical PCM boundary', async () => {
    const bytes = await adapter([[1.1, -1.1]]).decode(new Uint8Array([1]));
    assert.deepEqual(Array.from(new Float32Array(bytes.buffer)), [1, -1]);
});

test('oversized media exposes duration before analysis and accepts a selected excerpt', async () => {
    const channels = [new Float32Array(121 * 16000).fill(.25), new Float32Array(121 * 16000).fill(.5)];
    const input = adapter(channels, 121);
    const info = await input.inspect(new Uint8Array([1]));
    assert.equal(info.durationSeconds, 121);
    assert.equal(info.channels, 2);
    assert.throws(() => input.excerpt(), /121.000.*120-second/);
    const bytes = input.excerpt(30, 10);
    assert.equal(bytes.byteLength, 10 * 16000 * 4);
    assert.equal(new Float32Array(bytes.buffer)[0], .375);
});
test('invalid, out-of-range and stale excerpts are rejected', async () => {
    const input = adapter([new Float32Array(32000)], 2);
    await input.inspect(new Uint8Array([1]));
    for (const [start, duration] of [[-1, 1], [0, 0], [NaN, 1], [0, Infinity], [1.5, 1]])
        assert.throws(() => input.excerpt(start, duration));
    input.clear();
    assert.throws(() => input.excerpt(0, 1), /Select and decode/);
});
test('selecting unsupported new media clears the previous decoded recording', async () => {
    const input = adapter([[.5]], 1);
    await input.inspect(new Uint8Array([1]));
    await assert.rejects(input.inspect(new Uint8Array(64 * 1024 * 1024 + 1)), /64 MiB/);
    assert.throws(() => input.excerpt(), /Select and decode/);
});

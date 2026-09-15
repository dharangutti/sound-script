// Real Playground smoke test and offline rendering of its actual GM samples.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const output = path.resolve(process.env.SOUNDSCRIPT_PERFORMANCE_ARTIFACTS || 'artifacts/performance');
const keys = ['harbor', 'bells', 'ode', 'clockwork', 'lanterns'].map(x => `performance-${x}`);

(async () => {
    const browser = await chromium.launch({ headless: true, channel: process.env.PLAYWRIGHT_CHANNEL || undefined,
        args: ['--autoplay-policy=no-user-gesture-required', '--mute-audio'] });
    try {
        const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
        const errors = [];
        page.on('pageerror', error => errors.push(error.message));
        await page.goto(process.argv[2] || 'http://127.0.0.1:5193/');
        await page.locator('#main-example-select').waitFor({ timeout: 60000 });
        for (const key of keys) {
            await page.selectOption('#main-example-select', key);
            await page.getByRole('button', { name: 'Run', exact: true }).first().click();
            await page.waitForFunction(() => [...document.querySelectorAll('.status')].some(e => e.textContent.startsWith('Playing')) || document.querySelector('.error pre'), null, { timeout: 60000 });
            const status = await page.locator('.status').first().textContent().catch(() => '');
            assert.ok(status.startsWith('Playing'), `${key}: ${await page.locator('body').innerText()}`);
            console.log(`${key}: real Playground playback passed`);
            await page.getByRole('button', { name: 'Stop', exact: true }).first().click();
        }
        await page.screenshot({ path: path.join(output, 'playground-performance.png'), fullPage: false });
        for (const key of keys) {
            for (const label of ['before', 'after']) {
                const midi = fs.readFileSync(path.join(output, `${key}-${label}.mid`));
                const result = await page.evaluate(async bytes => {
                    // Capture the real MIDI parser's schedule, then use the real sample player offline.
                    const scheduled = [];
                    const load = SoundScriptSoundfont.load, play = SoundScriptSoundfont.playNote;
                    SoundScriptSoundfont.load = async () => {};
                    SoundScriptSoundfont.playNote = (...args) => { scheduled.push(args); return null; };
                    try { await SoundScriptMidi.startPlayback(new Uint8Array(bytes)); }
                    finally { SoundScriptSoundfont.load = load; SoundScriptSoundfont.playNote = play; }
                    SoundScriptMidi.stop();
                    const origin = Math.min(...scheduled.map(n => n[2]));
                    const seconds = Math.max(...scheduled.map(n => n[2] - origin + n[3] + (n[6]?.release || 0))) + 0.1;
                    const context = new OfflineAudioContext(2, Math.ceil(seconds * 44100), 44100);
                    const gain = context.createGain(); gain.gain.value = 0.30; gain.connect(context.destination);
                    await load(context, [...new Set(scheduled.map(n => n[5]))]);
                    for (const n of scheduled) play(n[0], n[1], n[2] - origin, n[3], gain, n[5], n[6]);
                    const pcm = await context.startRendering();
                    const data = new ArrayBuffer(44 + pcm.length * 4);
                    const view = new DataView(data);
                    const text = (offset, value) => [...value].forEach((c, i) => view.setUint8(offset + i, c.charCodeAt(0)));
                    text(0, 'RIFF'); view.setUint32(4, data.byteLength - 8, true); text(8, 'WAVE'); text(12, 'fmt ');
                    view.setUint32(16, 16, true); view.setUint16(20, 1, true); view.setUint16(22, 2, true);
                    view.setUint32(24, 44100, true); view.setUint32(28, 176400, true); view.setUint16(32, 4, true);
                    view.setUint16(34, 16, true); text(36, 'data'); view.setUint32(40, pcm.length * 4, true);
                    let peak = 0, clipped = 0;
                    for (let i = 0; i < pcm.length; i++) for (let c = 0; c < 2; c++) {
                        const sample = pcm.getChannelData(c)[i]; peak = Math.max(peak, Math.abs(sample));
                        if (Math.abs(sample) > 1) clipped++;
                        view.setInt16(44 + (i * 2 + c) * 2, Math.round(Math.max(-1, Math.min(1, sample)) * 32767), true);
                    }
                    const array = new Uint8Array(data);
                    let binary = '';
                    for (let i = 0; i < array.length; i += 16384) binary += String.fromCharCode(...array.subarray(i, i + 16384));
                    return { audio: btoa(binary), peak, clipped, notes: scheduled.length };
                }, [...midi]);
                assert.ok(result.peak > 0.01, `${key}/${label}: audible output energy`);
                assert.equal(result.clipped, 0, `${key}/${label}: no clipped samples`);
                fs.writeFileSync(path.join(output, `${key}-${label}-browser.wav`), Buffer.from(result.audio, 'base64'));
                console.log(`${key}/${label}: ${result.notes} notes, peak ${result.peak.toFixed(4)}, no clipping`);
            }
        }
        assert.deepEqual(errors, []);
        console.log('PASS: five real Playground presets and ten browser-sample audio renders. Listening acceptance remains a separate check.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });

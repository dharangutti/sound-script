// Release-browser acceptance: mode dispatch, CLI parity, stale results and playback.
// Usage: node scripts/verify-transcription-playground.cjs artifacts/melody-playground
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const assert = require('node:assert/strict');
const { execFileSync } = require('node:child_process');
const cli = require('./cli-build-path.cjs');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const root = path.resolve(process.argv[2] || 'artifacts/melody-playground');
const output = path.resolve('artifacts/melody-browser');
fs.mkdirSync(output, { recursive: true });
const types = { '.html':'text/html', '.js':'text/javascript', '.wasm':'application/wasm', '.css':'text/css', '.json':'application/json' };
const server = http.createServer((req, res) => {
    const url = new URL(req.url, 'http://localhost');
    const file = path.resolve(root, '.' + (url.pathname === '/' ? '/index.html' : decodeURIComponent(url.pathname)));
    if (!file.startsWith(root + path.sep)) { res.writeHead(403); res.end(); return; }
    fs.readFile(file, (err, bytes) => { res.writeHead(err ? 404 : 200, { 'Content-Type': types[path.extname(file)] || 'application/octet-stream' }); res.end(err ? 'Not found' : bytes); });
});
function fixture(name, equal = false, seconds = 2) {
    const rate = 16000, count = rate * seconds, data = Buffer.alloc(44 + count * 2);
    data.write('RIFF'); data.writeUInt32LE(data.length - 8, 4); data.write('WAVEfmt ', 8);
    data.writeUInt32LE(16, 16); data.writeUInt16LE(1, 20); data.writeUInt16LE(1, 22);
    data.writeUInt32LE(rate, 24); data.writeUInt32LE(rate * 2, 28); data.writeUInt16LE(2, 32); data.writeUInt16LE(16, 34);
    data.write('data', 36); data.writeUInt32LE(count * 2, 40);
    for (let i = 0; i < count; i++) {
        const t = i / rate, local = t % .5, pitch = [72, 76, 79, 74][Math.floor(t * 2) % 4];
        const tone = p => Math.sin(2 * Math.PI * 440 * 2 ** ((p - 69) / 12) * t);
        const value = (equal ? .3 * tone(pitch) + .3 * tone(61) : .4 * tone(pitch) + .1 * tone(pitch + 12) + .08 * tone(48) + .07 * tone(55)) * Math.min(1, local / .01) * Math.exp(-local * 2);
        data.writeInt16LE(Math.round(value * 32767), 44 + i * 2);
    }
    const file = path.join(output, name + '.wav'); fs.writeFileSync(file, data); return file;
}
(async () => {
    const dominant = fixture('dominant'), equal = fixture('equal', true), long = fixture('long', false, 30);
    const cliSource = path.join(output, 'cli.ss');
    execFileSync('dotnet', [cli, 'transcribe', dominant,
        '--mode', 'extract-melody', '--tempo', '120', '--out', cliSource], { stdio: 'pipe', timeout: 120000 });
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_PATH || undefined });
    try {
        const page = await browser.newPage(); const errors = [];
        page.on('pageerror', e => errors.push(e.message));
        await page.goto(`http://127.0.0.1:${server.address().port}`);
        await page.locator('#transcription-workspace-tab').click({ timeout: 60000 });
        const analyze = page.getByRole('button', { name: 'Analyze / Transcribe', exact: true });
        const exportSource = page.getByRole('button', { name: 'Export SoundScript', exact: true });
        const play = page.getByRole('button', { name: 'Play edited source', exact: true });
        const ready = () => page.waitForFunction(() => document.querySelector('.transcription-workspace [role="status"]').textContent.includes('Media ready'));
        const finish = () => page.waitForFunction(() => !document.querySelector('#transcription-file').disabled, null, { timeout: 60000 });
        await page.locator('#transcription-file').setInputFiles(dominant); await ready();
        assert.equal(await page.locator('#transcription-mode').inputValue(), 'Monophonic');
        await page.locator('#transcription-mode').selectOption('ExtractMelody');
        await page.locator('#transcription-tempo').fill('120'); await page.locator('#transcription-tempo').blur();
        await analyze.click(); await finish();
        assert.equal(await exportSource.isEnabled(), true);
        assert.equal(await page.locator('#transcription-source').inputValue(), fs.readFileSync(cliSource, 'utf8'));
        assert.match(await page.locator('.transcription-workspace').innerText(), /4 extracted notes/);
        await page.locator('#transcription-original').evaluate(async audio => { await audio.play(); });
        await play.click(); await finish();
        assert.equal(await page.locator('#transcription-original').evaluate(audio => audio.paused), true);
        await page.getByRole('button', { name: 'Stop', exact: true }).click();
        for (const [selector, value] of [['#transcription-mode', 'Monophonic'], ['#transcription-instrument', '0']]) {
            await page.locator(selector).selectOption(value);
            assert.equal(await exportSource.isEnabled(), false);
            await page.locator('#transcription-mode').selectOption('ExtractMelody');
            await analyze.click(); await finish(); assert.equal(await exportSource.isEnabled(), true);
        }
        await page.locator('#transcription-start').fill('0.1'); await page.locator('#transcription-start').blur();
        assert.equal(await exportSource.isEnabled(), false);
        await page.locator('#transcription-file').setInputFiles(equal); await ready();
        await analyze.click(); await finish();
        assert.equal(await exportSource.isEnabled(), false);
        assert.match(await page.locator('.transcription-workspace [role="status"]').innerText(), /rejected/i);
        assert.equal(await page.getByRole('button', { name: 'Export analysis', exact: true }).isEnabled(), true);
        await page.locator('#transcription-file').setInputFiles(long); await ready();
        await analyze.click(); await page.getByRole('button', { name: 'Cancel analysis', exact: true }).click(); await finish();
        assert.match(await page.locator('.transcription-workspace [role="status"]').innerText(), /cancelled/i);
        assert.equal(await exportSource.isEnabled(), false);
        await page.setViewportSize({ width: 390, height: 844 });
        await page.screenshot({ path: path.join(output, 'mobile.png'), fullPage: true });
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), true);
        assert.deepEqual(errors, []);
        console.log('PASS extraction browser/CLI parity, original playback, option/excerpt invalidation, rejection, cancellation and mobile layout');
    } finally { await browser.close(); server.close(); }
})().catch(e => { console.error(e); server.close(); process.exitCode = 1; });

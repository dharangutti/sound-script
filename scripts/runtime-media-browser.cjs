// Serve and exercise a local Blazor WebAssembly publish:
//   node scripts/runtime-media-browser.cjs <publish-directory>
// Defaults to artifacts/playground. Set PLAYWRIGHT_MODULE or CHROMIUM_PATH when needed.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs');
const http = require('node:http');
const path = require('node:path');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');

const root = path.resolve(process.argv[2] || 'artifacts/playground');
const contentTypes = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.wasm': 'application/wasm',
  '.css': 'text/css', '.json': 'application/json', '.wav': 'audio/wav', '.png': 'image/png', '.svg': 'image/svg+xml' };
const server = http.createServer((request, response) => {
  const url = new URL(request.url, 'http://localhost');
  const file = path.resolve(root, '.' + (url.pathname === '/' ? '/index.html' : decodeURIComponent(url.pathname)));
  if (file !== root && !file.startsWith(root + path.sep)) { response.writeHead(403); response.end(); return; }
  fs.readFile(file, (error, bytes) => {
    response.writeHead(error ? 404 : 200, { 'Content-Type': contentTypes[path.extname(file)] || 'application/octet-stream' });
    response.end(error ? 'Not found' : bytes);
  });
});
const digest = bytes => crypto.createHash('sha256').update(bytes).digest('hex');

async function snapshot(page) {
  return page.locator('[data-testid="runtime-audio"]').evaluate(audio => {
    const prefix = 'data:audio/wav;base64,';
    if (!audio.src.startsWith(prefix)) throw new Error('Runtime output is not a WAV data URL.');
    const svg = document.querySelector('[data-testid="runtime-scene"] svg');
    const indicator = svg?.querySelector('g[data-name="indicator"]');
    if (!indicator) throw new Error('Rendered indicator is missing from the runtime SVG.');
    const shape = indicator.querySelector('ellipse, rect, path');
    if (!shape) throw new Error('Rendered indicator geometry is missing from the runtime SVG.');
    let x, width;
    if (shape.localName === 'path') {
      const bounds = shape.getBBox();
      x = bounds.x + bounds.width / 2;
      width = bounds.width;
    } else if (shape.localName === 'ellipse') {
      x = Number(shape.getAttribute('cx'));
      width = Number(shape.getAttribute('rx')) * 2;
    } else {
      width = Number(shape.getAttribute('width'));
      x = Number(shape.getAttribute('x')) + width / 2;
    }
    return {
      wavBase64: audio.src.slice(prefix.length),
      svg: svg.outerHTML,
      opacity: Number(indicator.getAttribute('opacity')),
      x,
      width
    };
  }).then(value => ({
    wavHash: digest(Buffer.from(value.wavBase64, 'base64')),
    svg: value.svg,
    opacity: value.opacity,
    x: value.x,
    width: value.width
  }));
}

async function waitForRevision(page, previous) {
  await page.waitForFunction(revision => {
    const status = document.querySelector('[data-testid="runtime-status"]');
    return status && Number(status.textContent.match(/snapshot (\d+)/)?.[1]) > revision;
  }, previous, { timeout: 30000 });
}

(async () => {
  if (!fs.existsSync(path.join(root, 'index.html'))) throw new Error(`Published Playground index not found under ${root}`);
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const baseUrl = `http://127.0.0.1:${server.address().port}/`;
  const browser = await chromium.launch({
    headless: true,
    executablePath: process.env.CHROMIUM_PATH || undefined,
    ...(process.env.CHROMIUM_CHANNEL ? { channel: process.env.CHROMIUM_CHANNEL } : {}),
    args: ['--autoplay-policy=no-user-gesture-required']
  });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 1100 } });
    const pageErrors = [];
    const corpusRequests = [];
    page.on('request', request => { if (/SoundScript\.Wordbank\.Corpus[^/]*\.wasm/.test(request.url())) corpusRequests.push(request.url()); });
    page.on('pageerror', error => pageErrors.push(error.message));
    // Exercise re-entrant renders while the first interop module import is pending.
    let releaseClockModule;
    const clockModuleReady = new Promise(resolve => { releaseClockModule = resolve; });
    await page.route('**/runtime-media.js', async route => {
      await clockModuleReady;
      await route.continue();
    });
    await page.goto(baseUrl);
    await page.locator('#visual-workspace-tab').waitFor({ timeout: 90000 });
    await page.locator('#visual-workspace-tab').click();
    await page.locator('[data-testid="runtime-source"]').waitFor({ timeout: 30000 });
    await page.getByTestId('runtime-audio').waitFor({ timeout: 30000 });
    assert.equal(await page.getByTestId('runtime-normal').count(), 1, 'monitoring demo is ready without compiling manually');
    await page.getByTestId('runtime-example').click();
    await page.getByTestId('runtime-compile').click();
    releaseClockModule();
    await page.getByTestId('runtime-status').waitFor({ timeout: 30000 });
    await page.getByTestId('runtime-audio').waitFor();

    assert.equal(await page.getByTestId('runtime-value-intensity').inputValue(), '0.25');
    assert.equal(await page.getByTestId('runtime-value-xpos').inputValue(), '200');
    const labels = await page.getByTestId('runtime-controls').locator('label').allTextContents();
    assert.ok(labels.some(label => label.includes('intensity (0 to 1)')), labels.join('\n'));
    assert.ok(labels.some(label => label.includes('xpos (-12800 to 12800)')), labels.join('\n'));
    const parseCount = await page.getByTestId('runtime-status').getAttribute('data-parse-count');
    assert.equal(parseCount, '1');
    const baseline = await snapshot(page);
    assert.ok(!(await page.getByTestId('runtime-panel').innerText()).includes('does not animate'));
    assert.equal(baseline.opacity, 0.25);
    assert.equal(baseline.x, 200);
    assert.equal(baseline.width, 70, 'indicator must render at its declared width');
    await page.getByTestId('runtime-audio').evaluate(audio => audio.play());
    await page.waitForFunction(() => Number(document.querySelector('[data-testid="runtime-scene"]').dataset.time) > 0.3);
    assert.ok((await snapshot(page)).width > baseline.width, 'indicator grows during playback');
    await page.getByTestId('runtime-audio').evaluate(audio => audio.pause());
    await page.waitForFunction(() => Math.abs(Number(document.querySelector('[data-testid="runtime-scene"]').dataset.time) -
      document.querySelector('[data-testid="runtime-audio"]').currentTime) < 0.001);
    const paused = await page.getByTestId('runtime-scene').innerHTML();
    await page.waitForTimeout(200);
    assert.equal(await page.getByTestId('runtime-scene').innerHTML(), paused, 'pause freezes the scene');
    await page.getByTestId('runtime-audio').evaluate(audio => { audio.currentTime = 1; });
    await page.waitForFunction(() => Number(document.querySelector('[data-testid="runtime-scene"]').dataset.time) === 1);
    assert.equal((await snapshot(page)).width, 120, 'seek evaluates SceneAt(1)');
    assert.equal((await snapshot(page)).x, 200, 'animation preserves runtime position');
    await page.getByTestId('runtime-audio').evaluate(audio => audio.play());
    await page.waitForFunction(() => Number(document.querySelector('[data-testid="runtime-scene"]').dataset.time) > 1.1);
    await page.waitForFunction(() => document.querySelector('[data-testid="runtime-audio"]').ended);
    await page.waitForFunction(() => Math.abs(Number(document.querySelector('[data-testid="runtime-scene"]').dataset.time) -
      document.querySelector('[data-testid="runtime-audio"]').currentTime) < 0.001);
    await page.getByTestId('runtime-audio').evaluate(audio => audio.play());
    await page.waitForFunction(() => Number(document.querySelector('[data-testid="runtime-scene"]').dataset.time) < 0.5);

    await page.getByTestId('runtime-value-intensity').fill('0.9');
    await page.getByTestId('runtime-value-xpos').fill('900');
    await page.getByTestId('runtime-apply').click();
    await waitForRevision(page, 0);
    const critical = await snapshot(page);
    assert.equal(await page.getByTestId('runtime-audio').evaluate(audio => audio.paused), true,
      'applying a new snapshot stops old playback and waits for Play');
    assert.notEqual(critical.wavHash, baseline.wavHash, 'changing gain must change rendered WAV bytes');
    assert.equal(critical.opacity, 0.9, 'intensity must update SVG opacity');
    assert.equal(critical.x, 900, 'xpos must move the indicator center');
    assert.equal(await page.getByTestId('runtime-status').getAttribute('data-parse-count'), parseCount,
      'parameter updates must reuse the compiled runtime');

    await page.getByTestId('runtime-reset').click();
    await waitForRevision(page, 1);
    const reset = await snapshot(page);
    assert.equal(reset.wavHash, baseline.wavHash, 'reset must restore byte-identical WAV output');
    assert.equal(reset.svg, baseline.svg, 'reset must restore byte-identical SVG output');

    await page.getByTestId('runtime-warning').click();
    await waitForRevision(page, 2);
    const warning = await snapshot(page);
    assert.equal(warning.x, 640);
    assert.equal(warning.opacity, 0.55);
    await page.getByTestId('runtime-critical').click();
    await waitForRevision(page, 3);
    const presetCritical = await snapshot(page);
    assert.equal(presetCritical.x, 1080);
    assert.equal(presetCritical.opacity, 0.9);
    assert.notEqual(presetCritical.wavHash, warning.wavHash);
    assert.equal(await page.getByTestId('runtime-value-intensity').inputValue(), '0.9');
    assert.equal(await page.getByTestId('runtime-status').getAttribute('data-parse-count'), '1');
    await page.getByTestId('runtime-normal').click();
    await waitForRevision(page, 4);
    assert.deepEqual(await snapshot(page), reset, 'Normal restores the original scene and WAV');

    await page.getByTestId('runtime-value-intensity').fill('1.1');
    await page.getByTestId('runtime-apply').click();
    await page.getByTestId('runtime-error').waitFor();
    assert.match(await page.getByTestId('runtime-error').innerText(), /expected/i);
    const afterInvalid = await snapshot(page);
    assert.equal(afterInvalid.wavHash, reset.wavHash, 'invalid input must preserve the last valid WAV');
    assert.equal(afterInvalid.svg, reset.svg, 'invalid input must preserve the last valid SVG');

    const source = await page.getByTestId('runtime-source').inputValue();
    await page.getByTestId('runtime-source').fill(source.replace('tempo 120', 'tempo 121'));
    await page.waitForFunction(() => !document.querySelector('[data-testid="runtime-controls"]')
      && !document.querySelector('[data-testid="runtime-audio"]'));
    await page.getByTestId('runtime-compile').click();
    await page.getByTestId('runtime-status').waitFor({ timeout: 30000 });
    await page.getByTestId('runtime-audio').waitFor();
    const recompiled = await snapshot(page);
    assert.notEqual(recompiled.wavHash, reset.wavHash, 'compiling edited source must replace prior output');
    assert.equal(await page.getByTestId('runtime-status').getAttribute('data-parse-count'), '1');
    const responsive = [];
    fs.mkdirSync('artifacts/v16-dx-browser', { recursive: true });
    for (const [width, height] of [[1440, 1000], [1024, 768], [390, 844]]) {
      await page.setViewportSize({ width, height });
      const sourceBox = await page.getByTestId('runtime-source').boundingBox();
      assert.ok(sourceBox.width > 200 && sourceBox.height >= 300, 'source remains a usable editor');
      assert.ok(sourceBox.x >= 0 && sourceBox.x + sourceBox.width <= width + 1, 'editor fits viewport');
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'workspace must not scroll sideways');
      await page.getByTestId('runtime-apply').scrollIntoViewIfNeeded();
      const button = await page.getByTestId('runtime-apply').boundingBox();
      assert.ok(button.x >= 0 && button.x + button.width <= width, 'Apply remains reachable');
      await page.getByTestId('runtime-value-intensity').focus();
      assert.equal(await page.getByTestId('runtime-value-intensity').evaluate(e => e === document.activeElement), true);
      await page.screenshot({ path: `artifacts/v16-dx-browser/runtime-${width}.png`, fullPage: true });
      responsive.push({ width, height, editorWidth: sourceBox.width, editorHeight: sourceBox.height });
    }
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.getByTestId('runtime-geometry').click();
    await page.getByTestId('runtime-value-angle').waitFor();
    assert.equal(await page.getByTestId('runtime-controls').locator('input').count(), 7);
    assert.equal(await page.getByTestId('runtime-critical').count(), 0, 'monitoring presets must not apply to other source');
    const geometryBefore = await page.getByTestId('runtime-scene').innerHTML();
    for (const [name, value] of Object.entries({ volume: '0.7', xpos: '800', ypos: '250', width: '320', height: '180', angle: '45', opacity: '0.6' })) {
      await page.getByTestId(`runtime-value-${name}`).fill(value);
    }
    await page.getByTestId('runtime-apply').click();
    await waitForRevision(page, 0);
    const geometryAfter = await page.getByTestId('runtime-scene').innerHTML();
    assert.notEqual(geometryBefore, geometryAfter);
    const tile = await page.getByTestId('runtime-scene').locator('g[data-name="tile"]').evaluate(group => {
      const box = group.getBBox();
      return { opacity: Number(group.getAttribute('opacity')), x: box.x + box.width / 2, y: box.y + box.height / 2,
        width: box.width, height: box.height };
    });
    assert.equal(tile.opacity, 0.6);
    assert.ok(Math.abs(tile.x - 800) < 0.01 && Math.abs(tile.y - 250) < 0.01);
    assert.ok(Math.abs(tile.width - 500 / Math.sqrt(2)) < 0.01 && Math.abs(tile.height - tile.width) < 0.01,
      'rectangle dimensions must be rotated 45 degrees');
    assert.equal(await page.getByTestId('runtime-status').getAttribute('data-parse-count'), '1');
    await page.getByTestId('runtime-reset').click();
    await waitForRevision(page, 1);
    assert.equal(await page.getByTestId('runtime-scene').innerHTML(), geometryBefore);
    await page.getByTestId('runtime-source').fill('track cue { C4 q }');
    await page.getByTestId('runtime-compile').click();
    await page.getByTestId('runtime-empty').waitFor();
    assert.equal(await page.getByTestId('runtime-controls').count(), 0, 'static programs must not show empty parameter controls');
    assert.equal(await page.getByTestId('runtime-audio').count(), 1);
    await page.locator('#music-workspace-tab').click();
    await page.getByLabel('Enable In-Browser Vocal Engine').check();
    await page.locator('.vocal-toggle-status.vocal-on').waitFor({ timeout: 30000 });
    assert.ok(await page.locator('#main-example-select option').count() > 15, 'existing examples remain available');
    await page.selectOption('#main-example-select', 'core-melody');
    await page.getByRole('button', { name: 'Run', exact: true }).first().click();
    await page.waitForFunction(() => [...document.querySelectorAll('.status')].some(e => e.textContent.startsWith('Playing')), null, { timeout: 30000 });
    const midiDownload = page.waitForEvent('download');
    await page.getByRole('button', { name: 'Download MIDI', exact: true }).click();
    await (await midiDownload).saveAs('artifacts/v16-dx-browser/music.mid');
    assert.equal(fs.readFileSync('artifacts/v16-dx-browser/music.mid').subarray(0, 4).toString(), 'MThd');
    await page.getByRole('button', { name: 'Stop', exact: true }).first().click();
    await page.selectOption('#wave-example-select', 'wave-speak');
    await page.locator('.studio').getByRole('button', { name: '▶ Play', exact: true }).click();
    const waveButton = page.locator('.studio').getByRole('button', { name: 'Download WAV', exact: true });
    await page.waitForFunction(() => [...document.querySelectorAll('.studio button')].some(e => e.textContent.includes('Download WAV') && !e.disabled), null, { timeout: 60000 });
    const waveDownload = page.waitForEvent('download');
    await waveButton.click();
    await (await waveDownload).saveAs('artifacts/v16-dx-browser/vocal.wav');
    assert.equal(fs.readFileSync('artifacts/v16-dx-browser/vocal.wav').subarray(0, 4).toString(), 'RIFF');
    await page.locator('.studio').getByRole('button', { name: 'Stop', exact: true }).click();
    await page.locator('#visual-workspace-tab').click();
    assert.equal(await page.locator('#visual-timeline-source').count(), 1, 'existing temporal editor remains available');
    assert.deepEqual(pageErrors, [], 'Blazor page must not report browser exceptions');
    assert.deepEqual(corpusRequests, [], 'Corpus WAV assembly must remain lazy during ordinary/runtime startup');

    console.log(JSON.stringify({
      url: baseUrl,
      browser: 'Chromium / Blazor WebAssembly',
      parseCount,
      wavHashes: { normal: baseline.wavHash, critical: critical.wavHash, recompiled: recompiled.wavHash },
      visual: { normal: { x: baseline.x, width: baseline.width, opacity: baseline.opacity }, critical: { x: critical.x, width: critical.width, opacity: critical.opacity } },
      assertions: ['delayed clock module initialization', 'audio-clock animation', 'pause and seek synchronization', 'resume, end and replay',
        'ready-to-use demo', 'monitoring state presets', 'all visual parameter targets', 'playback resets on new WAV',
        'parameter metadata', 'gain changes WAV', 'xpos and intensity update SVG', 'reset restores exact outputs',
        'invalid input preserves outputs and reports error', 'source edit clears and recompiles runtime'],
      pageErrors, corpusRequests, responsive
    }, null, 2));
  } finally {
    await browser.close();
    server.close();
  }
})().catch(error => {
  console.error(error);
  server.close();
  process.exitCode = 1;
});

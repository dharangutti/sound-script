// Serve and exercise the real local Blazor WebAssembly publish:
//   node scripts/runtime-media-browser.cjs artifacts/playground
// Set PLAYWRIGHT_MODULE or CHROMIUM_PATH when Playwright/browser lives elsewhere.
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
    const shape = indicator.querySelector('ellipse, rect');
    return {
      wavBase64: audio.src.slice(prefix.length),
      svg: svg.outerHTML,
      opacity: Number(indicator.getAttribute('opacity')),
      x: Number(shape.getAttribute(shape.localName === 'ellipse' ? 'cx' : 'x'))
    };
  }).then(value => ({
    wavHash: digest(Buffer.from(value.wavBase64, 'base64')),
    svg: value.svg,
    opacity: value.opacity,
    x: value.x
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
    page.on('pageerror', error => pageErrors.push(error.message));
    await page.goto(baseUrl);
    await page.locator('#visual-workspace-tab').waitFor({ timeout: 90000 });
    await page.locator('#visual-workspace-tab').click();
    await page.locator('[data-testid="runtime-source"]').waitFor({ timeout: 30000 });
    await page.getByTestId('runtime-example').click();
    await page.getByTestId('runtime-compile').click();
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
    assert.equal(baseline.opacity, 0.25);
    assert.equal(baseline.x, 200);

    await page.getByTestId('runtime-value-intensity').fill('0.8');
    await page.getByTestId('runtime-value-xpos').fill('900');
    await page.getByTestId('runtime-apply').click();
    await waitForRevision(page, 0);
    const critical = await snapshot(page);
    assert.notEqual(critical.wavHash, baseline.wavHash, 'changing gain must change rendered WAV bytes');
    assert.equal(critical.opacity, 0.8, 'intensity must update SVG opacity');
    assert.equal(critical.x, 900, 'xpos must move the indicator center');
    assert.equal(await page.getByTestId('runtime-status').getAttribute('data-parse-count'), parseCount,
      'parameter updates must reuse the compiled runtime');

    await page.getByTestId('runtime-reset').click();
    await waitForRevision(page, 1);
    const reset = await snapshot(page);
    assert.equal(reset.wavHash, baseline.wavHash, 'reset must restore byte-identical WAV output');
    assert.equal(reset.svg, baseline.svg, 'reset must restore byte-identical SVG output');

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
    assert.deepEqual(pageErrors, [], 'Blazor page must not report browser exceptions');

    console.log(JSON.stringify({
      url: baseUrl,
      browser: 'Chromium / Blazor WebAssembly',
      parseCount,
      wavHashes: { normal: baseline.wavHash, critical: critical.wavHash, recompiled: recompiled.wavHash },
      visual: { normal: { x: baseline.x, opacity: baseline.opacity }, critical: { x: critical.x, opacity: critical.opacity } },
      assertions: ['parameter metadata', 'gain changes WAV', 'xpos and intensity update SVG', 'reset restores exact outputs',
        'invalid input preserves outputs and reports error', 'source edit clears and recompiles runtime'],
      pageErrors
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

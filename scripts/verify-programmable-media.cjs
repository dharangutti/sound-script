// Run against the package-backed ASP.NET sample. No simulated playback clock.
const assert = require('node:assert/strict');
const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch({headless: true, channel: process.env.CHROMIUM_CHANNEL || 'msedge'});
  try {
    const page = await browser.newPage({viewport: {width: 1000, height: 800}});
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    const base = process.argv[2] || 'http://127.0.0.1:5198';
    await page.goto(base);
    await page.waitForFunction(() => document.querySelector('#audio').readyState >= 2);
    await page.click('#play');
    await page.waitForFunction(() => document.querySelector('#audio').currentTime > .25);
    await page.click('#pause');
    const paused = await page.$eval('#audio', a => a.currentTime);
    await page.waitForFunction(t => Math.abs(Number(document.querySelector('#scene').dataset.time) - t) < .02, paused);
    assert.equal(await page.$eval('#audio', a => a.paused), true);
    await page.click('#play');
    await page.waitForFunction(t => document.querySelector('#audio').currentTime > t + .15, paused);
    await page.click('#pause');
    await page.$eval('#audio', a => { a.currentTime = 2; });
    await page.waitForFunction(() => Number(document.querySelector('#scene').dataset.time) === 2);
    const json = await (await page.request.get(base + '/api/scene?scenario=healthy&t=2')).json();
    assert.equal(json.timeSeconds, 2);
    assert.equal(json.primitives[0].left + json.primitives[0].width / 2, 640);
    await page.click('#restart');
    await page.waitForFunction(() => document.querySelector('#audio').currentTime < 1 && !document.querySelector('#audio').paused);
    await page.click('#pause');
    const wav = new Set(), colors = new Set();
    for (const scenario of ['Healthy', 'Warning', 'Critical']) {
      await page.selectOption('#scenario', {label: scenario});
      await page.waitForFunction(() => document.querySelector('#audio').readyState >= 2 && Number(document.querySelector('#scene').dataset.time) === 0);
      const data = await (await page.request.get(`${base}/api/scene?scenario=${scenario.toLowerCase()}&t=1`)).json();
      colors.add(data.primitives[0].paths[0].fill);
      const bytes = await (await page.request.get(`${base}/api/audio?scenario=${scenario.toLowerCase()}`)).body();
      wav.add(require('node:crypto').createHash('sha256').update(bytes).digest('hex'));
    }
    assert.equal(wav.size, 3); assert.equal(colors.size, 3);
    await page.setViewportSize({width: 360, height: 740});
    assert.ok(await page.$eval('#scene', img => img.getBoundingClientRect().width <= innerWidth));
    for (const query of ['t=-1', 't=NaN', 't=1e100', 't=0&scenario=invalid'])
      assert.equal((await page.request.get(`${base}/api/scene?${query}`)).status(), 400);
    assert.deepEqual((await (await page.request.get(base + '/api/scene?t=5')).json()).primitives, []);
    assert.deepEqual(errors, []);
    assert.equal(await page.locator('#error').innerText(), '');
    const runtimeInfo = await (await page.request.get(base + '/api/runtime/info')).json();
    assert.equal(runtimeInfo.statistics.parses, 1);
    assert.equal(runtimeInfo.parameters.length, 2);
    const sha = bytes => require('node:crypto').createHash('sha256').update(bytes).digest('hex');
    const normal = await (await page.request.get(base + '/api/runtime/audio?intensity=0.25&xpos=200')).body();
    const critical = await (await page.request.get(base + '/api/runtime/audio?intensity=0.9&xpos=900')).body();
    assert.notEqual(sha(normal), sha(critical));
    const normalAgain = await (await page.request.get(base + '/api/runtime/audio?intensity=0.25&xpos=200')).body();
    assert.equal(sha(normalAgain), sha(normal));
    assert.equal((await page.request.get(base + '/api/runtime/audio?intensity=2&xpos=900')).status(), 400);
    await page.fill('#runtime-intensity', '0.9'); await page.fill('#runtime-xpos', '900');
    await page.click('#runtime-apply');
    await page.waitForFunction(() => document.querySelector('#runtime-status').value.includes('Applied intensity 0.9'));
    assert.equal(await page.locator('#runtime-error').innerText(), '');
    assert.equal((await (await page.request.get(base + '/api/runtime/info')).json()).statistics.parses, 1);
    console.log(`PASS: runtime metadata, WAV adaptation/reset, invalid values, UI apply; hashes normal=${sha(normal)} critical=${sha(critical)}`);
    console.log('PASS: play, pause, resume, restart, seek, scene synchronization, 3 scenarios, responsive scaling, invalid input, after-end state');
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });

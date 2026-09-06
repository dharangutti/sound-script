// Smoke-test the real compiled Playground UI, including the Wave-only bridge.
const fs=require('node:fs');
const path=require('node:path');
const assert=require('node:assert/strict');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const directory=path.resolve(process.env.SOUNDSCRIPT_AV_ARTIFACTS || path.join(__dirname,'../obj/av-verification'));
(async()=>{
  const browser=await chromium.launch({headless:true,executablePath:process.env.CHROMIUM_PATH || undefined,args:['--autoplay-policy=no-user-gesture-required']});
  try {
    const page=await browser.newPage({viewport:{width:1440,height:1100},acceptDownloads:true});
    const errors=[];page.on('pageerror',e=>errors.push(e.message));
    await page.goto((process.argv[2] || 'http://127.0.0.1:5191/')+'#visual-workspace-tab');
    await page.locator('#visual-example-select').waitFor({timeout:60000});
    const keys=await page.locator('#visual-example-select option').evaluateAll(options=>options.map(o=>o.value));
    assert.equal(keys.length,28);
    for(const key of keys.slice(8)) {
      await page.selectOption('#visual-example-select',key);
      await page.waitForFunction(()=>document.querySelector('.visual-status')?.textContent.includes('Compiled'));
      assert.equal(await page.locator('.visual-error').count(),0,key);
      await page.getByRole('spinbutton',{name:'Timeline time in seconds'}).fill('4');
      await page.waitForFunction(()=>document.querySelector('.visual-time-readout')?.textContent.includes('4s'));
      await page.locator('.visual-stage-canvas').screenshot({path:path.join(directory,key+'.playground.png')});
    }
    await page.selectOption('#visual-example-select','visual-mixed-audio');
    await page.getByRole('spinbutton',{name:'Timeline time in seconds'}).fill('0.2');
    await page.getByRole('button',{name:/Resume/}).click();
    await page.waitForFunction(()=>document.querySelector('.visual-status')?.textContent.includes('Playing'));
    await new Promise(resolve=>setTimeout(resolve,500));
    await page.getByRole('button',{name:/Pause/}).click();
    await page.waitForFunction(()=>document.querySelector('.visual-status')?.textContent.includes('Paused'));
    const paused=Number(await page.locator('.visual-time-input').inputValue());
    await new Promise(resolve=>setTimeout(resolve,250));
    assert.equal(Number(await page.locator('.visual-time-input').inputValue()),paused);
    await page.getByRole('button',{name:/Resume/}).click();
    await page.waitForFunction(paused=>{
      const time=Number(document.querySelector('.visual-time-input').value);
      const button=[...document.querySelectorAll('button')].find(b=>b.textContent.includes('Pause'));
      if(time>paused+0.15 && time<5.8 && button && !button.disabled){button.click();return true;}
      return false;
    },paused,{timeout:20000});
    await page.waitForFunction(()=>document.querySelector('.visual-status')?.textContent.includes('Paused'));
    await page.getByRole('button',{name:/Restart/}).click();
    await page.waitForFunction(()=>Number(document.querySelector('.visual-time-input').value)===0);
    await page.selectOption('#visual-export-fps','24');
    const pending=page.waitForEvent('download',{timeout:60000});
    await page.getByRole('button',{name:'Export Clip',exact:true}).click();
    await (await pending).saveAs(path.join(directory,'mixed-audio.playground.webm'));
    assert.deepEqual(errors,[]);
    fs.writeFileSync(path.join(directory,'playground.results.json'),JSON.stringify({examples:keys.length,newExamples:20,pauseResume:true,scrub:true,restart:true,waveExport:true,errors},null,2));
    console.log('Playground: all 28 presets discovered; 20 compiled and scrubbed; mixed Wave/Voice paused, resumed, restarted and exported.');
  } finally {await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

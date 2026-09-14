// Optional integration capture: compile the fixtures in the real Playground,
// scrub its time input, and compare its native canvas with the reference images.
const fs=require('node:fs'), path=require('node:path'), assert=require('node:assert/strict');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const sharp=require('sharp');
const root=path.resolve(__dirname,'..');
const directory=path.resolve(process.argv[3] || path.join(root,'obj/visual-parity'));
(async()=>{
    const browser=await chromium.launch({headless:true,channel:process.env.CHROMIUM_CHANNEL || 'msedge',executablePath:process.env.CHROMIUM_PATH || undefined});
    try {
        const page=await browser.newPage({viewport:{width:1440,height:1100}});
        const errors=[]; page.on('pageerror',e=>errors.push(e.message));
        // Install before Blazor resolves and caches the JS interop function.
        await page.addInitScript(()=>{
            let renderer;
            Object.defineProperty(window,'SoundScriptVisualRenderer',{
                get:()=>renderer,
                set:value=>{
                    const render=value.renderScene;
                    value.renderScene=(canvas,scene)=>{
                        render(canvas,scene); window.parityScene=scene; window.parityCapture=canvas.toDataURL('image/png');
                    };
                    renderer=value;
                }
            });
        });
        await page.goto((process.argv[2] || 'http://127.0.0.1:5194/')+'#visual-workspace-tab');
        await page.locator('#visual-timeline-source').waitFor({timeout:60000});
        const cases=JSON.parse(fs.readFileSync(path.join(directory,'after.timings.json'))).filter(c=>c.Width===1280);
        for(const item of cases) {
            await page.locator('#visual-timeline-source').fill(fs.readFileSync(path.join(root,item.Source),'utf8'));
            await page.evaluate(()=>{window.parityScene=null;});
            await page.getByRole('button',{name:'Evaluate timeline',exact:true}).click();
            await page.waitForFunction(()=>window.parityScene!==null);
            assert.equal(await page.locator('.visual-error').count(),0,item.Name);
            await page.getByRole('spinbutton',{name:'Timeline time in seconds'}).fill(String(item.Time));
            await page.waitForFunction(time=>window.parityScene.timeSeconds===time,item.Time);
            const capture=await page.evaluate(()=>({scene:window.parityScene,image:window.parityCapture}));
            const expectedScene=JSON.parse(fs.readFileSync(path.join(directory,item.Name+'.scene.json')));
            // Native and WASM trig can differ in the last bit; preserve every
            // field/order while allowing <1e-9 logical pixel coordinate error.
            const normalize=value=>JSON.parse(JSON.stringify(value,(_,v)=>typeof v==='number'?Math.round(v*1e9)/1e9:v));
            assert.deepEqual(normalize(capture.scene),normalize(expectedScene),item.Name+' scene');
            const png=Buffer.from(capture.image.split(',')[1],'base64');
            fs.writeFileSync(path.join(directory,item.Name+'.ui.png'),png);
            assert.deepEqual(await sharp(png).raw().toBuffer(),
                await sharp(path.join(directory,item.Name+'.playground.png')).raw().toBuffer(),item.Name+' pixels');
            console.log(item.Name+': live Playground state and native canvas match');
        }
        assert.deepEqual(errors,[]);
    } finally {await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

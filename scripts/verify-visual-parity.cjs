// Captures the unmodified Playground Canvas renderer at exactly the CLI scene
// time and resolution. Requires playwright and sharp (no FFmpeg or video loss).
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const sharp = require('sharp');
const root = path.resolve(__dirname, '..');
const directory = path.resolve(process.argv[2] || path.join(root, 'obj/visual-parity'));
const label = process.argv[3] || 'after';
const cases = JSON.parse(fs.readFileSync(path.join(directory, label + '.timings.json')));
function ppm(file, width, height) {
    const bytes = fs.readFileSync(file);
    return bytes.subarray(bytes.length - width * height * 3);
}
(async () => {
    const browser = await chromium.launch({headless:true, channel: process.env.CHROMIUM_CHANNEL || 'msedge',
        executablePath: process.env.CHROMIUM_PATH || undefined});
    const results = [];
    try {
        const page = await browser.newPage({viewport:{width:1280,height:720}, deviceScaleFactor:1});
        await page.setContent('<canvas width="1280" height="720"></canvas>');
        await page.addScriptTag({path:path.join(root,'src/SoundScript.Playground/wwwroot/js/visual-scene-renderer.js')});
        for (const item of cases) {
            const scene = JSON.parse(fs.readFileSync(path.join(directory,item.Name+'.scene.json')));
            const reference = await page.evaluate(({scene,width,height}) => {
                const canvas = document.querySelector('canvas');
                canvas.width = width; canvas.height = height;
                window.SoundScriptVisualRenderer.renderScene(canvas, scene);
                return canvas.toDataURL('image/png').split(',')[1];
            }, {scene,width:item.Width,height:item.Height});
            const browserPng = Buffer.from(reference,'base64');
            fs.writeFileSync(path.join(directory,item.Name+'.playground.png'),browserPng);
            const expected = await sharp(browserPng).removeAlpha().raw().toBuffer();
            const actual = ppm(path.join(directory,item.Name+'.'+label+'.ppm'),item.Width,item.Height);
            await sharp(actual,{raw:{width:item.Width,height:item.Height,channels:3}}).png()
                .toFile(path.join(directory,item.Name+'.'+label+'.png'));
            const diff = Buffer.alloc(actual.length);
            let sum = 0, bad = 0;
            for(let i=0;i<actual.length;i+=3) {
                let maximum = 0;
                for(let c=0;c<3;c++) {const d=Math.abs(actual[i+c]-expected[i+c]);sum+=d;maximum=Math.max(maximum,d);diff[i+c]=Math.min(255,d*4);}
                if(maximum>16) bad++;
            }
            const result = {name:item.Name,width:item.Width,height:item.Height,time:item.Time,
                meanAbsoluteError:sum/actual.length,pixelsOver16Percent:bad/(item.Width*item.Height)*100};
            results.push(result); console.log(JSON.stringify(result));
            await sharp(diff,{raw:{width:item.Width,height:item.Height,channels:3}}).png()
                .toFile(path.join(directory,item.Name+'.'+label+'.diff.png'));
            if(label === 'after' && process.argv.includes('--assert')) {
                // Geometric fixtures are font independent; legacy system-font
                // labels and shadows allow a wider cross-platform tolerance.
                assert(result.meanAbsoluteError < (item.Name.startsWith('legacy') ? 8 : 4),item.Name);
            }
        }
        fs.writeFileSync(path.join(directory,label+'.metrics.json'),JSON.stringify(results,null,2));
        fs.writeFileSync(path.join(directory,'reference.metadata.json'),JSON.stringify({
            rendererSha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(root,'src/SoundScript.Playground/wwwroot/js/visual-scene-renderer.js'),'utf8').replace(/\r\n/g,'\n')).digest('hex'),
            browser:browser.version(),platform:process.platform,deviceScaleFactor:1,
            cases:cases.map(({Name,Source,Time,Width,Height})=>({Name,Source,Time,Width,Height}))
        },null,2));
        const rows=cases.map(item=>`<h2>${item.Name} · ${item.Time}s · ${item.Width}×${item.Height}</h2><div class="row">`+
            ['before','playground','after'].map(kind=>`<figure><figcaption>${kind}</figcaption><img src="${item.Name}.${kind}.png"></figure>`).join('')+'</div>').join('');
        fs.writeFileSync(path.join(directory,'comparison.html'),`<!doctype html><meta charset="utf-8"><title>CLI / Playground visual parity</title><style>body{font:16px system-ui;background:#eee;margin:24px}.row{display:flex;gap:12px}figure{margin:0;width:33%}img{width:100%}</style><h1>Identical scenes and timestamps</h1>${rows}`);
        // Compact review sheet; measurements above always use full native frames.
        for(const name of ['legacy-cards','primitives-1','org-chart']) {
            const layers=[];
            for(const [i,kind] of ['before','playground','after'].entries()) {
                const file=path.join(directory,name+'.'+kind+'.png');
                if(!fs.existsSync(file)) continue;
                layers.push({input:await sharp(file).resize(640,360).png().toBuffer(),left:0,top:i*390+30});
                layers.push({input:Buffer.from(`<svg width="640" height="30"><rect width="640" height="30" fill="#eee"/><text x="12" y="22" font-family="sans-serif" font-size="18">${kind==='before'?'CLI before':kind==='after'?'CLI after':'Playground reference'}</text></svg>`),left:0,top:i*390});
            }
            await sharp({create:{width:640,height:1170,channels:3,background:'#eee'}}).composite(layers).png().toFile(path.join(directory,name+'.comparison.png'));
        }
    } finally {await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

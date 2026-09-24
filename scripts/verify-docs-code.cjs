const fs = require('node:fs'), path = require('node:path'), http = require('node:http'), assert = require('node:assert/strict');
const { chromium } = require('playwright');
const root = path.resolve(__dirname, '..');
const releaseState = JSON.parse(
    fs.readFileSync(path.join(root, 'docs', 'release-state.json'), 'utf8')
);
const publicVersion = releaseState.publicVersion;
const cases = [
    ['csharp', 'var x = "< > & \\" \' script img onclick";'],
    ['bash', 'dotnet build\nprintf "literal $ prompt"'],
    ['console-output', 'Build succeeded.'],
    ['html', '<script>window.injected=true</script><img src=x onclick="window.injected=true">'],
    ['powershell', 'PS> literal source prompt'],
    ['soundscript', 'tempo 120\ntrack cue { C4 q }'],
    ['unknown-<img>', '< > &'],
    ['text', '\n'+'A'.repeat(400)+'\n\n']
];
const markdown = '# Code UX\n\nInline `identifier` stays lightweight.\n\n' + cases.map(([lang, code])=>'```'+lang+'\n'+code+'\n```').join('\n\n');
async function main() {
    const server = http.createServer((req,res)=>{
        const url = new URL(req.url, 'http://localhost');
        if (url.pathname === '/fixture.md') { res.end(markdown);return; }
        const file = path.resolve(root,'docs','.'+decodeURIComponent(url.pathname));
        if (!file.startsWith(path.join(root,'docs')+path.sep) || !fs.existsSync(file) || !fs.statSync(file).isFile()) {res.writeHead(404);res.end();return;}
        res.setHeader('Content-Type',file.endsWith('.js')?'text/javascript':file.endsWith('.css')?'text/css':file.endsWith('.html')?'text/html':'text/plain');
        res.end(fs.readFileSync(file));
    });
    await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
    let browser;
    try {
        browser=await chromium.launch({channel:process.env.CHROMIUM_CHANNEL || (process.platform==='win32'?'msedge':undefined),headless:true});
        const context=await browser.newContext({permissions:['clipboard-read','clipboard-write']});
        const page=await context.newPage(), base=`http://127.0.0.1:${server.address().port}`;
        await page.addInitScript(()=>{window.clipboardWrites=[];const original=navigator.clipboard.writeText.bind(navigator.clipboard);navigator.clipboard.writeText=async text=>{window.clipboardWrites.push(text);return original(text);};});
        const errors=[];page.on('pageerror',e=>errors.push(e.message));
        await page.goto(base+'/doc.html?p=fixture.md');await page.waitForSelector('.code-block');
        assert.equal(await page.locator('.code-block').count(),8);
        assert.equal(await page.locator('#content p button').count(),0);
        assert.deepEqual(await page.locator('.code-toolbar > span').allTextContents(),['C#','Bash','Output','HTML','PowerShell','SoundScript','unknown-<img>','Text']);
        assert.equal(await page.locator('.code-terminal').count(),2);assert.equal(await page.locator('.code-output').count(),1);
        assert.equal(await page.locator('#content script,#content img').count(),0);assert.equal(await page.evaluate(()=>window.injected),undefined);
        const aliases=await page.evaluate(()=>{
            const target=document.createElement('div');
            const names=['cs','csharp','sh','shell','pwsh','cmd','json','xml','javascript','js','html','css','text'];
            target.innerHTML=SoundScriptDocs.render(names.map(name=>'```'+name+'\nx\n```').join('\n\n'),marked);
            SoundScriptDocs.enhance(target);
            return Array.from(target.querySelectorAll('.code-toolbar > span'),el=>el.textContent);
        });
        assert.deepEqual(aliases,['C#','C#','Shell','Shell','PowerShell','Command Prompt','JSON','XML','JavaScript','JavaScript','HTML','CSS','Text']);
        const codes=await page.locator('pre > code').allTextContents();
        assert.deepEqual(codes,cases.map(([,code])=>code+'\n'), 'rendered text preserves source, including leading/trailing blank lines');
        for(let i=0;i<codes.length;i++){
            const button=page.locator('.code-toolbar button').nth(i);await button.click();
            assert.equal(await page.evaluate(()=>window.clipboardWrites.at(-1)),codes[i]);
            assert.equal((await page.evaluate(()=>navigator.clipboard.readText())).replaceAll('\r\n','\n'),codes[i]);
            assert.equal(await button.textContent(),'Copied!');
        }
        assert.ok(!codes[1].startsWith('$ '));assert.ok(codes[4].startsWith('PS>'));
        const first=page.locator('.code-toolbar button').first();await first.focus();await page.keyboard.press('Enter');
        assert.equal(await page.evaluate(()=>window.clipboardWrites.at(-1)),codes[0]);
        assert.ok(await first.evaluate(el=>getComputedStyle(el).outlineStyle!=='none'));
        await page.waitForFunction(()=>document.querySelector('.code-toolbar button').textContent==='Copy');
        // Denied modern API falls back to local copy, restoring focus.
        await page.evaluate(()=>{navigator.clipboard.writeText=async()=>{throw new Error('denied')};window.realExec=document.execCommand.bind(document);window.fallbackText=null;document.execCommand=()=>{window.fallbackText=document.querySelector('textarea').value;return true;};});
        await first.click();assert.equal(await page.evaluate(()=>window.fallbackText),codes[0]);assert.equal(await page.locator('textarea').count(),0);
        await page.evaluate(()=>{document.execCommand=()=>false;});await first.click();
        assert.equal(await first.textContent(),'Copy failed');assert.deepEqual(await page.locator('pre > code').allTextContents(),codes);
        assert.match(await page.locator('.code-status').first().textContent(),/select code/);
        await page.setViewportSize({width:360,height:780});await page.emulateMedia({reducedMotion:'reduce'});
        assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
        assert.ok(await page.locator('.code-block').last().locator('pre').evaluate(el=>el.scrollWidth>el.clientWidth));
        for(const button of await page.locator('.code-toolbar button').all()){const b=await button.boundingBox();assert.ok(b.x>=0&&b.x+b.width<=360);}
        fs.mkdirSync(path.join(root,'artifacts/v15-docs'),{recursive:true});
        await page.screenshot({path:path.join(root,'artifacts/v15-docs/code-mobile.png'),fullPage:true});
        await page.setViewportSize({width:1280,height:900});await page.screenshot({path:path.join(root,'artifacts/v15-docs/code-desktop.png'),fullPage:true});
        await page.goto(base+'/doc.html');await page.waitForSelector('#content h1');assert.match(await page.locator('#content').textContent(),/canonical documentation index/);
        const metadata=JSON.parse(await page.locator('#structured-data').textContent());
assert.equal(
    metadata['@graph'].find(x=>x['@type']==='SoftwareApplication').softwareVersion,
    publicVersion
);
        await page.goto(base+'/doc.html?p=SoundScript.md');await page.waitForSelector('#content h1');assert.ok(await page.locator('#content a[href="doc.html?p=documentation.md"]').count());
        await page.goto(base+'/doc.html?p=cli.md#installation-and-automation');await page.waitForSelector('#installation-and-automation');
        assert.deepEqual(errors,[]);
        console.log('PASS: 8 independent blocks; labels/classes, exact clipboard, literal prompts, safe text, keyboard/focus, success/reset, denial/fallback/failure, 360px overflow, reduced motion, canonical/legacy navigation and public metadata.');
    } finally {if(browser)await browser.close();await new Promise(resolve=>server.close(resolve));}
}
main().catch(e=>{console.error(e);process.exitCode=1;});

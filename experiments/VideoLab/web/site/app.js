'use strict';
const byId = id => document.getElementById(id);
const local = document.body.dataset.localWorkbench === 'true';
const fail = message => { byId('error').textContent = message; byId('error').hidden = false; };
let token, proof, demo, snapshot, candidates = [], sourceMode = 'samples', values = {}, revision = 0, pendingFrame = null;
let bindQueue = Promise.resolve(), requestQueue = Promise.resolve(), busy = false, busyDepth = 0;
const video = byId('video');
const option = (text,value) => { const item=document.createElement('option');item.textContent=text;item.value=value;return item; };
const publicFile = format => `${snapshot.stem}.${format}`;
function state() {return {demo:demo.id,media:sourceMode,video:byId('sample-video').value,audio:byId('sample-audio').value,parameters:values};}
async function api(action,body={},file) {
    // Media selection can immediately follow a binding change. Serialize native
    // operations so the workbench's single-operation gate cannot reject that UI
    // sequence; cancellation must still reach an in-flight render immediately.
    const send=async()=>{
    const headers={'X-VideoLab-Session':token};
    if(file) headers['X-File-Name']=encodeURIComponent(file.name); else headers['Content-Type']='application/json';
    const response=await fetch(new URL(`api/${action}`,location.origin),{method:'POST',headers,body:file||JSON.stringify(body)});
    const data=await response.json(); if(!response.ok)throw new Error(data.error||'Local operation failed.');return data;
    };
    if(action==='cancel')return send();
    const pending=requestQueue.catch(()=>{}).then(send);requestQueue=pending;return pending;
}
function clearError(){byId('error').hidden=true;}
function exportsFor(mp4,webm){for(const [format,url] of [['mp4',mp4],['webm',webm]]){const link=byId(`export-${format}`);if(url)link.href=url;else link.removeAttribute('href');link.setAttribute('aria-disabled',String(!url));}}
function dirty(){exportsFor(null,null);byId('render-state').textContent='Parameters/media changed · render to update video';byId('export-note').textContent='Exact frame state is updated. Render the current composition before export; the video still shows the previous render.';}
function parameters(){
    byId('parameter-controls').replaceChildren();
    const metadata={...demo.script.parameters,...demo.script.typedParameters};
    for(const [name,p] of Object.entries(metadata)){
        const row=document.createElement('div');row.className='param';
        const line=document.createElement('div');line.className='param-line';
        const label=document.createElement('label');label.textContent=name;label.htmlFor=`parameter-${name}`;
        if(p.type){
            const select=document.createElement('select');select.id=`parameter-${name}`;select.disabled=!local;
            const choices=p.type==='boolean'?[true,false]:p.values;
            select.replaceChildren(...choices.map(v=>option(String(v),String(v))));select.value=String(values[name]);
            select.addEventListener('change',()=>{clearError();values[name]=p.type==='boolean'?select.value==='true':select.value;bindLocal();});
            const defaults=document.createElement('small');defaults.textContent=`Source default ${p.default} · ${p.type}`;
            row.append(label,select,defaults);byId('parameter-controls').append(row);continue;
        }
        const number=document.createElement('input');number.type='number';number.id=`parameter-${name}`;number.min=p.min;number.max=p.max;number.step=p.max-p.min<=1?'0.01':'1';number.value=values[name];number.disabled=!local;number.setAttribute('aria-label',name);
        const slider=document.createElement('input');slider.type='range';slider.min=p.min;slider.max=p.max;slider.step=number.step;slider.value=values[name];slider.disabled=!local;slider.setAttribute('aria-label',`${name} slider`);
        const defaults=document.createElement('small');defaults.textContent=`Source default ${p.default} · range ${p.min}–${p.max}`;
        const change=element=>{const n=Number(element.value);if(!Number.isFinite(n)||n<p.min||n>p.max){fail(`${name} must be between ${p.min} and ${p.max}.`);return;}clearError();values[name]=n;number.value=slider.value=n;bindLocal();};
        number.addEventListener('change',()=>change(number));slider.addEventListener('input',()=>change(slider));
        line.append(label,number);row.append(line,slider,defaults);byId('parameter-controls').append(row);
    }
    byId('parameters').textContent=JSON.stringify(values,null,2);
    byId('parameter-help').textContent=Object.keys(metadata).length ? (local?'Change values without recompiling the composition. Inspect immediately, then render to update video.':'Choose a validated binding below. For arbitrary values, open My Files → local workbench.') : 'This composition has no exposed runtime parameters.';
}
function timeline(){
    byId('timeline').replaceChildren();
    const tracks=snapshot.tracks||[];
    for(const kind of ['video','transition','text','callout','overlay','audio']){
        const items=tracks.filter(t=>t.kind===kind);if(!items.length)continue;
        const row=document.createElement('div');row.className='track-row';
        const label=document.createElement('span');label.className='track-label';label.textContent=kind==='overlay'?'Shape':kind[0].toUpperCase()+kind.slice(1);
        const lane=document.createElement('div');lane.className='track-lane';
        lane.style.height=(items.length*27)+'px';
        for(const [index,track] of items.entries()){const block=document.createElement('span');block.className=`track-block ${kind}`;block.style.top=(index*27+2)+'px';block.style.left=`${100*track.at/demo.frames}%`;block.style.width=`${100*track.frames/demo.frames}%`;block.textContent=track.label;block.title=`${track.label}: frames ${track.at}–${track.at+track.frames-1}`;lane.append(block);}
        const head=document.createElement('span');head.className='playhead';lane.append(head);row.append(label,lane);byId('timeline').append(row);
    }
}
function editingProperties(scene){
    const panel=byId('edit-properties');panel.replaceChildren();
    const line=text=>{const p=document.createElement('p');p.textContent=text;panel.append(p);};
    for(const t of snapshot.tracks.filter(t=>t.kind==='video'))line(`${t.label}: source trim ${t.trim}–${t.trim+t.frames-1}; timeline ${t.at}–${t.at+t.frames-1}; ${t.fade?`crossfade ${t.fade} frames`:'cut / placement'}.`);
    for(const l of scene.Layers){const t=l.Transform;line(`${l.Id}${l.Group?` · group ${l.Group}`:''} · ${l.Included?'visible':'excluded'} · position (${t.X}, ${t.Y}) · scale ${t.ScaleX} × ${t.ScaleY} · rotation ${t.Rotation}° · opacity ${t.Opacity} · crop (${t.Crop.X}, ${t.Crop.Y}, ${t.Crop.Width}, ${t.Crop.Height})`);if(l.Caption)line(`${l.Caption.Text} · ${l.Caption.Font}, ${l.Caption.FontSize}px · ${l.Caption.Align} aligned`);}
    for(const a of scene.Audio)line(`${a.Id}: source frame ${a.SourceFrame}; gain ${a.Gain}; ${a.Included?'included':'muted by condition'}.`);
}
function inspect(seek){
    const frame=Number(byId('frame').value),scene=snapshot.scenes[frame];if(!scene)return;
    byId('frame-label').textContent=`${frame} / ${demo.frames-1}`;
    byId('scene').textContent=JSON.stringify(scene,null,2);
    editingProperties(scene);
    byId('summary').textContent=`${(frame/demo.fps).toFixed(3)} s · ${scene.Clips.length} visible clip(s) · ${scene.Layers.filter(l=>l.Included).length}/${scene.Layers.length} included layer(s)`;
    for(const head of document.querySelectorAll('.playhead'))head.style.left=`${100*frame/demo.frames}%`;
    if(seek){video.pause();pendingFrame=frame;if(video.readyState){video.currentTime=frame/demo.fps;pendingFrame=null;}}
}
function showSnapshot(){
    values={...snapshot.parameters};parameters();timeline();byId('frame').value=0;
    byId('script').textContent=JSON.stringify(snapshot.script||demo.script,null,2);inspect(false);
}
function selectBinding(){
    clearError();snapshot=candidates[Number(byId('snapshot').value)||0];showSnapshot();
    pendingFrame=null;video.pause();video.src=publicFile(byId('format').value);video.poster=demo.thumbnail;video.load();
    exportsFor(publicFile('mp4'),publicFile('webm'));byId('render-state').textContent='Verified sample render';byId('export-note').textContent='Download the selected verified output. No browser re-encoding.';
    if(local)bindLocal();
}
function selectMedia(){
    const sampleVideo=byId('sample-video').value==='original'?'montage':byId('sample-video').value,sampleAudio=byId('sample-audio').value==='original'?'calm':byId('sample-audio').value;
    candidates=demo.id==='showcase'?demo.snapshots.filter(s=>s.video===sampleVideo&&s.audio===sampleAudio):demo.snapshots;
    byId('snapshot').replaceChildren(...candidates.map((s,i)=>option(`${s.name} · ${i?'alternate runtime values':'source defaults'}`,String(i))));
    byId('snapshot').disabled=candidates.length===1;selectBinding();
}
function selectDemo(){
    demo=proof.demos.find(d=>d.id===byId('demo').value);revision++;
    byId('sample-video').value=demo.id==='showcase'?'montage':'original';byId('sample-audio').value=demo.id==='showcase'?'calm':'original';
    byId('demo-title').textContent=demo.title;byId('description').textContent=demo.description;
    byId('dimensions').textContent=`${demo.width} × ${demo.height} · ${demo.frames} frames · ${demo.fps} fps`;
    video.style.aspectRatio=`${demo.width} / ${demo.height}`;byId('frame').max=demo.frames-1;
    byId('transition').textContent=`Inspect key moment · frame ${demo.inspectFrame}`;
    const hasVideo=!!(demo.script.videos.length||demo.script.sequences?.length),hasAudio=!!demo.script.audio.length;
    byId('sample-video').disabled=!(local?hasVideo:demo.id==='showcase');byId('sample-audio').disabled=!(local?hasAudio:demo.id==='showcase');
    byId('video-file').disabled=!hasVideo;byId('audio-file').disabled=!hasAudio;
    byId('media-help').textContent=demo.id==='showcase'?'Swap compatible sample media without editing the composition.':local?'Selectors apply where this composition has the corresponding track.':'Sample swapping is available in the Video + audio showcase. These focused examples use their validated inputs.';
    for(const card of document.querySelectorAll('.card')){card.classList.toggle('selected',card.dataset.id===demo.id);card.querySelector('button').setAttribute('aria-pressed',String(card.dataset.id===demo.id));}
    selectMedia();byId('status').textContent=`${proof.demos.length} demos ready · ${demo.title}`;
}
function bindLocal(markDirty=true){
    if(!token)return;const current=++revision,body=state();if(markDirty)dirty();
    bindQueue=bindQueue.catch(()=>{}).then(async()=>{
        if(current!==revision)return;
        const result=await api('bind',body);if(current!==revision)return;
        snapshot={...snapshot,...result};byId('parameters').textContent=JSON.stringify(result.parameters,null,2);byId('script').textContent=JSON.stringify(result.script,null,2);timeline();inspect(false);
    }).catch(e=>fail(e.message));return bindQueue;
}
const disabled=new Map();
function setBusy(value,rendering=false){
    // File-picker events can overlap while the first upload awaits its bind.
    // Capture original control states once and restore after the last operation.
    if(value){if(++busyDepth>1)return;}
    else{busyDepth=Math.max(0,busyDepth-1);if(busyDepth)return;}
    busy=value;
    if(value){for(const el of document.querySelectorAll('button,input,select')){disabled.set(el,el.disabled);el.disabled=true;}}
    else{for(const [el,was]of disabled)el.disabled=was;disabled.clear();}
    byId('cancel').disabled=!(value&&rendering);
}
async function mode(value){
    sourceMode=value;byId('personal-panel').hidden=value!=='personal';byId('sample-panel').hidden=value!=='samples';
    byId('sample-mode').setAttribute('aria-pressed',String(value==='samples'));byId('personal-mode').setAttribute('aria-pressed',String(value==='personal'));
    if(local&&value==='samples'){
        try{setBusy(true);await api('reset');byId('video-file-info').textContent='No personal video selected.';byId('audio-file-info').textContent='No personal audio selected.';byId('video-file').value=byId('audio-file').value='';}
        catch(e){fail(e.message);}finally{setBusy(false);}selectMedia();
    }else if(local)bindLocal();
}
async function upload(kind,file){
    if(!file)return;
    const ext=file.name.split('.').pop().toLowerCase(),allowed=kind==='video'?['mp4','webm']:['wav','mp3'];
    if(!allowed.includes(ext)||!file.size||file.size>50*1024*1024){fail('Choose MP4/WebM video or WAV/MP3 audio, 1 byte to 50 MiB.');return;}
    try{clearError();setBusy(true);byId('status').textContent='Validating selected media locally…';
        const result=await api(`upload?kind=${kind}&demo=${demo.id}`,{},file);
        byId(`${kind}-file-info`).textContent=`${result.name} · ${result.duration.toFixed(2)} s · ${result.width?`${result.width} × ${result.height} · `:''}${result.codec} · ${(result.bytes/1048576).toFixed(2)} MiB`;
        await bindLocal();byId('status').textContent='Local media ready. Inspect the composition, then render.';
    }catch(e){fail(e.message);}finally{setBusy(false);byId(`${kind}-file`).value='';}
}
(async()=>{
    try{
        const response=await fetch('proof.json');if(!response.ok)throw new Error(`Timeline request failed (${response.status}).`);proof=await response.json();
        if(proof.schemaVersion!==2||!Array.isArray(proof.demos)||proof.demos.length!==9||proof.demos.some(d=>!Number.isInteger(d.frames)||d.frames<1||!d.snapshots.length||d.snapshots.some(s=>!/^[A-Za-z0-9-]+$/.test(s.stem)||s.scenes.length!==d.frames)))throw new Error('Unsupported or incomplete proof data.');
        for(const d of proof.demos){
            byId('demo').append(option(d.title,d.id));
            const card=document.createElement('article');card.className='card';card.dataset.id=d.id;
            const image=document.createElement('img');image.src=d.thumbnail;image.alt=`${d.title} preview`;image.loading='lazy';
            const body=document.createElement('div');body.className='card-body';const title=document.createElement('h3');title.textContent=d.title;
            const description=document.createElement('p');description.textContent=d.description.split('. ')[0]+'.';
            const button=document.createElement('button');button.type='button';button.textContent='Load Demo';button.addEventListener('click',()=>{byId('demo').value=d.id;selectDemo();byId('demo-title').scrollIntoView({block:'nearest'});});
            body.append(title,description,button);card.append(image,body);byId('demo-cards').append(card);
        }
        if(local){const r=await fetch(new URL('api/session',location.origin));if(!r.ok)throw new Error('Local session unavailable.');token=(await r.json()).token;byId('mode').textContent='Private local workbench · native VideoLab + FFmpeg';byId('render-actions').hidden=false;byId('local-launch').hidden=true;byId('file-controls').hidden=false;}
        byId('workspace').hidden=false;selectDemo();
        byId('demo').addEventListener('change',selectDemo);byId('snapshot').addEventListener('change',selectBinding);
        for(const id of ['sample-video','sample-audio'])byId(id).addEventListener('change',()=>local?bindLocal():selectMedia());
        byId('format').addEventListener('change',()=>{if(local){dirty();byId('render-state').textContent='Render the selected format for current settings';}else selectBinding();});
        byId('frame').addEventListener('input',()=>inspect(true));byId('transition').addEventListener('click',()=>{byId('frame').value=demo.inspectFrame;inspect(true);});
        byId('reset-parameters').addEventListener('click',()=>{if(local){values=Object.fromEntries(Object.entries({...demo.script.parameters,...demo.script.typedParameters}).map(([k,v])=>[k,v.default]));parameters();bindLocal();}else{byId('snapshot').value='0';selectBinding();}});
        byId('sample-mode').addEventListener('click',()=>mode('samples'));byId('personal-mode').addEventListener('click',()=>mode('personal'));
        byId('open-local').addEventListener('click',()=>location.assign('http://127.0.0.1:8745/labs/videolab/'));
        for(const kind of ['video','audio'])byId(`${kind}-file`).addEventListener('change',e=>upload(kind,e.target.files[0]));
        byId('render').addEventListener('click',async()=>{try{await bindQueue;clearError();setBusy(true,true);byId('render-state').textContent='Rendering locally…';byId('status').textContent='FFmpeg is composing the current snapshot. You can cancel.';
            const format=byId('format').value,result=await api('render',{...state(),format});video.pause();video.src=result.url;video.load();exportsFor(format==='mp4'?result.url:null,format==='webm'?result.url:null);byId('render-state').textContent='Current composition rendered';byId('export-note').textContent=`${format.toUpperCase()} ready · SHA-256 ${result.sha256}`;byId('status').textContent='Render complete. Download before resetting the session.';
        }catch(e){fail(e.message);byId('render-state').textContent='Render did not complete';}finally{setBusy(false);}});
        byId('end-session').addEventListener('click',async()=>{try{await api('stop');setBusy(true);exportsFor(null,null);video.pause();video.removeAttribute('src');video.load();byId('status').textContent='Local session ended and temporary files removed. Close this tab or restart the workbench.';}catch(e){fail(e.message);}});
        byId('cancel').addEventListener('click',()=>api('cancel').catch(e=>fail(e.message)));
        video.addEventListener('loadedmetadata',()=>{if(pendingFrame!==null){video.currentTime=pendingFrame/demo.fps;pendingFrame=null;}byId('playback-time').textContent=`${video.currentTime.toFixed(2)} / ${(demo.frames/demo.fps).toFixed(2)} s`;});
        video.addEventListener('timeupdate',()=>{byId('playback-time').textContent=`${video.currentTime.toFixed(2)} / ${(demo.frames/demo.fps).toFixed(2)} s`;if(!video.paused){byId('frame').value=Math.min(demo.frames-1,Math.floor(video.currentTime*demo.fps+0.000001));inspect(false);}});
        video.addEventListener('error',()=>fail('Playback failed. Try the other verified format, or render again in local mode.'));
        addEventListener('pagehide',()=>{if(local&&token&&!busy)fetch(new URL('api/reset',location.origin),{method:'POST',headers:{'X-VideoLab-Session':token,'Content-Type':'application/json'},body:'{}',keepalive:true}).catch(()=>{});});
    }catch(e){fail(`Unable to load VideoLab: ${e.message} Reload to retry.`);}
})();

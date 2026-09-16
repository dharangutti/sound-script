// Local-only acceptance report. Original/derived recordings stay in ignored artifacts.
const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(process.argv[2]);
const rows=JSON.parse(fs.readFileSync(path.join(root,'measurements.json'),'utf8').replace(/^\uFEFF/,''));
const escape=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const ids=[...new Set(rows.map(r=>r.Id))];
let html=`<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>SoundScript — Piano listening review</title><style>
body{font:16px/1.6 system-ui;background:#101722;color:#e4eaf4;max-width:1250px;margin:40px auto;padding:0 20px}h1,h2{line-height:1.25}a{color:#8fd7fc}.muted{color:#b1bfce}.row{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:16px}.card{background:#1b2738;border:1px solid #34445c;border-radius:12px;padding:18px}section{margin:36px 0}audio{width:100%;margin-top:12px}select,textarea,button{font:inherit;box-sizing:border-box;padding:10px;border-radius:6px}textarea{display:block;width:100%;margin-top:8px}button{background:#76e0bd;color:#101722;border:0;cursor:pointer}select{max-width:100%}@media(max-width:800px){.row{grid-template-columns:1fr}}</style>
<h1>Piano listening review</h1><p>Ten excerpts, two recordings. Compare the original with a single extracted melody and parallel polyphonic voices. Generated playback uses the same piano instrument for both modes.</p>
<p class="muted">Original excerpts are mono 16 kHz, matching the analysis boundary. Playback timbre differs from the acoustic piano. Coverage, note counts and round-trip metrics are evidence, not source accuracy. A rejected melody has no generated audio; it has not been forced through the gate.</p>
<p><strong>Human review is pending.</strong> Rate improved / partially improved only after listening. Use “not recognizable” for an unrelated reconstruction and “rejected” when unsuitable. Ratings can be exported for the acceptance record.</p><button id="save">Download listening verdicts</button>`;
for(const id of ids){
 const records=rows.filter(r=>r.Id===id), r=records[0];
 html+=`<section data-id="${escape(id)}"><h2>${escape(r.Recording)} · ${r.StartSeconds}–${r.StartSeconds+10} s</h2><div class="row"><div class="card"><strong>Original excerpt</strong><audio controls preload="none" src="${escape(id)}-original.wav"></audio></div>`;
 for(const mode of ['extract-melody','polyphonic']) {
  const x=records.find(r=>r.Mode===mode);const file=escape(id+'-'+mode);
  html+=`<div class="card"><strong>${mode==='polyphonic'?'Polyphonic / Piano':'Extract Melody'}</strong><p>${escape(x.Status)} · ${x.NoteCount} candidate notes · ${(x.StableCoverage*100).toFixed(1)}% stable coverage</p>`;
  if(mode==='polyphonic')html+=`<p>Maximum simultaneous notes: ${x.MaximumSimultaneousNotes}; octave ambiguity: ${(x.OctaveAmbiguousFraction*100).toFixed(1)}% of active frames.</p>`;
  html+=x.Generated?`<audio controls preload="none" src="${file}.wav"></audio><p><a href="${file}.ss">SoundScript</a> · <a href="${file}.json">Report</a></p>`:`<p>No generated audio — evidence rejected.</p><a href="${file}.json">Rejection report</a>`;
  html+='</div>';
 }
 html+=`</div><p class="muted">Monophonic baseline: ${escape(records.find(r=>r.Mode==='monophonic').Status)}.</p><label>Listening verdict <select><option>Pending human review</option><option>Improved</option><option>Partially improved</option><option>Not recognizable</option><option>Rejected</option></select></label><textarea rows="2" aria-label="Listening notes for ${escape(id)}" placeholder="Describe chord resemblance, false notes, missing notes, timing or sustain."></textarea></section>`;
}
html+=`<script>document.addEventListener('play',event=>{if(event.target.tagName==='AUDIO')document.querySelectorAll('audio').forEach(a=>{if(a!==event.target)a.pause()})},true);document.querySelector('#save').onclick=()=>{const verdicts=Array.from(document.querySelectorAll('section')).map(s=>({id:s.dataset.id,verdict:s.querySelector('select').value,notes:s.querySelector('textarea').value}));const url=URL.createObjectURL(new Blob([JSON.stringify(verdicts,null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download='piano-listening-verdicts.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000)};</script></html>`;
fs.writeFileSync(path.join(root,'index.html'),html);
console.log(`Created listening comparison for ${ids.length} excerpts`);

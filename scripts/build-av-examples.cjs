// Maintainer authoring helper. Outputs standalone, editable SoundScript sources;
// no JavaScript is needed to load, play, or export the resulting compositions.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const legacy = process.argv.includes('--legacy');
const themes = {
  business: ['#f1f5f9', '#ffffff', '#0f172a', '#2563eb', '#64748b'],
  technical: ['#0b1220', '#17263c', '#e2e8f0', '#38bdf8', '#64748b'],
  warm: ['#fff7ed', '#ffedd5', '#431407', '#ea580c', '#9a3412'],
  dark: ['#17132b', '#292041', '#faf5ff', '#c084fc', '#a78bfa'],
};
let parts, colors, duration;
function begin(title, theme = 'technical', seconds = 6, audio = '') {
  parts = [`tempo 120\n${audio}\nsync audio\n`];
  colors = themes[theme]; duration = seconds;
  shape('background', 'rectangle', 640, 360, 1280, 720, 0, seconds, colors[0]);
  text('heading', title, 640, 78, 1100, 48, 0, seconds);
}
function property(key, value, seconds) {
  return legacy ? `animate ${key} ${value} -> ${value} over ${seconds}s` : `set ${key} ${value}`;
}
function shape(name, kind, x, y, w, h, at = 0, seconds = duration - at, fill = colors[1], extra = [], curves = {}) {
  const settings = [`shape ${kind}`, ...((kind === 'line' || kind === 'ring') ? [`stroke "${fill}"`] : [`fill "${fill}"`]), ...extra];
  for (const [key, value] of Object.entries({ x, y, width: w, height: h })) {
    if (!curves[key]) settings.push(property(key, value, seconds));
  }
  for (const [key, value] of Object.entries(curves)) settings.push(`animate ${key} ${value[0]} -> ${value[1]} over ${value[2] ?? seconds}s`);
  parts.push(`visual "${name}" for ${seconds}s at ${at}s {\n    ${settings.join('\n    ')}\n}\n`);
}
function text(name, label, x, y, w = 320, size = 28, at = 0, seconds = duration - at, fill = colors[2]) {
  shape(name, 'text', x, y, w, size + 14, at, seconds, fill, [`text "${label}"`, `fontSize ${size}`]);
}
function card(name, label, x, y, at = 0, w = 260, h = 104, seconds = duration - at) {
  shape(name, 'roundedRectangle', x, y, w, h, at, seconds, colors[1], [`stroke "${colors[4]}"`, 'strokeWidth 2']);
  text(`${name}-label`, label, x, y, w - 24, 26, at, seconds);
}
function line(name, x1, y1, x2, y2, at = 0, arrow = false, seconds = duration - at) {
  const w = Math.hypot(x2 - x1, y2 - y1).toFixed(3);
  const rotation = ((Math.atan2(y2 - y1, x2 - x1) * 180 / Math.PI + 360) % 360).toFixed(3);
  shape(name, arrow ? 'arrow' : 'line', (x1+x2)/2, (y1+y2)/2, w, arrow ? 22 : 8, at, seconds, colors[3], [property('rotation', rotation, seconds), 'strokeWidth 3']);
}
function caption(label, at, seconds = 2) { text(`caption-${at}`, label, 640, 640, 1120, 28, at, seconds); }
function save(key) { fs.writeFileSync(path.join(root, 'examples', `visual-${key}.ssv`), parts.join('\n')); }
const cues = 'track cues { instrument piano mp C5 q rest:3 E5 q rest:3 G5 q rest:3 }';
function speech(words) {
  return words.map((word,i) => `track explanation${i} { ${i ? `rest:${i*4}` : ''} speak "${word}" seed=7 }`).join('\n');
}

begin('TEAM RESPONSIBILITIES', 'business', 6, cues);
line('reporting-stem',640,250,640,340); line('reporting-bar',260,340,1020,340);
for (const x of [260,640,1020]) line(`report-${x}`,x,340,x,430);
card('lead','DELIVERY LEAD',640,210);
['DESIGN','ENGINEERING','OPERATIONS'].forEach((label,i) => {
  card(`team-${i}`,label,260+i*380,480, i*2);
  shape(`active-${i}`,'circle',260+i*380,553,18,18,i*2,6-i*2,colors[3]);
});
caption('ONE OWNER - THREE SPECIALIST TEAMS',0,6); save('org-chart');

begin('REQUEST TO DELIVERY','warm',6, speech(['plan','build','ship']));
for (let i=0;i<3;i++) {
  if (i<2) line(`handoff-${i}`,380+i*380,350,520+i*380,350,i*2+1,true);
  card(`step-${i}`,['PLAN','BUILD','SHIP'][i],260+i*380,350,i*2,240,130);
  caption(['AGREE THE OUTCOME','BUILD AND CHECK','DELIVER WITH CONFIDENCE'][i],i*2);
}
save('delivery-flow');

begin('CACHE HIT - REQUEST SEQUENCE','technical',6,cues);
['CLIENT','API','CACHE'].forEach((label,i) => { card(`actor-${i}`,label,240+i*400,200,0,220,72); line(`life-${i}`,240+i*400,250,240+i*400,580); });
line('request',240,320,640,320,0,true); text('request-label','GET /STATUS',440,292,280,22);
line('lookup',640,420,1040,420,2,true); text('lookup-label','LOOKUP',840,392,280,22,2);
line('result',1040,520,240,520,4,true); text('result-label','CACHED RESULT',640,492,500,22,4);
save('sequence-diagram');

begin('SERVICE BOUNDARIES','technical',6,speech(['edge','service','store']));
shape('service-boundary','roundedRectangle',640,380,610,360,0,6,'none',[`stroke "${colors[4]}"`]);
text('boundary-label','PRIVATE NETWORK',640,230,450,22);
card('edge','EDGE',160,390,0,190); card('api','SERVICE',500,390,2,210); card('store','STORE',790,390,4,210);
line('edge-api',255,390,395,390,2,true); line('api-store',605,390,685,390,4,true);
card('client','CLIENT',1100,215,0,220);
line('client-out',990,215,970,215); line('client-up',970,215,970,150);
line('client-route',970,150,160,150); line('client-edge',160,150,160,338,0,true);
caption('EDGE VALIDATES - SERVICE DECIDES - STORE PERSISTS',0,6); save('architecture');

begin('SIGNAL CONDITIONING','technical',6,cues);
['SENSOR','FILTER','OUTPUT'].forEach((s,i) => card(`block-${i}`,s,250+i*390,330,0,250,120));
line('wire-0',375,330,515,330,0,true); line('wire-1',765,330,905,330,2,true);
shape('signal','circle',130,330,18,18,0,6,colors[3],[],{x:[130,1150,6]});
caption('REMOVE NOISE BEFORE THE SIGNAL REACHES THE OUTPUT',0,6); save('block-diagram');

begin('RELEASE MILESTONES','business',6,cues);
line('axis',140,360,1140,360);
['DESIGN','BETA','RELEASE'].forEach((s,i) => { shape(`milestone-${i}`,'circle',220+i*420,360,26,26,i*2,6-i*2,colors[3]); text(`milestone-text-${i}`,s,220+i*420,300,300,28,i*2); text(`date-${i}`,['WEEK 1','WEEK 3','WEEK 6'][i],220+i*420,425,300,22,i*2); });
caption('A TIMED WALK THROUGH THE DELIVERY PLAN',0,6); save('release-timeline');

begin('OPERATIONS - SERVICE HEALTH','technical',6,'track alert { rest:8 C5 e rest e C5 q }');
['API','WORKERS','STORAGE'].forEach((s,i) => {card(`service-${i}`,s,260+i*380,300,0,290,145); shape(`status-${i}`,'circle',260+i*380,425,34,34,0,6,i===1?'#fbbf24':'#34d399'); text(`health-${i}`,i===1?'DEGRADED':'HEALTHY',260+i*380,490,300,24); });
caption('WORKER QUEUE ABOVE TARGET',4); save('status-dashboard');

begin('DEPLOYMENT PROGRESS','dark',6,cues);
for(let i=0;i<3;i++) {
  const at=i*2, y=245+i*125;
  text(`task-${i}`,['BUILD','VERIFY','RELEASE'][i],220,y,240,26);
  shape(`rail-${i}`,'roundedRectangle',770,y,650,36,0,6,colors[1]);
  shape(`fill-${i}`,'rectangle',449,y,8,30,at,6-at,colors[3],[],{x:[449,770,1.5],width:[8,650,1.5]});
}
caption('EACH CHECK COMPLETES BEFORE THE NEXT STAGE',0,6); save('progress-dashboard');

begin('SUPPORT HANDOFF','business',6);
['OWNER: MAYA','PRIORITY: NORMAL','NEXT: FOLLOW UP'].forEach((s,i) => {
  card(`info-${i}`,s,260+i*380,330,i,340,190);
  text(`detail-${i}`,['PLATFORM TEAM','RESPONSE: 4 HOURS','THURSDAY 10:00'][i],260+i*380,395,300,20,i);
});
caption('A SHAREABLE BRIEF WITH CLEAR OWNERSHIP',0,6); save('information-cards');

begin('LATENCY BEFORE AND AFTER','business',6);
text('before-label','BEFORE: 480 MS',260,285,350,26); text('after-label','AFTER: 160 MS',260,455,350,26,2);
shape('before','rectangle',800,285,600,72,0,6,colors[4]);
shape('after','rectangle',504,455,8,72,2,4,colors[3],[],{x:[504,600,2],width:[8,200,2]});
caption('SAME SCALE - ONE THIRD OF THE LATENCY',4); save('comparison');

begin('SAFE DEVICE STARTUP','warm',6,speech(['check','connect','start']));
for(let i=0;i<3;i++) {
  shape(`icon-${i}`,['ring','arrow','triangle'][i],640,330,160,160,i*2,2,colors[3]);
  caption(['1. CHECK THE POWER IS OFF','2. CONNECT THE CABLE','3. START THE DEVICE'][i],i*2);
}
save('startup-explainer');

begin('','dark',6,'track sting { instrument piano p C4 h E4 h G4 h C5 h }');
text('title','A SMALL CHANGE',640,280,1060,68,0,2); text('subtitle','CAN MAKE A LARGE DIFFERENCE',640,380,1060,30,0,2);
text('middle','MEASURE. LEARN. IMPROVE.',640,330,1120,44,2,2);
text('end','START WITH ONE EXPERIMENT',640,330,1120,42,4,2); save('title-captions');

begin('QUARTERLY REVIEW','business',6,speech(['results','focus','next']));
for(let i=0;i<3;i++) {
  text(`page-${i}`,`0${i+1} / 03`,1110,140,160,20,i*2,2);
  text(`slide-${i}`,['RELIABILITY IMPROVED','FOCUS ON RESPONSE TIME','NEXT: AUTOMATE CHECKS'][i],640,300,1100,42,i*2,2);
  caption(['INCIDENTS DOWN 25%','TARGET: UNDER 200 MS','OWNER: PLATFORM TEAM'][i],i*2);
}
save('presentation');

begin('REVIEW AND REVISION','warm',6,cues);
card('draft','DRAFT',260,300); card('review','REVIEW',640,300,2); card('approved','APPROVED',1020,300,4);
line('submit',390,300,510,300,2,true); line('approve',770,300,890,300,4,true);
line('feedback-down',640,365,640,490,2); line('feedback-back',640,490,260,490,2,true); line('feedback-up',260,490,260,365,2);
text('revision','REVISE WHEN NEEDED',450,545,500,24,2);
save('workflow');

begin('DEPENDENCY MAP','technical',6);
const nodes=[[640,350,'CORE'],[280,220,'API'],[1000,220,'WORKER'],[280,510,'TESTS'],[1000,510,'TOOLS']];
nodes.slice(1).forEach(([x,y],i)=>line(`link-${i}`,640,350,x,y,0));
nodes.forEach(([x,y,s],i)=>card(`node-${i}`,s,x,y,i===0?0:1,210,80));
shape('focus','ring',640,350,250,250,2,4,colors[3],['strokeWidth 3'],{opacity:[0.2,1,1]});
caption('CHANGES TO CORE AFFECT FOUR DEPENDENTS',2,4); save('network');

begin('INCIDENT RESPONSE','technical',6,cues);
['DETECT','TRIAGE','RESOLVE'].forEach((s,i) => {
  card(`stage-${i}`,s,260+i*380,340,0,280,160);
  shape(`highlight-${i}`,'roundedRectangle',260+i*380,340,296,176,i*2,2,'none',[`stroke "${colors[3]}"`,'strokeWidth 5']);
  caption(['CONFIRM THE SIGNAL','ASSIGN AN OWNER','VERIFY RECOVERY'][i],i*2);
});
save('step-by-step');

begin('WEEKLY DELIVERY SCORECARD','business',6);
['UPTIME','LEAD TIME','CHANGE FAILURE'].forEach((s,i)=>{
  card(`metric-${i}`,s,260+i*380,240,0,340,100);
  text(`value-${i}`,['99.95%','2.4 DAYS','3%'][i],260+i*380,375,320,58,i);
  text(`target-${i}`,['TARGET: 99.9%','TARGET: 3 DAYS','TARGET: UNDER 5%'][i],260+i*380,490,340,22,i);
});
caption('AUTHORED SNAPSHOT - WEEK 36',0,6); save('kpi');

begin('DISTANCE = SPEED X TIME','warm',6,'track ticks { mp loop 6 { C5 e rest:1.5 } }');
line('ruler',150,420,1130,420);
for(let i=0;i<6;i++) {line(`tick-${i}`,150+i*196,408,150+i*196,432);text(`scale-${i}`,`${i*2} M`,150+i*196,478,130,22);}
shape('traveller','circle',150,340,56,56,0,6,colors[3],[],{x:[150,1130,5]});
caption('SPEED 2 M/S - AFTER 5 SECONDS: 10 M',0,6); save('education');

begin('SCORE + WAVE + VOICE','dark',6,`${cues}\nvoice guide { vocal choir mp rest:4 sing "go" C4 h }\ntrack speech { rest:8 speak "done" seed=7 }\neffect delay time=0.125 feedback=0.1 mix=0.08`);
card('score','SCORE CUES',260,330); card('voice','SYNTHETIC GO',640,330,2); card('speech','SPEAK DONE',1020,330,4);
caption('ONE PCM MIX - ONE SHARED TIMELINE',0,6); save('mixed-audio');

begin('COMPONENT ROLLOUT - 48 SERVICES','technical',6,'track completion { rest:10 C5 q E5 q }');
for(let i=0;i<48;i++) {
  const x=122+(i%8)*148,y=190+Math.floor(i/8)*66,at=Math.floor(i/8)*0.75;
  card(`component-${i}`,`SVC ${String(i+1).padStart(2,'0')}`,x,y,0,132,52);
  shape(`ready-${i}`,'circle',x+51,y-17,10,10,at,6-at,'#34d399');
}
caption('ROLLING BATCHES - ALL 48 READY BY 4 SECONDS',4); save('scale-study');
console.log(`Wrote 20 standalone compositions (${legacy ? 'existing' : 'compact'} syntax).`);

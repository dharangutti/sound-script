const audio = document.querySelector('#audio');
const scene = document.querySelector('#scene');
const scenario = document.querySelector('#scenario');
const position = document.querySelector('#position');
const error = document.querySelector('#error');
let generation = 0, pending = null, objectUrl = null, animation = null;
const selection = () => scenario.value.toLowerCase();

async function paint() {
    // Rendering cadence is not a media clock. Only the actual audio position is queried.
    const time = audio.currentTime;
    position.value = `${time.toFixed(2)} s`;
    if (pending) return;
    const current = generation;
    const controller = new AbortController();
    pending = controller;
    try {
        const response = await fetch(`/api/scene.svg?scenario=${selection()}&t=${time}`, { signal: controller.signal });
        if (!response.ok) throw new Error('Scene request failed');
        const blob = await response.blob();
        if (current !== generation) return;
        const previous = objectUrl;
        objectUrl = URL.createObjectURL(blob);
        scene.src = objectUrl;
        scene.dataset.time = String(time);
        if (previous) URL.revokeObjectURL(previous);
    } catch (e) { if (e.name !== 'AbortError') error.textContent = e.message; }
    finally { if (pending === controller) pending = null; }
}
function invalidate() { generation++; pending?.abort(); pending = null; }
function frame() {
    animation = null;
    void paint();
    if (!audio.paused && !audio.ended) animation = requestAnimationFrame(frame);
}
function refresh() { invalidate(); void paint(); }
async function play() {
    try { error.textContent = ''; await audio.play(); }
    catch (e) { error.textContent = e.message; }
}
function changeScenario() {
    audio.pause();
    invalidate();
    audio.src = `/api/audio?scenario=${selection()}`;
    audio.load();
    error.textContent = '';
    void paint();
}
audio.addEventListener('play', () => { if (animation === null) frame(); });
audio.addEventListener('pause', () => { if (animation !== null) cancelAnimationFrame(animation); animation = null; refresh(); });
audio.addEventListener('seeked', refresh);
audio.addEventListener('ended', refresh);
audio.addEventListener('loadedmetadata', refresh);
document.querySelector('#play').addEventListener('click', play);
document.querySelector('#pause').addEventListener('click', () => audio.pause());
document.querySelector('#restart').addEventListener('click', () => { audio.currentTime = 0; refresh(); void play(); });
scenario.addEventListener('change', changeScenario);
changeScenario();

const runtimeIntensity = document.querySelector('#runtime-intensity');
const runtimeXpos = document.querySelector('#runtime-xpos');
const runtimeApply = document.querySelector('#runtime-apply');
const runtimeStatus = document.querySelector('#runtime-status');
const runtimeError = document.querySelector('#runtime-error');
const runtimeAudio = document.querySelector('#runtime-audio');
const runtimeScene = document.querySelector('#runtime-scene');
let runtimeGeneration = 0;
let runtimeParameters = [];
async function loadRuntimeInfo() {
    try {
        const response = await fetch('/api/runtime/info');
        if (!response.ok) throw new Error('Runtime schema request failed');
        const info = await response.json();
        runtimeParameters = info.parameters;
        for (const [name, control] of [['intensity', runtimeIntensity], ['xpos', runtimeXpos]]) {
            const parameter = runtimeParameters.find(p => p.name === name);
            control.min = parameter.minimum; control.max = parameter.maximum; control.step = 'any';
        }
        runtimeStatus.value = `Compiled once · tokenize ${info.statistics.tokenizations}, parse ${info.statistics.parses}, timeline ${info.statistics.timelineCompilations}`;
        await applyRuntimeValues();
    } catch (e) { runtimeError.textContent = e.message; }
}
async function applyRuntimeValues() {
    const intensity = Number(runtimeIntensity.value), xpos = Number(runtimeXpos.value);
    const values = { intensity, xpos };
    const invalid = runtimeParameters.find(p => !Number.isFinite(values[p.name]) || values[p.name] < p.minimum || values[p.name] > p.maximum);
    if (invalid || runtimeIntensity.value.trim() === '' || runtimeXpos.value.trim() === '') {
        runtimeError.textContent = invalid ? `${invalid.name} requires a decimal from ${invalid.minimum} to ${invalid.maximum}.` : 'Enter a decimal value for each parameter.';
        return;
    }
    const generation = ++runtimeGeneration;
    runtimeError.textContent = '';
    const params = new URLSearchParams({ intensity: String(intensity), xpos: String(xpos) });
    runtimeAudio.src = `/api/runtime/audio?${params}`;
    runtimeAudio.load();
    try {
        const response = await fetch(`/api/runtime/scene.svg?${params}&t=2`);
        if (!response.ok) throw new Error(await response.text());
        const blob = await response.blob();
        if (generation !== runtimeGeneration) return;
        if (runtimeScene.dataset.url) URL.revokeObjectURL(runtimeScene.dataset.url);
        const url = URL.createObjectURL(blob);
        runtimeScene.dataset.url = url;
        runtimeScene.src = url;
        runtimeStatus.value = `Applied intensity ${intensity} and x ${xpos}. Audio is a complete offline render.`;
    } catch (e) { if (generation === runtimeGeneration) runtimeError.textContent = e.message; }
}
runtimeApply.addEventListener('click', () => { void applyRuntimeValues(); });
runtimeAudio.addEventListener('error', () => { runtimeError.textContent = 'Runtime audio request failed.'; });
void loadRuntimeInfo();

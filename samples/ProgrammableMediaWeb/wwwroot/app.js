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

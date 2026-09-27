'use strict';
const byId = id => document.getElementById(id);
const fail = message => { byId('error').textContent = message; byId('error').hidden = false; byId('status').textContent = ''; };
(async () => {
    try {
        const response = await fetch('proof.json');
        if (!response.ok) throw new Error(`Timeline request failed (${response.status}).`);
        const proof = await response.json();
        if (proof.schemaVersion !== 2 || !Array.isArray(proof.demos) || proof.demos.length !== 6 ||
            proof.demos.some(d => !Number.isInteger(d.frames) || d.frames < 1 || !d.snapshots.length ||
                d.snapshots.some(s => !/^[A-Za-z0-9-]+$/.test(s.stem) || s.scenes.length !== d.frames)))
            throw new Error('Unsupported or incomplete proof data.');
        const video = byId('video'); let demo, pendingFrame = null;
        const option = (text, value) => { const item = document.createElement('option'); item.textContent = text; item.value = value; return item; };
        for (const d of proof.demos) byId('demo').append(option(d.title, d.id));
        function inspect(seek) {
            const frame = Number(byId('frame').value), snapshot = demo.snapshots[Number(byId('snapshot').value)];
            const scene = snapshot.scenes[frame];
            byId('frame-label').textContent = `${frame} / ${demo.frames - 1}`;
            byId('scene').textContent = JSON.stringify(scene, null, 2);
            const included = scene.Layers.filter(layer => layer.Included).length;
            byId('summary').textContent = `${(frame / demo.fps).toFixed(3)} s · ${scene.Clips.length} visible clip(s) · ${included}/${scene.Layers.length} included layer(s)`;
            if (seek) {
                video.pause(); pendingFrame = frame;
                if (video.readyState) { video.currentTime = frame / demo.fps; pendingFrame = null; }
            }
        }
        function selectSnapshot() {
            const snapshot = demo.snapshots[Number(byId('snapshot').value)];
            const file = `${snapshot.stem}.${byId('format').value}`;
            byId('error').hidden = true; byId('status').textContent = `${proof.demos.length} validated demos ready. Viewing ${demo.title}.`;
            pendingFrame = null; video.pause(); video.src = file; video.load();
            byId('download').href = file;
            byId('parameters').textContent = Object.keys(snapshot.parameters).length ? JSON.stringify(snapshot.parameters, null, 2) : 'No runtime parameters in this example.';
            byId('frame').value = 0; inspect(false);
        }
        function selectDemo() {
            demo = proof.demos.find(d => d.id === byId('demo').value);
            byId('description').textContent = demo.description;
            byId('dimensions').textContent = `${demo.width} × ${demo.height} · ${demo.frames} frames · ${demo.fps} fps · ${(demo.frames / demo.fps).toFixed(1)} seconds`;
            video.style.aspectRatio = `${demo.width} / ${demo.height}`;
            byId('frame').max = demo.frames - 1;
            byId('transition').textContent = `Inspect key moment · frame ${demo.inspectFrame}`;
            byId('script').textContent = JSON.stringify(demo.script, null, 2);
            byId('snapshot').replaceChildren(...demo.snapshots.map((s, i) => option(`${s.name} · ${i ? 'alternate binding' : 'default binding'}`, String(i))));
            byId('snapshot').disabled = demo.snapshots.length === 1;
            selectSnapshot();
        }
        byId('demo').addEventListener('change', selectDemo);
        byId('snapshot').addEventListener('change', selectSnapshot); byId('format').addEventListener('change', selectSnapshot);
        byId('frame').addEventListener('input', () => inspect(true));
        byId('transition').addEventListener('click', () => { byId('frame').value = demo.inspectFrame; inspect(true); });
        video.addEventListener('loadedmetadata', () => { if (pendingFrame !== null) { video.currentTime = pendingFrame / demo.fps; pendingFrame = null; } });
        video.addEventListener('timeupdate', () => { if (!video.paused && demo) { byId('frame').value = Math.min(demo.frames - 1, Math.floor(video.currentTime * demo.fps)); inspect(false); } });
        video.addEventListener('error', () => fail('This browser could not play the selected video. Try the other format or download it for local playback.'));
        byId('workspace').hidden = false; selectDemo();
    } catch (error) { fail(`Unable to load VideoLab: ${error.message} Reload to retry.`); }
})();

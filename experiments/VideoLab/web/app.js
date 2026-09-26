'use strict';
const byId = id => document.getElementById(id);
const fail = message => { byId('error').textContent = message; byId('error').hidden = false; byId('status').textContent = ''; };
(async () => {
    try {
        const response = await fetch('proof.json');
        if (!response.ok) throw new Error(`Timeline request failed (${response.status}).`);
        const proof = await response.json();
        if (proof.schemaVersion !== 1 || proof.frames !== 180 || proof.snapshots.length !== 2 ||
            proof.snapshots.some(s => !['A', 'B'].includes(s.name) || s.scenes.length !== proof.frames))
            throw new Error('Unsupported or incomplete proof data.');
        const video = byId('video');
        function inspect(seek) {
            const frame = Number(byId('frame').value), snapshot = proof.snapshots[Number(byId('snapshot').value)];
            const scene = snapshot.scenes[frame];
            byId('frame-label').textContent = `${frame} / ${proof.frames - 1}`;
            byId('scene').textContent = JSON.stringify(scene, null, 2);
            byId('summary').textContent = `${(frame / proof.fps).toFixed(3)} s · ${scene.Clips.length} visible clip(s) · ${scene.Shapes.length} shape(s)`;
            if (seek && video.readyState) { video.pause(); video.currentTime = frame / proof.fps; }
        }
        function select() {
            const snapshot = proof.snapshots[Number(byId('snapshot').value)];
            const file = `${snapshot.name}.${byId('format').value}`;
            video.pause(); video.src = file; video.load();
            byId('download').href = file;
            byId('parameters').textContent = JSON.stringify(snapshot.parameters, null, 2);
            byId('frame').value = 0; inspect(false);
        }
        byId('snapshot').addEventListener('change', select); byId('format').addEventListener('change', select);
        byId('frame').addEventListener('input', () => inspect(true));
        byId('transition').addEventListener('click', () => { byId('frame').value = 98; inspect(true); });
        video.addEventListener('timeupdate', () => { if (!video.paused) { byId('frame').value = Math.min(proof.frames - 1, Math.floor(video.currentTime * proof.fps)); inspect(false); } });
        video.addEventListener('error', () => fail('This browser could not play the selected video. Try the other format or download it for local playback.'));
        byId('script').textContent = JSON.stringify(proof.script, null, 2);
        byId('workspace').hidden = false; byId('status').textContent = 'Two validated snapshots ready.'; select();
    } catch (error) { fail(`Unable to load VideoLab: ${error.message} Reload to retry.`); }
})();

// Browser encoder adapter for TemporalVideoExportPlan. The plan is produced by
// SoundScript.Media from VisualTimeline.StateAt(t); this code only rasterizes
// those supplied observations and combines them with the existing MIDI rail.
window.SoundScriptVideoExporter = (function () {
    const width = 1280;
    const height = 720;

    function supportedMimeType() {
        if (!window.MediaRecorder || !HTMLCanvasElement.prototype.captureStream)
            return null;
        return [
            'video/webm;codecs=vp9,opus',
            'video/webm;codecs=vp8,opus',
            'video/webm'
        ].find(type => MediaRecorder.isTypeSupported(type)) || null;
    }

    function property(element, name, fallback) {
        const found = (element.properties || []).find(item => item.name.toLowerCase() === name);
        return found ? Number(found.value) : fallback;
    }

    function draw(ctx, sample) {
        ctx.fillStyle = '#0b0d12';
        ctx.fillRect(0, 0, width, height);
        const gradient = ctx.createRadialGradient(width * 0.5, height * 0.3, 10, width * 0.5, height * 0.4, width * 0.7);
        gradient.addColorStop(0, 'rgba(129, 140, 248, .32)');
        gradient.addColorStop(1, 'rgba(11, 13, 18, 0)');
        ctx.fillStyle = gradient;
        ctx.fillRect(0, 0, width, height);

        for (const element of sample.elements || []) {
            const name = element.name.toLowerCase();
            const opacity = Math.max(0, Math.min(1, property(element, 'opacity', 1)));
            ctx.globalAlpha = opacity;
            if (name === 'circle') {
                const radius = Math.max(12, Math.min(260, property(element, 'radius', 72)));
                ctx.fillStyle = '#6ee7b7';
                ctx.beginPath();
                ctx.arc(width / 2, height / 2, radius, 0, Math.PI * 2);
                ctx.fill();
            } else if (name === 'sparkle') {
                ctx.fillStyle = '#fbbf24';
                ctx.font = '180px system-ui';
                ctx.textAlign = 'center';
                ctx.fillText('✦', width * .73, height * .35);
            } else {
                ctx.fillStyle = name === 'outro' ? '#c4b5fd' : '#e8ecf4';
                ctx.font = '700 58px system-ui';
                ctx.textAlign = 'center';
                ctx.fillText(name === 'intro' ? 'A visual idea begins' : element.name, width / 2, height * .78);
            }
        }
        ctx.globalAlpha = 1;
        ctx.fillStyle = 'rgba(232, 236, 244, .7)';
        ctx.font = '22px ui-monospace, monospace';
        ctx.textAlign = 'left';
        ctx.fillText(`SoundScript · t = ${sample.timeSeconds.toFixed(3)}s`, 38, 52);
    }

    function wait(milliseconds) {
        return new Promise(resolve => setTimeout(resolve, milliseconds));
    }

    async function exportWebm(plan, midiBytes, filename) {
        const mimeType = supportedMimeType();
        if (!mimeType)
            throw new Error('This browser cannot encode WebM from Canvas and Web Audio. Use Chrome, Edge, or Firefox for V1 clip export.');
        if (!plan || !Array.isArray(plan.samples) || plan.samples.length === 0)
            throw new Error('The temporal export plan contains no samples.');

        const canvas = document.createElement('canvas');
        canvas.width = width;
        canvas.height = height;
        const context = canvas.getContext('2d');
        const canvasStream = canvas.captureStream(plan.framesPerSecond);
        const audio = await window.SoundScriptMidi.startExportPlayback(midiBytes);
        const stream = new MediaStream([
            ...canvasStream.getVideoTracks(),
            ...audio.stream.getAudioTracks()
        ]);
        if (stream.getAudioTracks().length === 0)
            throw new Error('The browser did not provide an audio capture track for this export.');

        const chunks = [];
        const recorder = new MediaRecorder(stream, { mimeType, videoBitsPerSecond: 4_000_000 });
        recorder.addEventListener('dataavailable', event => { if (event.data.size) chunks.push(event.data); });
        const finished = new Promise((resolve, reject) => {
            recorder.addEventListener('stop', () => resolve());
            recorder.addEventListener('error', () => reject(recorder.error || new Error('MediaRecorder failed.')));
        });

        recorder.start();
        const started = performance.now();
        for (const sample of plan.samples) {
            draw(context, sample);
            const intendedElapsed = sample.timeSeconds * 1000;
            const remaining = intendedElapsed - (performance.now() - started);
            if (remaining > 0)
                await wait(remaining);
        }
        const remainingDuration = plan.durationSeconds * 1000 - (performance.now() - started);
        if (remainingDuration > 0)
            await wait(remainingDuration);
        recorder.stop();
        await finished;
        window.SoundScriptMidi.stop();

        const blob = new Blob(chunks, { type: mimeType });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = filename || 'soundscript-temporal-clip.webm';
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    }

    return { exportWebm };
})();

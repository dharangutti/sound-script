// Browser codec adapter for a TemporalVisualExportPlan. The Media layer has
// already evaluated VisualTimeline.StateAt(t) and converted it to canonical
// primitives. This adapter only paces those supplied scenes and muxes their
// deterministic WAV rail into a browser-native WebM container.
window.SoundScriptVideoExporter = (function () {
    const width = 1280;
    const height = 720;

    function supportedMimeType() {
        if (!window.MediaRecorder || !HTMLCanvasElement.prototype.captureStream) {
            return null;
        }

        return [
            'video/webm;codecs=vp9,opus',
            'video/webm;codecs=vp8,opus',
            'video/webm'
        ].find(type => MediaRecorder.isTypeSupported(type)) || null;
    }

    function wait(milliseconds) {
        return new Promise(resolve => setTimeout(resolve, milliseconds));
    }

    async function waitUntil(performanceStart, targetElapsedMilliseconds) {
        const remaining = targetElapsedMilliseconds - (performance.now() - performanceStart);
        if (remaining > 0) {
            await wait(remaining);
        }
    }

    function stopTracks(stream) {
        if (!stream) {
            return;
        }

        for (const track of stream.getTracks()) {
            track.stop();
        }
    }

    function download(blob, filename) {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = filename || 'soundscript-temporal-clip.webm';
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    }

    function validatePlan(plan) {
        if (!plan || !Array.isArray(plan.samples) || plan.samples.length === 0) {
            throw new Error('The temporal export plan contains no samples.');
        }
        if (!Number.isFinite(Number(plan.durationSeconds)) || Number(plan.durationSeconds) <= 0) {
            throw new Error('The temporal export plan has an invalid duration.');
        }
        if (!Number.isFinite(Number(plan.framesPerSecond)) || Number(plan.framesPerSecond) <= 0) {
            throw new Error('The temporal export plan has an invalid frame rate.');
        }
        if (!window.SoundScriptVisualRenderer || typeof window.SoundScriptVisualRenderer.renderScene !== 'function') {
            throw new Error('The canonical SoundScript visual renderer is unavailable. Reload the Playground and try again.');
        }
        if (!window.SoundScriptAudio || typeof window.SoundScriptAudio.startExportPlayback !== 'function') {
            throw new Error('The deterministic SoundScript WAV export rail is unavailable. Reload the Playground and try again.');
        }
    }

    async function stopRecorderAndWait(recorder, finished) {
        if (!recorder || recorder.state === 'inactive') {
            return;
        }

        recorder.stop();
        try {
            await finished;
        } catch (_) {
            // Preserve the original export failure if recorder shutdown also
            // reports an error.
        }
    }

    async function exportWebm(plan, audioBytes, filename) {
        const mimeType = supportedMimeType();
        if (!mimeType) {
            throw new Error('This browser cannot encode WebM from Canvas and Web Audio. Use Chrome, Edge, or Firefox for clip export.');
        }
        validatePlan(plan);

        const canvas = document.createElement('canvas');
        canvas.width = width;
        canvas.height = height;
        const context = canvas.getContext('2d');
        if (!context) {
            throw new Error('The browser could not create a 2D canvas for video export.');
        }

        // Manual canvas capture prevents the browser repaint cadence from
        // silently adding, dropping, or resampling StateAt observations. One
        // supplied plan sample becomes exactly one requested video frame.
        const canvasStream = canvas.captureStream(0);
        const videoTrack = canvasStream.getVideoTracks()[0];
        if (!videoTrack || typeof videoTrack.requestFrame !== 'function') {
            stopTracks(canvasStream);
            throw new Error('This browser cannot explicitly capture canvas frames for deterministic export. Use a current Chrome, Edge, or Firefox build.');
        }

        let audio;
        let mediaStream;
        let recorder;
        let finished;
        let audioStarted = false;

        try {
            // Decoding/preparing audio may take time, but it does not start the
            // source. That happens only after MediaRecorder is ready below.
            audio = await window.SoundScriptAudio.startExportPlayback(audioBytes);
            mediaStream = new MediaStream([
                ...canvasStream.getVideoTracks(),
                ...audio.stream.getAudioTracks()
            ]);
            if (mediaStream.getAudioTracks().length === 0) {
                throw new Error('The browser did not provide an audio capture track for this export.');
            }

            const chunks = [];
            recorder = new MediaRecorder(mediaStream, { mimeType, videoBitsPerSecond: 4_000_000 });
            recorder.addEventListener('dataavailable', event => {
                if (event.data.size) {
                    chunks.push(event.data);
                }
            });
            finished = new Promise((resolve, reject) => {
                recorder.addEventListener('stop', resolve, { once: true });
                recorder.addEventListener('error', () => reject(recorder.error || new Error('MediaRecorder failed.')), { once: true });
            });

            recorder.start();

            // `started` schedules the WAV source at an explicit future
            // AudioContext time only after the recorder has attached to both
            // streams. Its performance-clock anchor lets the video start at
            // the same semantic instant as the audio rather than 50 ms early.
            const audioTiming = audio.started();
            audioStarted = true;
            const semanticStart = audioTiming.scheduledAtPerformanceTime + audioTiming.leadInMilliseconds;

            for (const sample of plan.samples) {
                const sampleTime = Number(sample.timeSeconds);
                if (!Number.isFinite(sampleTime) || sampleTime < 0) {
                    throw new Error('The temporal export plan contains an invalid sample time.');
                }

                await waitUntil(semanticStart, sampleTime * 1000);
                window.SoundScriptVisualRenderer.renderScene(context, sample);
                videoTrack.requestFrame();
            }

            // Hold the final state for the final frame interval. The audio and
            // video share the lead-in, so they remain aligned throughout the
            // program without putting that renderer detail into the DSL.
            await waitUntil(semanticStart, Number(plan.durationSeconds) * 1000);
            recorder.stop();
            await finished;

            download(new Blob(chunks, { type: mimeType }), filename);
        } catch (error) {
            if (!audioStarted && audio && typeof audio.stopBeforeStart === 'function') {
                audio.stopBeforeStart();
            }
            await stopRecorderAndWait(recorder, finished);
            throw error;
        } finally {
            window.SoundScriptAudio.stop();
            stopTracks(mediaStream);
            stopTracks(canvasStream);
        }
    }

    return { exportWebm };
})();

// The audio element is the only clock. Coalesce callbacks and cap playback work at 20 FPS.
export function attach(audio, receiver, generation) {
    let disposed = false, busy = false, pending = false, timer;
    async function update() {
        if (disposed) return;
        if (busy) { pending = true; return; }
        busy = true;
        try {
            await receiver.invokeMethodAsync('UpdateAudioTime', generation, audio.currentTime);
        } finally {
            busy = false;
            if (pending && !disposed) { pending = false; void update(); }
        }
    }
    function sync() {
        clearInterval(timer);
        if (!audio.paused && !audio.ended) timer = setInterval(update, 50);
        void update();
    }
    const events = ['play', 'pause', 'seeking', 'seeked', 'ended', 'loadedmetadata'];
    for (const event of events) audio.addEventListener(event, sync);
    sync();
    return {
        dispose() {
            disposed = true;
            clearInterval(timer);
            for (const event of events) audio.removeEventListener(event, sync);
            audio.pause();
        }
    };
}

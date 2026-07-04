// Vocal preview for voice { } blocks — speaks lyric words at their note times
// using the browser's Web Speech API (speechSynthesis).
//
// MIDI carries lyrics as FF 05 meta events, which have no audio payload, so the
// playground overlays synthesized speech on top of the melody. Pitch follows the
// sung MIDI note (coarsely — speechSynthesis pitch is 0..2), and each word's
// speaking rate is stretched toward its note duration.
//
// This is a preview: speech voices vary by browser/OS. The exported MIDI file
// (with its lyric events) remains fully deterministic.

window.SoundScriptVoice = (function () {
    let timers = [];
    let active = false;

    function isSupported() {
        return typeof window.speechSynthesis !== 'undefined'
            && typeof window.SpeechSynthesisUtterance !== 'undefined';
    }

    function pitchForMidi(midi) {
        // map the singable range around middle C (C4 = 60) onto the utterance
        // pitch range 0.5..1.9, so C4 speaks at natural pitch and higher notes rise
        const pitch = 1 + (midi - 60) / 24;
        return Math.min(1.9, Math.max(0.5, pitch));
    }

    function rateForDuration(text, durationMs) {
        // ~220ms per syllable-ish chunk at rate 1; stretch toward the note length
        const chunks = Math.max(1, Math.round(text.length / 3));
        const naturalMs = chunks * 220;
        const rate = naturalMs / Math.max(120, durationMs);
        return Math.min(1.8, Math.max(0.6, rate));
    }

    // words: [{ text, startMs, durationMs, midi }]
    function speak(words) {
        stop();

        if (!isSupported() || !Array.isArray(words) || words.length === 0) {
            return isSupported();
        }

        active = true;

        for (const word of words) {
            const timer = setTimeout(() => {
                if (!active) {
                    return;
                }

                const utterance = new SpeechSynthesisUtterance(word.text);
                utterance.pitch = pitchForMidi(word.midi);
                utterance.rate = rateForDuration(word.text, word.durationMs);
                utterance.volume = 1.0;
                window.speechSynthesis.speak(utterance);
            }, Math.max(0, word.startMs));

            timers.push(timer);
        }

        return true;
    }

    function stop() {
        active = false;
        for (const timer of timers) {
            clearTimeout(timer);
        }
        timers = [];

        if (isSupported()) {
            window.speechSynthesis.cancel();
        }
    }

    return {
        isSupported,
        speak,
        stop
    };
})();

// Deterministic in-browser MIDI player using Web Audio + local GM soundfont samples.

window.SoundScriptMidi = (function () {
    let audioContext = null;
    let masterGain = null;
    let activeNodes = [];
    let stopTimer = null;

    function readVarLen(data, offset) {
        let value = 0;
        let pos = offset;
        while (pos < data.length) {
            const byte = data[pos++];
            value = (value << 7) | (byte & 0x7f);
            if ((byte & 0x80) === 0) {
                break;
            }
        }
        return { value, pos };
    }

    function readUint32(data, offset) {
        return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
    }

    function readUint16(data, offset) {
        return (data[offset] << 8) | data[offset + 1];
    }

    function parseMidi(bytes) {
        const data = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
        if (data.length < 14 || String.fromCharCode(...data.slice(0, 4)) !== 'MThd') {
            throw new Error('Invalid MIDI file.');
        }

        const headerLength = readUint32(data, 4);
        const trackCount = readUint16(data, 10);
        const division = readUint16(data, 12);
        let ticksPerBeat = division;

        if (division & 0x8000) {
            ticksPerBeat = 480;
        }

        let offset = 8 + headerLength;
        const events = [];
        let tempo = 500000; // default 120 BPM

        for (let track = 0; track < trackCount; track++) {
            if (offset + 8 > data.length || String.fromCharCode(...data.slice(offset, offset + 4)) !== 'MTrk') {
                break;
            }

            const trackLength = readUint32(data, offset + 4);
            offset += 8;
            const trackEnd = offset + trackLength;
            let tick = 0;
            let runningStatus = 0;

            while (offset < trackEnd) {
                const delta = readVarLen(data, offset);
                tick += delta.value;
                offset = delta.pos;

                if (offset >= trackEnd) {
                    break;
                }

                let status = data[offset];
                if (status < 0x80) {
                    status = runningStatus;
                    offset -= 1;
                } else {
                    runningStatus = status;
                }

                offset += 1;
                const type = status & 0xf0;
                const channel = status & 0x0f;

                if (type === 0x90) {
                    const note = data[offset++];
                    const velocity = data[offset++];
                    if (velocity > 0) {
                        events.push({ tick, type: 'on', note, velocity, channel });
                    } else {
                        events.push({ tick, type: 'off', note, channel });
                    }
                } else if (type === 0x80) {
                    const note = data[offset++];
                    offset++; // release velocity
                    events.push({ tick, type: 'off', note, channel });
                } else if (type === 0xc0) {
                    const program = data[offset++];
                    events.push({ tick, type: 'program', program, channel });
                } else if (type === 0xb0) {
                    offset += 2;
                } else if (type === 0xe0) {
                    offset += 2;
                } else if (status === 0xff) {
                    const metaType = data[offset++];
                    const metaLen = readVarLen(data, offset);
                    offset = metaLen.pos;
                    if (metaType === 0x51 && metaLen.value === 3) {
                        tempo = (data[offset] << 16) | (data[offset + 1] << 8) | data[offset + 2];
                    }
                    offset += metaLen.value;
                    runningStatus = 0;
                } else if (status === 0xf0 || status === 0xf7) {
                    const len = readVarLen(data, offset);
                    offset = len.pos + len.value;
                    runningStatus = 0;
                } else {
                    break;
                }
            }

            offset = trackEnd;
        }

        events.sort((a, b) => a.tick - b.tick || (a.type === 'off' ? 1 : -1));

        const channelPrograms = new Array(16).fill(0);
        const noteOn = new Map();
        const scheduled = [];

        for (const event of events) {
            const seconds = (event.tick / ticksPerBeat) * (tempo / 1_000_000);

            if (event.type === 'program') {
                channelPrograms[event.channel] = event.program;
                continue;
            }

            const key = event.channel + ':' + event.note;

            if (event.type === 'on') {
                noteOn.set(key, {
                    start: seconds,
                    velocity: event.velocity,
                    program: channelPrograms[event.channel]
                });
            } else if (event.type === 'off') {
                const startInfo = noteOn.get(key);
                if (startInfo) {
                    scheduled.push({
                        note: event.note,
                        velocity: startInfo.velocity,
                        program: startInfo.program,
                        start: startInfo.start,
                        duration: Math.max(0.05, seconds - startInfo.start)
                    });
                    noteOn.delete(key);
                }
            }
        }

        for (const [key, startInfo] of noteOn.entries()) {
            const note = Number(key.split(':')[1]);
            scheduled.push({
                note,
                velocity: startInfo.velocity,
                program: startInfo.program,
                start: startInfo.start,
                duration: 0.5
            });
        }

        const programs = [...new Set(scheduled.map(n => n.program))];
        const totalDuration = scheduled.reduce((max, n) => Math.max(max, n.start + n.duration), 0);
        return { notes: scheduled, programs, totalDuration };
    }

    function clearScheduled() {
        for (const node of activeNodes) {
            try {
                node.source.stop();
            } catch (_) {
                // already stopped
            }
        }
        activeNodes = [];
        if (stopTimer) {
            clearTimeout(stopTimer);
            stopTimer = null;
        }
    }

    function scheduleNotes(parsedMidi) {
        const { notes, totalDuration } = parsedMidi;
        const now = audioContext.currentTime + 0.05;

        for (const note of notes) {
            const nodes = SoundScriptSoundfont.playNote(
                note.note,
                note.velocity,
                now + note.start,
                note.duration,
                masterGain,
                note.program
            );
            if (nodes) {
                activeNodes.push(nodes);
            }
        }

        stopTimer = setTimeout(() => {
            clearScheduled();
        }, (totalDuration + 0.5) * 1000);
    }

    async function startPlayback(midiBytes) {
        clearScheduled();
        const parsedMidi = parseMidi(midiBytes);

        if (!audioContext) {
            audioContext = new (window.AudioContext || window.webkitAudioContext)();
            masterGain = audioContext.createGain();
            masterGain.gain.value = 0.85;
            masterGain.connect(audioContext.destination);
        }

        await audioContext.resume();

        if (audioContext.state === 'suspended') {
            console.warn('AudioContext blocked — ensure silent mode is off.');
            return;
        }

        await SoundScriptSoundfont.load(audioContext, parsedMidi.programs);

        scheduleNotes(parsedMidi);
    }

    async function primePrograms(programs) {
        if (!SoundScriptSoundfont.prefetch) {
            return;
        }

        await SoundScriptSoundfont.prefetch(programs);
    }

    function stop() {
        clearScheduled();
    }

    function download(base64, filename) {
        window.SoundScriptDownload.fromBase64(base64, filename || 'soundscript.mid', 'audio/midi');
    }

    return {
        startPlayback,
        primePrograms,
        stop,
        download
    };
})();

window.startPlayback = window.SoundScriptMidi.startPlayback;

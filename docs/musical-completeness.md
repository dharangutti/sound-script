# Musical completeness audit

The parser, AST, MIDI interpreter, MIDI writer and Wave adapter were compared
against the language reference. The implementation now provides comprehensive
conventional MIDI pitch and rhythm coverage while retaining the existing
readable DSL and custom Wave/SoundCSS path.

Supported directly:

- MIDI pitches 0–127, enharmonic sharp/flat/natural spellings, rests, ties,
  dotted values, triplets, arbitrary single-note tuplets and grace events.
- Major, minor, diminished, augmented, suspended, sixth, seventh, ninth,
  eleventh, thirteenth, add9 and half-diminished chord intervals, plus the
  existing inversion/drop/spread voicings and deterministic polyphony.
- Tempo, tempo ramps, time signatures, dynamics through `ppp`/`fff`, velocity,
  articulations, swing, push/pull, humanization, arpeggiation, strumming,
  multiple tracks and layers, voice/speak, visual timelines and media export.
- Every General MIDI Level 1 program by compact name or number 0–127. Existing
  SoundScript instrument names map exactly as before.

Partially supported or backend-specific:

- Wave rendering consumes the shared AST but intentionally uses synthesis and
  SoundCSS timbres rather than General MIDI program changes.
- Phrase envelopes, swing and humanization are deterministic in the MIDI path;
  some are intentionally lighter-weight in Wave rendering.
- The MIDI writer currently represents note events, program changes, lyrics,
  tempo and time signatures. It does not yet expose a general source-level
  control-change or pitch-bend event model.

Recommended future work:

- Add first-class `cc`, sustain, modulation, expression, pan and pitch-bend
  statements backed by a shared automation/event model.
- Add a channel-10 percussion namespace with the standard General MIDI drum
  key names and tests for kit events.
- Add explicit transposing-instrument declarations and practical range checks;
  these belong above raw program selection and require notation-policy choices.

The audit deliberately leaves these latter items documented instead of
pretending that a program change alone models controller automation,
percussion semantics or orchestral transposition.

# What's New in V6

SoundScript **V6** adds an **opt-in `.ss` export** for the text-to-melody
pipeline — a new `--emit-ss <path>` flag on `compose` and `prosody` that
serializes the AST those composers already build into human-editable `.ss`
DSL source, in addition to the `.mid` file they already produce. Nothing
about the existing direct path changes; this is a second, optional detour.

## The fix

Before V6, `compose`/`prosody` went straight from text to MIDI with no
inspectable, editable middle step:

```
text → syllables → phonemes → gestures → AST → MidiGenerator.Write → .mid
```

If you wanted to nudge one note of a composed melody, your only option was to
hand-write a whole `.ss` script from scratch — the machine-generated
composition was a dead end once it became MIDI. V6 adds a detour that stops
at the AST and prints it as text instead:

```
compose "text" --emit-ss melody.ss     (also still writes .mid directly, unchanged)
    ↓
melody.ss (human-editable .ss source)
    ↓ hand-edit (optional)
run melody.ss melody.mid
    ↓
render melody.mid --css style.ssc --out out.wav
```

## New library API: `SsPrinter`

| Module | Purpose |
|--------|---------|
| `SoundScript.Parser.SsPrinter` | `Print(ProgramNode)` — serializes a pre-interpretation AST back into `.ss` source text that the existing `Tokenizer`/`Parser` can re-parse |

`SsPrinter` only needs to handle the node shapes `PhonemeComposer.BuildAst`
and `ProsodyComposer.BuildAst` can produce (`TempoNode`, `TrackNode`,
`PhraseNode`, `PhraseEnvelopeNode`, `NoteNode`) — every one of them already
has a lossless textual form in the grammar. Anything it doesn't recognize, or
a value it can't faithfully represent (a tied note, an explicit "no
envelope" statement), throws `NotSupportedException` rather than silently
dropping data.

## New CLI flag: `--emit-ss <path>`

```bash
dotnet run --project src/SoundScript.Cli -- compose "Twinkle twinkle little star" --emit-ss twinkle.ss
dotnet run --project src/SoundScript.Cli -- run twinkle.ss twinkle-viass.mid
```

Available on both `compose` and `prosody`, alongside the existing `--append`.
`--emit-ss` and `--append` cannot be combined: `--append` merges the composed
track into an already-interpreted program and never keeps a single AST for
"existing script + composed track," so there's no correct program to print —
combining the two flags is rejected with a clear error instead of silently
emitting a partial `.ss` file.

## Playground

Both **Compose from text** and **Compose with Prosody** now also produce a
`.ss` export: an inline **View .ss source** panel plus a **Download .ss**
button, next to the existing MIDI/WAV downloads.

## No breaking changes

- Default `compose`/`prosody` output (no `--emit-ss`) is byte-for-byte
  identical to before V6.
- `run`'s existing `.ss` → `.mid` behavior is unchanged — `SsPrinter` output
  is read by the same parser as any hand-written script, no special-casing.
- SoundCSS/`.ssc` handling is untouched — styling still applies only at the
  MIDI → WAV render step, regardless of whether the MIDI came from `run`,
  `compose`, or `prosody`.

## Determinism / round-trip fidelity

`compose "text" --emit-ss out.ss` followed by `run out.ss out.mid` produces
MIDI identical to direct `compose "text" out.mid` on tempo, pitches,
durations, and velocities. The printer always emits the composed BPM as the
literal first statement (`tempo <N>`) — this is deliberate: a `tempo`
statement at beat 0 was silently dropped by an earlier bug in
`TempoAutomationMap.GetTempoMapPoints()` (fixed in the same pipeline this
feature builds on), and every composer emits its tempo at beat 0, so this is
exactly the case the round-trip tests guard against regressing.

## Tests

New `EmitSsRoundTripTests.cs`:

- Byte-identical MIDI via `BuildAst` → `SsPrinter.Print` → reparse → generate,
  compared against the direct `BuildAst` → generate path, for both
  `PhonemeComposer` and `ProsodyComposer`.
- Beat-zero tempo: printed text's first statement is `tempo <N>`, and the
  regenerated MIDI has exactly one tempo change equal to the composed BPM.
- Human-formatted output: multiple lines, indented, not a single-line dump.
- Hand-edit fidelity: editing one note token in the printed text and
  reparsing changes exactly that note and nothing else.
- Fail-loud guards: a tied note, a `None`-envelope node, and an unsupported
  node type all raise `NotSupportedException` instead of printing silently
  lossy output.

All existing tests continue to pass unmodified.

## Protected subsystems

Core, `Tokenizer`, `Parser`, `NotationParser`, the `Interpreter`, the MIDI
generator, Voice, SoundCSS, and Timbre are all **unchanged** — `SsPrinter` is
a new, additive file in `SoundScript.Parser`, and the only existing file
touched for the CLI is `Program.cs` (a new opt-in flag on two verbs).

## Previous releases

→ [What's new in V5](whats-new-v5.md) — word-level prosody (`ProsodyComposer`)

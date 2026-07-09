# What's New in V7

SoundScript **V7** adds **SoundScript.Wave** — a second, independent backend
that renders the same `.ss` grammar straight to WAV, with no MIDI step at
any point. It runs as a parallel rail alongside the existing MIDI backend:
one shared grammar, two independent renderers, selected by what a script
uses. A `.ss` script with none of V7's new directives produces identical
output through either backend; nothing about the existing MIDI path changes.

## The fix

Every prior release rendered through the same pipeline: `.ss` → AST →
MIDI → (optionally) offline timbre synthesis. That pipeline is unchanged and
still the default. But it means anything that isn't expressible as a MIDI
event — a post-mix audio effect, a phoneme-level frequency — had nowhere to
live. V7 adds a second path that goes straight from AST to a raw audio
buffer instead:

```
.ssw (or .ss with wave-only directives)
    ↓ (existing) Tokenizer → Parser → AST
    ↓
SoundScript.Wave: AstToNoteEventAdapter → Mixer → MasterEffectChain → WavWriter
    ↓
output.wav
```

`SoundScript.Wave` depends only on `SoundScript.Core` — it can be added or
removed without touching `SoundScript.Midi`, `SoundScript.Timbre`, or the
parser, and the MIDI backend is unaware it exists.

## New grammar: `effect`, `speak`, and `humanize`'s named form

→ full reference: [wave-grammar.md](wave-grammar.md)

| Directive | Scope | What it's for |
|-----------|-------|----------------|
| `effect delay time= feedback= mix=` | Master-only, post-mix | Feedback delay line |
| `effect filter type= cutoff=` | Master-only, post-mix | Single-pole lowpass/highpass |
| `speak "text" voice= seed=` | Per-track | Phoneme-level frequency mapping (proof-of-concept scope) |
| `humanize timing= velocity= seed=` | Per-track | Named-parameter extension of the existing `humanize` directive |

Each of these was added only after passing a two-part test: the feature
can't be expressed with existing `.ss` grammar, and it stays fully isolated
to the wave backend. `humanize`'s named form is the one exception worth
calling out — it deliberately extends an existing directive rather than
introducing a new keyword, since `humanize` already existed.

If a script using `effect` or `speak` is run through the MIDI backend
instead, the interpreter rejects it with a named error pointing at the wave
backend, rather than silently ignoring the directive.

## Determinism

Same seed + same input = byte-identical WAV, across runs and platforms —
not just "usually reproducible." A few things make that hold:

- A single shared PRNG, `DeterministicRandom`, is the only source of "human
  variation" in the wave pipeline (humanize jitter and prosody tone
  variation both draw from it). It's a pure function of `(seed, index,
  salt)` through integer hashing and one exact IEEE division — deliberately
  not `System.Random`, whose seeded algorithm has changed between .NET
  versions.
- Every seed is either explicit (`seed=42`) or, if omitted, derived from
  content (the track name for `humanize`, the spoken text for `speak`) —
  never the wall clock, never an unseeded generator. See
  [wave-grammar.md](wave-grammar.md#determinism-the-null-seed-behavior) for
  what that means in practice.
- The effects chain, mixer, and WAV writer avoid non-deterministic
  floating-point summation order.

This is verified by a dedicated SHA-256 checksum suite
(`WaveDeterminismTests`) covering the effects chain, seeded humanize, and
`speak` independently, plus one combined script exercising all three
together — at render lengths long enough to actually exercise the delay
line's tail-repeat accumulation and the filter's running state, not just a
two-note clip. That suite now runs in CI across an Ubuntu/Windows/macOS
matrix, not just one OS.

## Playground

The [Playground](https://soundscript.net/playground/) now renders and plays
back `effect`/`speak`/named-`humanize` scripts directly: **Run** checks the
parsed AST for the new node types and, if present, renders through
`SoundScript.Wave` instead of MIDI, reusing the same WAV playback path
`Render Audio` already used for offline timbre output. Wave-specific UI (a
small "SoundScript.Wave · no MIDI step" indicator, an alternate pipeline
diagram) only appears for scripts that actually use the new grammar — an
ordinary `.ss` script's Playground experience is unchanged. A new "Wave
(.ssw)" preset group demonstrates all three additions. The CLI also exposes
a `wave` verb for direct `.ss`/`.ssw` → WAV rendering — see
[cli.md](cli.md#wave--script-to-wav-v7) and
[wave-grammar.md](wave-grammar.md#where-you-can-use-this-today).

## Protected subsystems

`SoundScript.Core`, `SoundScript.Midi`, `SoundScript.Timbre`, and the
existing `.ss` grammar/behavior are **unchanged** — `SoundScript.Wave` is a
new, additive project. The only edits outside it are the Playground wiring
above and the three new AST node types (`EffectNode`, `SpeakNode`, and the
named-form fields on `HumanizeNode`) that the MIDI interpreter explicitly
rejects rather than silently mishandling.

## What SoundScript.Wave is (and isn't)

Per the project's existing messaging guardrails: SoundScript.Wave doesn't
claim to invent wavetable synthesis, ADSR envelopes, or prosody modeling —
those predate the project by decades. What's actually new here is:

1. One shared grammar, two independent backends (MIDI and direct WAV)
2. Byte-identical determinism as a first-class, tested guarantee
3. Zero external dependencies — no DAW, no soundfont, no MIDI stack required
4. Small, embeddable, readable-as-code syntax

The `speak` directive in particular is a small, fixed grapheme/phoneme
table proving the seed-derived tone-variation mechanism — not a
text-to-speech engine. See [wave-grammar.md](wave-grammar.md#scope-shipped-vs-deferred)
for what was deliberately deferred (reverb, per-track effect routing,
multi-language prosody, additional voices).

## Previous releases

→ [What's new in V6](whats-new-v6.md) — editable `.ss` export
→ [What's new in V5](whats-new-v5.md) — word-level prosody
→ [What's new in V4.1.1](whats-new-v4.1.1.md) — timbre quality tuning

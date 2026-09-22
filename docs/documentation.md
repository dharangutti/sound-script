# SoundScript documentation

SoundScript is a deterministic programming language and .NET toolkit for
programmable audio and media. It can turn editable source into MIDI, WAV,
vocals, and synchronized visual media. V13 also includes experimental
audio-to-code transcription for suitable recordings.

## Choose a path

| I want to… | Start here |
|---|---|
| Try SoundScript in five minutes | [Quick start](quick-start.md) |
| Solve a common application task | [Common tasks](common-tasks.md) |
| Use it from a .NET application | [.NET API guide](dotnet-api.md) |
| Understand the package and local installation | [NuGet](nuget.md) |
| See developer-focused application demos | [Application samples](application-samples.md) |
| Build complete NuGet media workflows | [NuGet end to end](nuget-end-to-end.md) |
| Learn the language | [Language reference](language-reference.md) |
| Use the command line | [CLI reference](cli.md) |
| Convert suitable audio into editable source | [Transcription](transcription.md) |
| Understand the implementation boundaries | [Architecture](architecture.md) |

The language reference covers notes, rests, chords, tracks, layers, phrases,
dynamics, timing, tempo, patterns, vocals, Wave and SoundCSS authoring,
visuals, synchronization, and the experimental unpitched hit syntax. Its
dedicated links lead to [Wave grammar](wave-grammar.md), [Vocal](vocal.md),
[Visual timelines](visual-temporal.md), [SoundCSS](soundcss.md), and
[Percussion transcription](percussion-transcription.md).

## Existing reference material

The repository keeps its detailed, versioned references in place. These are
useful follow-on guides rather than replacements for the entry points above.

- [User guide](user-guide.md) — a hands-on tour from notes to media.
- [Examples](examples.md) — runnable scripts and Playground presets.
- [Wave grammar](wave-grammar.md) — direct .ssw audio authoring.
- [Vocal](vocal.md) — lyrics, vocal cues, and offline stems.
- [Visual timelines](visual-temporal.md) — timed visuals and media sync.
- [SoundCSS](soundcss.md) — deterministic timbre styling.
- [Playground guide](PLAYGROUND.md) — browser workflow and export checks.
- [Release checklist](releasing.md) — local package and release preparation.
- [V13 Reliability & Release Hardening](v13-reliability-hardening.md) — clean validation, import boundaries, process safety and provenance.

SoundScript's CLI and Playground continue to use the existing language and
rendering pipeline. The NuGet facade adds a programmatic .NET surface without
changing CLI commands or language semantics.

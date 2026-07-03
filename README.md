# SoundScript

A tiny, deterministic music DSL that turns simple text into MIDI.  
Built in C# for curiosity, creativity, and play.

```
melody {
    bpm 120
    C4 E4 G4 | C5
}
```

**Text → parsed → interpreted → MIDI.** No DAW, no plugins, no audio synthesis.

## Quick Start

```bash
dotnet build
dotnet run --project src/SoundScript.Cli -- run examples/melody.ss
```

Open `output.mid` in any MIDI player.

## Documentation

Full language reference, pipeline details, and usage for CLI + Web:

**[docs/SoundScript.md](docs/SoundScript.md)**

## Project Layout

```
/src
    SoundScript.Core/      # Shared models
    SoundScript.Parser/    # Tokenizer + Parser
    SoundScript.Midi/      # Interpreter + MIDI export
    SoundScript.Cli/       # CLI runner
    SoundScript.Web/       # Blazor WASM demo

/examples
    melody.ss
    durations.ss
    instruments.ss
    chords.ss
    sequences.ss
    loops.ss
    velocity.ss
    multitrack.ss
    full.ss
```

## License

See [LICENSE](LICENSE).

# Runtime parameter host sample

This basic .NET 10 console host uses a project reference to the repository's SoundScript facade. It compiles [`monitor.ss`](monitor.ss) once, captures its defaults, updates `intensity` and `xpos` as typed decimal application data, and captures a second snapshot. It writes complete WAV/MIDI files and a temporal scene JSON for each state.

From the repository root, with the pinned submodules initialized:

```powershell
dotnet run --project samples/RuntimeParameters/RuntimeParameters.csproj -c Release -- artifacts/runtime-parameters
```

The output folder contains `normal.wav`, `normal.mid`, `normal.json`, `warning.wav`, `warning.mid`, and `warning.json`, plus hashes. The host also prints the runtime compiler's tokenizer/parser/timeline counts and the two snapshot revisions. Compilation statistics remain 1/1/1 across the update because `SetMany` updates parameter state and `Bind` captures the resulting values without reparsing source.

The sample exercises repeated in-process changes to approved numeric values. It does not implement live synthesis or a continuous stream: each WAV is rendered as a complete offline result using the existing SoundScript renderers.

See [runtime parameters](../../docs/runtime-parameters.md) for supported bindings and limits, and the [public media runtime guide](../../docs/programmatic-media-runtime.md) for the shared audio and visual model.

# Runtime parameter host sample

This .NET 10 console host compiles [`monitor.ss`](monitor.ss) once, then maps application status (normal, warning, critical) to typed `intensity` and `xpos` values. Each snapshot produces WAV, MIDI, JSON and SVG through the existing renderers.

From the repository root, with the pinned submodules initialized:

```powershell
dotnet run --project samples/RuntimeParameters/RuntimeParameters.csproj -c Release -- artifacts/runtime-parameters
```

The output contains normal/warning/critical artifacts and hashes. Compiler counts remain 1/1/1 across the updates. Add `--benchmark` after the output directory to record seven-batch timing/allocation measurements in `benchmark.json`; timings are observations on the current machine, not latency guarantees.

The sample exercises repeated in-process changes to approved numeric values. It does not implement live synthesis or a continuous stream: each WAV is rendered as a complete offline result using the existing SoundScript renderers.

See [runtime parameters](../../docs/runtime-parameters.md) for supported bindings and limits, and the [public media runtime guide](../../docs/programmatic-media-runtime.md) for the shared audio and visual model.

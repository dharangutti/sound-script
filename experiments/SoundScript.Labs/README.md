# SoundScript.Labs - experimental simulator POC

**Research only. Separate language and opt-in solution. Not a production feature or roadmap commitment.**

This small POC tests whether a readable, version-controlled definition can flow through
parser -> AST -> versioned experiment IR -> simulator -> analysis -> structured files.
It does not replace SoundScript Audio. There is no hardware, capture, medical,
calibration, certification, NDT, machine control or adaptive workflow support.

See [FINAL_REPORT.md](FINAL_REPORT.md) for repository findings, decisions, verification
evidence and remaining limitations. The supplied private strategy PDF stays outside
the public documentation tree; its identity and relationship to this POC are recorded
in the report.

The completed v0.1 checkpoint is preserved at `labs-signal-poc-v0.1.0`.
See [VISION.md](VISION.md) for the future generic signal model, backend capability
principle and explicit decision to defer v0.2. That direction is research only;
it adds no implementation or production commitment.

## Run from the repository root

Use the repository's existing .NET 10 SDK selection in `global.json`.

```powershell
dotnet test experiments/SoundScript.Labs/SoundScript.Labs.slnx -c Debug
dotnet run --project experiments/SoundScript.Labs/SoundScript.Labs.Cli -- experiments/SoundScript.Labs/examples/tone.sslabs --out artifacts/labs-tone
```

The output directory must not already exist. This prevents accidental replacement
of previous results. For a full local repeatability check in fresh directories:

```powershell
pwsh -File experiments/SoundScript.Labs/verify.ps1
```

The script runs unit/integration tests and compares all four outputs from two
separate CLI processes. It keeps evidence under the ignored `artifacts` directory.

## Example

```text
experiment tone_check {
    generate sine {
        frequency 1000Hz
        duration 128ms
        sample_rate 32kHz
        amplitude 0.5
    }
    analyze { fft; peaks; rms; frequency }
    export json
    export wav
    export pcm
    export float32
}
```

Expected: 4096 mono samples, peak at 1000 Hz, RMS approximately 0.35355339.
The samples have amplitude 0.5; the result is calculated, not copied from an example.

## Deliberately small grammar

- One `signal name { ... }` or `experiment name { generate waveform { ... } analyze { ... } export format ... }` per file.
- Use `.sslabs` to distinguish Labs source from production `.ss`, `.ssw` and `.ssv`.
- `signal` has a `waveform` parameter and exports all four formats without analysis.
- `experiment` uses `generate sine|square|chirp|sweep`; `analyze` is optional and must precede exports.
- Sine/square require `frequency`; chirp/sweep require `start` and `end`. All require `duration`.
- `sample_rate` defaults to 48000 Hz; `amplitude` defaults to 1. Defaults appear explicitly in IR.
- Frequency units: `Hz`, `kHz`; time units: `s`, `ms`. Units are mandatory and case-sensitive.
- Decimal dot notation only, without exponents. Scalar amplitude has no unit.
- Parameters may be reordered. Braces, whitespace and optional semicolons delimit tokens.
- `#` and `//` comments start at a token boundary and continue to the end of the line.
- Names: ASCII letter/underscore followed by letters/digits/underscores, maximum 128 characters.
- Unknown, duplicate, incomplete and unsupported constructs fail; no implicit hardware syntax.

## Numerical contract (IR schema v1)

| Item | Definition |
| --- | --- |
| Sample rate | Integer, 1-384000 Hz |
| Sample budget | 1-1048576 mono frames per definition |
| Duration | Positive; duration times rate must be an exact whole sample count |
| Frequencies | Positive and strictly below sample-rate/2 |
| Amplitude | In (0, 1], dimensionless normalized sample amplitude |
| Time | Sample i occurs at i/rate; end time N/rate is excluded |
| Sine | Zero starting phase |
| Square | 50% duty, starts positive; ideal sampled square, not band-limited |
| Chirp/sweep | Identical linear-frequency law in v1, ascending or descending |
| Sweep phase | cycles(t) = start*t + 0.5*((end-start)/duration)*t*t |
| RMS | Entire Float32 buffer, double accumulation, before PCM quantization |
| FFT | All samples, rectangular window, next-power-of-two zero padding; minimum size 2 |
| Spectrum | Single-sided amplitude 2*abs(X)/N; DC/Nyquist use abs(X)/N |
| Peaks | Non-DC local maxima above max(1e-12, maxMagnitude*1e-6); top 8, amplitude descending then frequency ascending |
| Frequency | Strongest qualifying spectral peak, or null for silence/no peak; not a fundamental/resonance estimator |
| Source size | At most 65536 decoded characters |

The sample-rate ceiling and frequency validation above are **v0.1 simulator
constraints**, not permanent universal SoundScript language limits. See
[VISION.md](VISION.md#backend-capability-principle) for the proposed future boundary.

`peaks` and `frequency` compute FFT even without an explicit `fft` request. JSON
includes FFT size/resolution and requested peak metrics, not every spectrum bin.
The `AnalysisEngine.Spectrum` API exposes the full spectrum for testing/exploration.
Zero padding does not add measurement information. Noncoherent tones and sweeps
exhibit leakage; a sweep's strongest bin is not a measured resonance. Square
harmonics may alias despite the fundamental passing the Nyquist check.

## Output and reproducibility

| File | Format |
| --- | --- |
| `result.json` | Always emitted: resolved IR, source/IR/Float32 SHA-256, backend/engine versions, sample metadata, analysis, diagnostics and binary-output hashes |
| `signal.wav` | Mono signed PCM16 WAV, requested sample rate, existing SoundScript WAV writer |
| `signal.pcm` | Raw signed PCM16 little-endian, identical to WAV data payload |
| `signal.f32` | Raw IEEE-754 Float32 little-endian, pre-quantization samples |

PCM conversion follows the existing writer: clamp to [-1,1], scale by 32767,
round to nearest with ties to even. Read raw files with rate/channel information
from `result.json`. No timestamps, paths or random identifiers enter file contents.
Temporary directory names are random only for safe file staging.

The source hash covers the UTF-8 encoding of decoded source text, including comments
and line endings (a UTF-8 BOM is not part of the decoded text). Equivalent units can
produce identical IR/sample hashes while source hashes differ. Local `.gitattributes`
pins example sources to LF for reproducible checkouts.

Same source and engine/dependency versions are tested for repeatability on this
Windows x64 environment in Debug and Release. The existing deterministic sine/cosine
utility is reused. Golden hashes are included, but cross-OS/CPU agreement has not
been measured for Labs. Do not treat this POC as a universal bitwise guarantee.

## Code boundaries

`Syntax.cs` owns tokens/AST. `LabsCompiler.cs` resolves units/defaults and validates.
`ExperimentIr.cs` owns immutable versioned records; it has no parser or hardware types.
`Simulator.cs` implements `IExperimentBackend.Execute(ExperimentIr)`.
`Analysis.cs` consumes IR and immutable samples. `ResultExporter.cs` owns serialization.
`ExperimentRunner.cs` orchestrates the pipeline; the separate CLI handles filesystem I/O.

These are logical boundaries inside one small library, not a framework of projects.
The only production reference is Labs -> SoundScript.Wave (and its existing transitive
Core/Wordbank dependencies). Wave supplies `DeterministicMath` and `WavWriter`.
Production code never references Labs. Tests also use the existing parser for a
coexistence smoke test. The production solution, CLI, grammar, UI, publishing and CI
are untouched. Run the Labs solution explicitly; root solution tests do not discover it.

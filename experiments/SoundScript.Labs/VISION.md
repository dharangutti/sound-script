# SoundScript Labs: future signal architecture

## Purpose

Record the architectural direction suggested by the completed experimental
checkpoint `labs-signal-poc-v0.1.0`. This is a research direction, not a v0.2
implementation plan or a production roadmap commitment. Labs remains isolated
under `experiments/SoundScript.Labs/`.

## What v0.1 proved

Production SoundScript represents a program flowing into deterministic audio/media.
Labs v0.1 demonstrated a separate, frequency-defined sampled-signal workflow:

```text
Program -> AST -> validated experiment IR -> simulator -> analysis -> structured exports
```

It generates and analyzes synthetic sample buffers in software. The
[final report](FINAL_REPORT.md) records the implemented scope and historical
verification evidence; this document does not extend those claims. Software
generation of samples is not physical signal generation or measurement.

## Generic signal model

> SoundScript should model signals generically. Whether a signal is infrasonic,
> audible, or ultrasonic should be determined by its frequency and by whether
> the selected execution backend supports that signal.

The broader conceptual direction is:

```text
Program -> Acoustic / sampled signal definition -> Infrasonic | Audible | Ultrasonic
```

The core language should describe the waveform and its parameters generically.
Prefer the architectural idea below to an ultrasound-specific construct such as
`generate ultrasound`:

```text
generate sine {
    frequency 40kHz
    ...
}
```

This incomplete sketch illustrates the direction; it is not a runnable example,
a new syntax proposal to implement now, or a claim of hardware output support.

## Infrasonic, audible, and ultrasonic classifications

These are frequency classifications, not separate fundamental language types.
Whether a requested signal can execute depends on the selected backend.

| Frequency | Classification |
| --- | --- |
| 10 Hz | Infrasonic |
| 1 kHz | Audible |
| 40 kHz | Ultrasonic |
| 5 MHz | Ultrasonic |

The examples express the conceptual range, not a v0.1 capability list. In
particular, 5 MHz cannot be represented within the current simulator limits;
`MHz` is not a supported v0.1 source unit. A classification does not establish
physical output, transducer bandwidth or measurement capability.

## Backend capability principle

The long-term architecture may separate signal intent from execution capability:

```text
Signal definition
      |
      v
Execution backend
      +-- Simulator
      +-- Audio backend
      +-- Infrasonic-capable backend
      +-- Ultrasonic-capable backend
      +-- Industrial instrument / hardware backend
```

These are possible backend roles, not implemented integrations or mutually
exclusive language types. v0.1 provides one software simulator and an execution
interface; it does not provide capability negotiation. A future design could
validate requested signals against explicit backend capabilities:

```text
Signal definition -> Backend capability check
                          +-- supported   -> execute
                          +-- unsupported -> deterministic capability error
```

Frequency range, sample rate and supported waveform behavior could be relevant
capabilities. Their contract and validation remain future design work.

## Current v0.1 simulator limits

The current compiler and IR validation enforce, among other bounds:

```text
1 Hz <= sample_rate <= 384000 Hz (integer sample rate)
0 < signal frequency < sample_rate / 2
1 <= sample_count <= 1048576
```

These are **v0.1 simulator constraints**, not permanent universal SoundScript
language limits. The frequency check also applies to chirp/sweep endpoints. The
Nyquist relationship remains relevant to sampled representations; future
backends would need validation appropriate to their representation and hardware,
not a universal 384 kHz ceiling. No limits or validation are changed here.

Passing the current frequency check does not guarantee analog waveform fidelity:
the v0.1 ideal sampled square can alias harmonics. Exporting a WAV or sample file
does not demonstrate that an audio device can reproduce its frequency content.

## Possible future physical-signal pipeline

```text
Program
  -> Generate signal
  -> Physical interaction
  -> Capture / observe response
  -> Analyze
  -> Interpret
  -> Structured result
```

This is only a future research direction. v0.1 analyzes the synthetic stimulus
directly, not a captured response. It provides no physical interaction model,
hardware generation, capture, physical measurement, sensors, instrumentation,
NDT, resonance detection, defect detection or industrial interpretation.

## Explicit non-goals

This documentation adds no v0.2 functionality. It does not introduce waveforms,
higher sample-rate limits, ultrasound-specific syntax, backend capability
negotiation, hardware support, sensors, capture, SCPI, NDT workflows, assertions,
adaptive control, physical measurements or resonance interpretation.

Production grammar, parser, CLI, packages, NuGet behavior, Playground, website,
examples, CI and current SoundScript behavior remain untouched. The v0.1
implementation and its historical verification evidence remain preserved.

## Conditions that could justify v0.2

Do not continue implementation merely to produce another version. A future v0.2
should begin only for a concrete reason, such as:

- A collaborator with a real workflow.
- A specific industrial or research requirement.
- A requested signal capability.
- A hardware or instrument integration opportunity.
- An experiment that v0.1 cannot represent.
- A justified need to explore signal capture or interpretation.

Such a reason should define a bounded experiment and the evidence needed to
evaluate it. This document does not establish that any of these conditions has
been met or authorize the full physical-signal pipeline.

## Current decision

```text
Preserve v0.1
Document the vision
Keep Labs isolated
Defer v0.2
```

`labs-signal-poc-v0.1.0` is a completed experimental checkpoint. Stop implementation
at that checkpoint until a concrete need justifies separately scoped work. Record
this vision in a normal documentation commit on `codex/soundscript-labs-poc`;
leave the existing tag unchanged and create no new tag or release.

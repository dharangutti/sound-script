namespace SoundScript.Labs.IR;

public enum Waveform { Sine, Square, Chirp, Sweep }
[Flags] public enum AnalysisKind { None = 0, Fft = 1, Peaks = 2, Rms = 4, Frequency = 8 }
[Flags] public enum ExportKind { Json = 1, Wav = 2, Pcm = 4, Float32 = 8 }

// Versioned, immutable value contract; no parser, file, instrument or AST types.
public sealed record SignalIr(Waveform Waveform, double StartHz, double EndHz,
    int SampleRate, int SampleCount, double Amplitude);
public sealed record ExperimentIr(int SchemaVersion, string Name, SignalIr Signal,
    AnalysisKind Analysis, ExportKind Exports)
{
    public const int CurrentVersion = 1;
    public const int MaxSamples = 1_048_576;

    // Validate at backend entry too, so direct IR callers cannot bypass limits.
    public void Validate()
    {
        if (SchemaVersion != CurrentVersion) throw new ArgumentException("Unsupported IR version.");
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 128) throw new ArgumentException("Invalid IR name.");
        if (Signal is null) throw new ArgumentException("Signal is required.");
        if (!Enum.IsDefined(Signal.Waveform)) throw new ArgumentException("Unsupported waveform.");
        if (Signal.SampleRate is < 1 or > 384_000 || Signal.SampleCount is < 1 or > MaxSamples)
            throw new ArgumentException("Sample rate/count exceeds Labs limits.");
        if (!double.IsFinite(Signal.Amplitude) || Signal.Amplitude is <= 0 or > 1)
            throw new ArgumentException("Amplitude must be in (0, 1].");
        foreach (var frequency in new[] { Signal.StartHz, Signal.EndHz })
            if (!double.IsFinite(frequency) || frequency <= 0 || frequency >= Signal.SampleRate / 2.0)
                throw new ArgumentException("Frequencies must be positive and strictly below Nyquist.");
        if (Signal.Waveform is Waveform.Sine or Waveform.Square && Signal.StartHz != Signal.EndHz)
            throw new ArgumentException("Constant waveforms require equal start/end frequencies.");
        if ((Analysis & ~(AnalysisKind.Fft | AnalysisKind.Peaks | AnalysisKind.Rms | AnalysisKind.Frequency)) != 0)
            throw new ArgumentException("Unsupported analysis flags.");
        if (Exports == 0 || (Exports & ~(ExportKind.Json | ExportKind.Wav | ExportKind.Pcm | ExportKind.Float32)) != 0)
            throw new ArgumentException("Unsupported export flags.");
    }
}

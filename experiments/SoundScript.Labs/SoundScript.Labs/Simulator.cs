using System.Collections.Immutable;
using SoundScript.Labs.IR;
using SoundScript.Wave.Synthesis;

namespace SoundScript.Labs.Execution;

public sealed record SampleBuffer(int SampleRate, ImmutableArray<float> Samples)
{
    public void ValidateFor(ExperimentIr experiment)
    {
        experiment.Validate();
        if (SampleRate != experiment.Signal.SampleRate || Samples.IsDefault ||
            Samples.Length != experiment.Signal.SampleCount || Samples.Any(x => !float.IsFinite(x)))
            throw new ArgumentException("Backend samples do not match the IR or contain nonfinite values.");
    }
}

public interface IExperimentBackend
{
    string Id { get; }
    SampleBuffer Execute(ExperimentIr experiment);
}

public sealed class Simulator : IExperimentBackend
{
    public string Id => "simulator-linear-phase/v1";

    public SampleBuffer Execute(ExperimentIr experiment)
    {
        experiment.Validate();
        var signal = experiment.Signal;
        var samples = ImmutableArray.CreateBuilder<float>(signal.SampleCount);
        double duration = signal.SampleCount / (double)signal.SampleRate;
        double slope = (signal.EndHz - signal.StartHz) / duration;
        for (int i = 0; i < signal.SampleCount; i++)
        {
            double time = i / (double)signal.SampleRate;
            // Integrate linear instantaneous frequency; start phase is always zero.
            double cycles = signal.StartHz * time + .5 * slope * time * time;
            double phase = cycles - Math.Floor(cycles);
            double sample = signal.Waveform == Waveform.Square
                ? (phase < .5 ? 1 : -1)
                : DeterministicMath.Sin(2 * Math.PI * phase);
            samples.Add((float)(signal.Amplitude * sample));
        }
        return new(signal.SampleRate, samples.MoveToImmutable());
    }
}

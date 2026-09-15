namespace SoundScript.Transcription;

/// <summary>YIN CMND pitch estimation with parabolic lag interpolation; no random state.</summary>
public sealed class MonophonicAnalyzer : IMusicalAnalyzer
{
    public const int Hop = 160;
    public const int Window = 640;
    public static (double? Frequency, double Periodicity) DetectPitch(ReadOnlySpan<float> samples)
    {
        if (samples.Length < Window) return (null, 0);
        const int minLag = 16, maxLag = 246, integration = 320;
        Span<double> difference = stackalloc double[maxLag + 2];
        double running = 0;
        difference[0] = 1;
        for (int lag = 1; lag <= maxLag + 1; lag++)
        {
            double sum = 0;
            for (int j = 0; j < integration; j++) { double delta = samples[j] - samples[j + lag]; sum += delta * delta; }
            running += sum;
            difference[lag] = running < 1e-15 ? 1 : sum * lag / running;
        }
        int selected = -1;
        for (int lag = minLag; lag <= maxLag; lag++)
        {
            if (difference[lag] >= .15) continue;
            while (lag < maxLag && difference[lag+1] < difference[lag]) lag++;
            selected = lag; break;
        }
        if (selected < 0) return (null, 0);
        double left = difference[selected-1], center = difference[selected], right = difference[selected+1];
        double denominator = 2 * (left - 2*center + right);
        double adjustment = Math.Abs(denominator) < 1e-15 ? 0 : Math.Clamp((left-right)/denominator,-.5,.5);
        return (AnalysisAudio.SampleRate / (selected + adjustment), Math.Clamp(1-center,0,1));
    }
    public static int FrequencyToMidi(double frequency)
    {
        if (!double.IsFinite(frequency) || frequency <= 0) throw new ArgumentOutOfRangeException(nameof(frequency));
        return (int)Math.Round(69 + 12*Math.Log2(frequency/440));
    }
    public MusicalObservations Analyze(AnalysisAudio audio, CancellationToken cancellationToken = default)
        => Complete(audio, AnalyzeFrames(audio, cancellationToken).ToList());

    /// <summary>Same calculations, yielding every 20 frames so WASM can repaint and cancel.</summary>
    public async Task<MusicalObservations> AnalyzeAsync(AnalysisAudio audio, CancellationToken cancellationToken = default)
    {
        var frames = new List<PitchFrame>();
        foreach (var frame in AnalyzeFrames(audio, cancellationToken))
        {
            frames.Add(frame);
            if (frames.Count % 20 == 0) await Task.Delay(1, cancellationToken);
        }
        return Complete(audio, frames);
    }

    private static IEnumerable<PitchFrame> AnalyzeFrames(AnalysisAudio audio, CancellationToken cancellationToken)
    {
        var samples = audio.Samples;
        var rms = new double[(samples.Length + Hop-1)/Hop];
        for (int i = 0; i < rms.Length; i++)
        {
            double energy=0; int end=Math.Min(samples.Length,(i+1)*Hop);
            for (int j=i*Hop;j<end;j++) energy+=samples[j]*samples[j];
            rms[i]=Math.Sqrt(energy/Math.Max(1,end-i*Hop));
        }
        double threshold=Math.Max(.003, rms.DefaultIfEmpty(0).Max()*.035);
        var window=new float[Window];
        for (int i=0;i<rms.Length;i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double? frequency=null; double periodicity=0;
            if (rms[i]>=threshold)
            {
                Array.Clear(window);
                int start=i*Hop-Window/2+Hop/2;
                for (int j=0;j<Window;j++) if (start+j>=0 && start+j<samples.Length) window[j]=samples[start+j];
                (frequency,periodicity)=DetectPitch(window);
            }
            yield return new(i*.01,frequency,periodicity,rms[i]);
        }
    }

    private static MusicalObservations Complete(AnalysisAudio audio, List<PitchFrame> frames)
    {
        var samples = audio.Samples;
        var diagnostics = new List<AnalysisDiagnostic>();
        if (samples.Length < AnalysisAudio.SampleRate / 10) diagnostics.Add(new("short-input","Less than 100 ms; note and tempo evidence is insufficient."));
        if (samples.Count(x => Math.Abs(x) >= .999f) > samples.Length * .001) diagnostics.Add(new("clipping","More than 0.1% of samples reach full scale."));
        double threshold = Math.Max(.003, frames.Select(f => f.Rms).DefaultIfEmpty(0).Max() * .035);
        int active=frames.Count(f => f.Rms>=threshold), voiced=frames.Count(f => f.Frequency.HasValue);
        if (active==0) diagnostics.Add(new("silence","No audio above the analysis silence threshold."));
        else if (voiced < active*.8) diagnostics.Add(new("weak-periodicity","Substantial nonperiodic audio: noise, weak fundamental or simultaneous sources may prevent monophonic analysis."));
        diagnostics.Add(new("monophonic-assumption","One pitch per frame; simultaneous sources and instrument identity are not inferred."));
        return new(frames,samples.Length/(double)AnalysisAudio.SampleRate,diagnostics);
    }
}

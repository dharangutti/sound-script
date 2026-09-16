using SoundScript.Transcription;

namespace SoundScript.Tests;

// Original synthetic performances, CC0. Timbres differ from the analyzer's
// conservative harmonic ceilings; ground truth is authored independently.
internal sealed record PolyphonicFixture(string Name, MusicalNote[] Truth, double Duration, string Timbre = "piano", int Tempo = 120)
{
    private static MusicalNote N(int pitch, double start, double duration) => new(pitch, start, duration, start * 2, duration * 2, 80, new(1, "Authored synthetic ground truth"));
    private static MusicalNote[] Chord(int[] pitches, double start = .2, double duration = .7) => pitches.Select(p => N(p, start, duration)).ToArray();
    public static readonly PolyphonicFixture[] All = [
        new("dyad", Chord([60, 67]), 1.2),
        new("major-triad", Chord([60, 64, 67]), 1.2),
        new("minor-triad", Chord([57, 60, 64]), 1.2),
        new("sustained", Chord([55, 60, 64], .2, 1.6), 2),
        new("repeated", [..Chord([60, 64, 67], .2, .35), ..Chord([60, 64, 67], .7, .35), ..Chord([60, 64, 67], 1.2, .35)], 1.8),
        new("repeated-legato", [..Chord([60,64,67],.2,.5),..Chord([60,64,67],.7,.5),..Chord([60,64,67],1.2,.5)], 2),
        new("arpeggio", [N(60,.2,.3),N(64,.6,.3),N(67,1,.3),N(72,1.4,.3)], 1.9),
        new("octaves", Chord([60,72]), 1.2),
        new("melody-block", [..Chord([48,55,60],.2,1.5),N(76,.2,.4),N(79,.7,.4),N(74,1.2,.4)], 2),
        new("melody-broken", [N(48,.2,.45),N(55,.7,.45),N(60,1.2,.45),N(76,.2,.8),N(74,1.1,.55)], 2),
        new("overlap", [N(60,.2,1),N(64,.5,1),N(67,.8,1)], 2),
        new("monophonic", [N(60,.2,.4),N(64,.8,.4),N(67,1.4,.4)], 2),
        new("harmonics", [N(60,.2,1)], 1.5),
        new("dense", Chord([48,50,52,54,56,58,60,62,64],.2,1), 1.5, "sine"),
        new("dense-ensemble", Chord([48,50,52,54,56,58,60,62,64],.2,1), 1.5, "ensemble"),
        new("missing-fundamental", [N(48,.2,1)], 1.5, "missing"),
        new("noise", [], 1.5, "noise"),
        new("drums", [], 1.5, "drums"),
        new("silence", [], 1.5, "silence")
    ];

    public AnalysisAudio Audio()
    {
        var samples = new float[(int)(Duration * AnalysisAudio.SampleRate)];
        foreach (var n in Truth)
        {
            int begin = (int)Math.Round(n.StartSeconds * AnalysisAudio.SampleRate), count = (int)Math.Round(n.DurationSeconds * AnalysisAudio.SampleRate);
            double frequency = 440 * Math.Pow(2, (n.MidiPitch - 69) / 12.0);
            for (int j = 0; j < count; j++)
            {
                double t = j / (double)AnalysisAudio.SampleRate, phase = 2 * Math.PI * frequency * t;
                double tone = Timbre == "missing" ? (Math.Sin(2*phase)+Math.Sin(3*phase)+Math.Sin(4*phase)+Math.Sin(5*phase)) / 2
                    : Timbre == "sine" ? Math.Sin(phase)
                    : Math.Sin(phase) + (.30 + (n.MidiPitch % 3) * .025) * Math.Sin(2*phase) + .16 * Math.Sin(3*phase) + .07 * Math.Sin(4*phase);
                double envelope = Math.Min(1, t / .008) * Math.Min(1, (count-j) / (AnalysisAudio.SampleRate * .015)) * Math.Exp(-t * 1.2);
                samples[begin+j] += (float)(tone * envelope);
            }
        }
        var random = new Random(2202);
        if (Timbre is "noise" or "drums") for (int i=0;i<samples.Length;i++)
            samples[i]=(float)((random.NextDouble()*2-1) * (Timbre=="drums" ? Math.Exp(-(i/16000.0%.25)*30) : 1));
        if (Timbre == "ensemble") for (int i=0;i<samples.Length;i++) samples[i]+=(float)((random.NextDouble()*2-1)*1.5);
        double peak = samples.Select(s => Math.Abs(s)).DefaultIfEmpty().Max();
        if (peak > 0) for (int i=0;i<samples.Length;i++) samples[i]=(float)(samples[i]/peak*.8);
        return new(samples);
    }
}

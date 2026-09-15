using SoundScript.Transcription;

namespace SoundScript.Tests;

// Original generated musical phrases, dedicated to CC0. No third-party recording or score.
internal sealed record TranscriptionFixture(string Name, int Tempo, string Timbre, MusicalNote[] Truth, double Duration)
{
    internal static IReadOnlyList<TranscriptionFixture> All { get; } =
    [
        Create("clean-sine",120,"sine",[60,64,67,62,65,69],[0,.5,1,1.5,2,2.5],[.4,.4,.4,.4,.4,.4]),
        Create("vocal-style",100,"vocal",[57,60,62,64,62],[0,.6,1.2,1.8,2.4],[.5,.5,.5,.5,.5]),
        Create("sustained-bowed",96,"bowed",[67,69,72,71],[0,.625,1.875,2.5],[.58,1.2,.58,1.2]),
        Create("keyboard-repeated",120,"piano",[60,60,64,64,67,60],[0,.5,1,1.5,2,2.5],[.32,.32,.32,.32,.32,.32]),
        Create("rests-rubato",120,"sine",[62,65,69,67,64],[.25,.75,1.7,2.45,3.1],[.3,.65,.45,.35,.6])
    ];
    private static TranscriptionFixture Create(string name,int tempo,string timbre,int[] pitches,double[] starts,double[] durations) =>
        new(name,tempo,timbre,pitches.Select((p,i)=>new MusicalNote(p,starts[i],durations[i],starts[i]*tempo/60,durations[i]*tempo/60,80,new(1,"Original fixture ground truth"))).ToArray(),starts[^1]+durations[^1]+.2);
    internal AnalysisAudio Audio()
    {
        var samples=new float[(int)(Duration*AnalysisAudio.SampleRate)];
        foreach(var note in Truth)
        {
            int begin=(int)Math.Round(note.StartSeconds*AnalysisAudio.SampleRate), count=(int)Math.Round(note.DurationSeconds*AnalysisAudio.SampleRate);
            double phase=0, frequency=440*Math.Pow(2,(note.MidiPitch-69)/12.0);
            for(int i=0;i<count;i++)
            {
                double t=i/(double)AnalysisAudio.SampleRate;
                double vibrato=Timbre is "vocal" or "bowed" ? .006*Math.Sin(2*Math.PI*5*t):0;
                phase+=2*Math.PI*frequency*(1+vibrato)/AnalysisAudio.SampleRate;
                double tone=Math.Sin(phase);
                if(Timbre=="vocal") tone+=.45*Math.Sin(phase*2)+.2*Math.Sin(phase*3);
                if(Timbre=="bowed") tone+=.3*Math.Sin(phase*2)+.15*Math.Sin(phase*4);
                if(Timbre=="piano") tone=(tone+.35*Math.Sin(phase*2)+.12*Math.Sin(phase*3))*Math.Exp(-t*4);
                double envelope=Math.Min(1,t/(Timbre=="bowed"?.025:.005))*Math.Min(1,(count-i)/(AnalysisAudio.SampleRate*.01));
                samples[begin+i]=(float)(.4*tone*envelope);
            }
        }
        return new(samples);
    }
}

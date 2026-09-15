using System.Text.Json;
using SoundScript.Core;
using SoundScript.Media;
using SoundScript.Transcription;

namespace SoundScript.Cli;

public static partial class CommandHandlers
{
    private static int Transcribe(CliArguments args)
    {
        var outputs=new[]{args.Value("out"),args.Value("report"),args.Value("preview")}.OfType<string>().ToArray();
        for(int i=0;i<outputs.Length;i++)
        {
            AtomicOutput.ValidatePath(outputs[i]);
            if(PathEquals(outputs[i],args.Input) || outputs.Take(i).Any(p=>PathEquals(p,outputs[i])))
                throw new CliUsageException("Transcription output, report, preview and input must have distinct paths.");
        }
        var token=CliRuntime.CancellationToken;
        var audio=new DesktopMediaInput(args.Value("ffmpeg")).DecodeAsync(args.Input,token).GetAwaiter().GetResult();
        int? tempo=args.Value("tempo") is null or "auto"?null:int.Parse(args.Value("tempo")!,System.Globalization.CultureInfo.InvariantCulture);
        var result=new MonophonicTranscriber().Transcribe(audio,new(tempo,InstrumentMap.Resolve(args.Value("instrument")??"flute")),token);
        if(result.Score.Tracks.All(t=>t.Notes.Count==0)) throw new InvalidDataException("No stable musical notes found. "+string.Join(" ",result.Diagnostics.Select(d=>d.Message)));
        var validation=MusicalComparison.Validate(result.Score,token);
        var reconstructed=new MonophonicTranscriber().Transcribe(PcmWaveInput.Decode(validation.PreviewWave),new(tempo??(int)result.Score.TempoMap[0].Bpm,Quantize:false),token);
        var sourceComparison=MusicalComparison.Compare(result.Score.Tracks[0].Notes,reconstructed.Score.Tracks[0].Notes);
        token.ThrowIfCancellationRequested();
        AtomicOutput.Write(args.Value("out")!,p=>File.WriteAllText(p,validation.Source),p=>SoundScriptOutput.Parse(File.ReadAllText(p)));
        if(args.Value("preview") is { } preview) AtomicOutput.Write(preview,p=>File.WriteAllBytes(p,validation.PreviewWave),p=>PcmWaveInput.Decode(File.ReadAllBytes(p)));
        if(args.Value("report") is { } report)
        {
            string json=JsonSerializer.Serialize(new { Transcription=result,RoundTrip=validation.ScoreToRenderedAudio,SourceObservationToRendered=sourceComparison,
                ComparisonMeaning="Source comparison uses detected observations as a proxy; it is not independent source ground truth." },AnalysisJsonOutput.Options);
            AtomicOutput.Write(report,p=>File.WriteAllText(p,json),p=>{using var document=JsonDocument.Parse(File.ReadAllText(p));});
        }
        Console.WriteLine($"Transcribed {result.Score.Tracks[0].Notes.Count} notes; tempo hypothesis {result.Score.TempoMap[0].Bpm:0} BPM; pitch periodicity {result.PitchConfidence:P1}; timing grid fit {result.TimingGridFit:P1}.");
        Console.WriteLine($"Render comparison: pitch recall {validation.ScoreToRenderedAudio.PitchAccuracy:P1}, missed {validation.ScoreToRenderedAudio.MissedNotes}, extra {validation.ScoreToRenderedAudio.ExtraNotes}.");
        foreach(var diagnostic in result.Diagnostics) Console.WriteLine($"[{diagnostic.Code}] {diagnostic.Message}");
        Console.WriteLine($"SoundScript: {Path.GetFullPath(args.Value("out")!)}");
        return 0;
    }
}

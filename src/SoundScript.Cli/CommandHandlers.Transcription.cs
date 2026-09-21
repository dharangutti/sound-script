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
        var completionPath = TranscriptionCompletion.ManifestPath(args.Value("out")!);
        for(int i=0;i<outputs.Length;i++)
        {
            AtomicOutput.ValidatePath(outputs[i]);
            if(PathEquals(outputs[i],args.Input) || PathEquals(outputs[i],completionPath) || outputs.Take(i).Any(p=>PathEquals(p,outputs[i])))
                throw new CliUsageException("Transcription output, report, preview and input must have distinct paths.");
        }
        if (PathEquals(completionPath, args.Input)) throw new CliUsageException("Completion manifest and input must have distinct paths.");
        var token=CliRuntime.CancellationToken;
        token.ThrowIfCancellationRequested();
        TranscriptionCompletion.Invalidate(completionPath);
        var input = new DesktopMediaInput(args.Value("ffmpeg"));
        var media = input.InspectAsync(args.Input,token).GetAwaiter().GetResult();
        var excerpt = new MediaExcerpt(args.Value("start") is { } start ? double.Parse(start,System.Globalization.CultureInfo.InvariantCulture) : 0,
            args.Value("duration") is { } duration ? double.Parse(duration,System.Globalization.CultureInfo.InvariantCulture) : null);
        Console.WriteLine($"Media duration {media.DurationSeconds:F3} seconds; maximum analysis 120 seconds. Input: {media.SampleRate} Hz, {media.Channels} channels; analysis: mono 16000 Hz.");
        var audio=input.DecodeAsync(args.Input,excerpt,token).GetAwaiter().GetResult();
        int? tempo=args.Value("tempo") is null or "auto"?null:int.Parse(args.Value("tempo")!,System.Globalization.CultureInfo.InvariantCulture);
        var mode = args.Value("mode") switch { "percussion" => TranscriptionMode.Percussion, "mixed" => TranscriptionMode.Mixed, "polyphonic" => TranscriptionMode.Polyphonic, "extract-melody" => TranscriptionMode.ExtractMelody, _ => TranscriptionMode.Monophonic };
        var result=new TranscriptionEngine().Transcribe(audio,new(tempo,InstrumentMap.Resolve(args.Value("instrument")??(mode is TranscriptionMode.Polyphonic or TranscriptionMode.Mixed ? "piano" : "flute"))) { Roles = args.Value("roles")?.Split(',') },mode,token);
        if (result.Mixed is { } mixed)
            foreach (var role in mixed.Roles) Console.WriteLine($"Role {role.Role}: {role.DetectedNotes} observed notes; selected={role.Selected}; rank agreement {role.MeanRankAgreement:P1}. {role.Method}");
        if (result.Polyphony is { } poly)
            Console.WriteLine($"Polyphonic / Piano (Experimental): {poly.DetectedNotes} notes; maximum simultaneous notes {poly.MaximumSimultaneousNotes}; {poly.ChordCount} simultaneous pitch groups; mean active polyphony {poly.MeanActivePolyphony:F2}; polyphonic coverage {poly.PolyphonicCoverage:P1}; residual fundamental share {poly.MeanFundamentalShare:P1}; ambiguous frames {poly.AmbiguousFrameFraction:P1}; octave ambiguity {poly.OctaveAmbiguousFraction:P1}.");
        if (result.Extraction is { } extraction)
            Console.WriteLine($"Extract Melody (Experimental): {extraction.ExtractedNoteCount} notes; melody coverage {extraction.MelodyCoverage:P1}; mean spectral share {extraction.MeanSpectralShare:P1}; competing pitches {extraction.CompetingPitchFraction:P1}; octave uncertainty {extraction.OctaveUncertainFraction:P1}; {extraction.RejectedSections.Count} rejected sections (see report).");
        var suitability = result.Suitability;
        if (result.Percussion == null) Console.WriteLine($"{suitability.Status}: {suitability.DetectedNotes} detected notes, {suitability.UsableNotes} usable candidates; stable active coverage {suitability.StableActiveFraction:P1}; voiced active coverage {suitability.VoicedActiveFraction:P1}. {suitability.Reason}");
        if (!suitability.CanGenerate)
        {
            if (args.Value("report") is { } failedReport)
                AtomicOutput.Write(failedReport,p=>File.WriteAllText(p,JsonSerializer.Serialize(new { Media=media, Excerpt=excerpt, Transcription=result, Generated=false },AnalysisJsonOutput.Options)),p=>{using var document=JsonDocument.Parse(File.ReadAllText(p));});
            throw new InvalidDataException(suitability.Reason + " No SoundScript or preview written; existing output files, if any, belong to an earlier run.");
        }
        var generationScore=TranscriptionSuitability.ScoreForGeneration(result);
        if (mode == TranscriptionMode.Percussion)
        {
            var rhythm = PercussionComparison.Validate(generationScore, token);
            token.ThrowIfCancellationRequested();
            AtomicOutput.Write(args.Value("out")!, p => File.WriteAllText(p, rhythm.Source), p => SoundScriptOutput.Parse(File.ReadAllText(p)));
            if (args.Value("preview") is { } rhythmPreview)
                AtomicOutput.Write(rhythmPreview, p => File.WriteAllBytes(p, rhythm.PreviewWave), p => PcmWaveInput.Decode(File.ReadAllBytes(p)));
            if (args.Value("report") is { } rhythmReport)
                AtomicOutput.Write(rhythmReport, p => File.WriteAllText(p, JsonSerializer.Serialize(new {
                    Media = media, Excerpt = excerpt, Generated = true, Mode = "percussion", Transcription = result,
                    RoundTrip = rhythm.ScoreToRenderedAudio,
                    ComparisonMeaning = "Generated hit schedule versus percussion reanalysis of synthetic playback, not source ground truth."
                }, AnalysisJsonOutput.Options)), p => { using var document = JsonDocument.Parse(File.ReadAllText(p)); });
            Console.WriteLine($"Experimental percussion: {result.Percussion!.Hits.Count} hits; tempo hypothesis {generationScore.TempoMap[0].Bpm} BPM; grid fit {result.TimingGridFit:P1}.");
            Console.WriteLine($"Render comparison: onset precision {rhythm.ScoreToRenderedAudio.OnsetPrecision:P1}, recall {rhythm.ScoreToRenderedAudio.OnsetRecall:P1}, class agreement {rhythm.ScoreToRenderedAudio.MatchedClassAgreement:P1}.");
            foreach (var diagnostic in result.Diagnostics) Console.WriteLine($"[{diagnostic.Code}] {diagnostic.Message}");
            Console.WriteLine($"SoundScript: {Path.GetFullPath(args.Value("out")!)}");
            TranscriptionCompletion.Write(completionPath, "percussion", outputs, token);
            return 0;
        }
        if (mode is TranscriptionMode.Polyphonic or TranscriptionMode.Mixed)
        {
            var polyphonicValidation = PolyphonicComparison.Validate(generationScore, token);
            token.ThrowIfCancellationRequested();
            AtomicOutput.Write(args.Value("out")!, p => File.WriteAllText(p, polyphonicValidation.Source), p => SoundScriptOutput.Parse(File.ReadAllText(p)));
            if (args.Value("preview") is { } polyphonicPreview)
                AtomicOutput.Write(polyphonicPreview, p => File.WriteAllBytes(p, polyphonicValidation.PreviewWave), p => PcmWaveInput.Decode(File.ReadAllBytes(p)));
            if (args.Value("report") is { } polyphonicReport)
                AtomicOutput.Write(polyphonicReport, p => File.WriteAllText(p, JsonSerializer.Serialize(new {
                    Media = media, Excerpt = excerpt, Generated = true, Mode = mode == TranscriptionMode.Mixed ? "mixed" : "polyphonic", Transcription = result,
                    RoundTrip = polyphonicValidation.ScoreToRenderedAudio,
                    ComparisonMeaning = "Round trip compares the generated note schedule to polyphonic reanalysis of its render; it is not source ground truth."
                }, AnalysisJsonOutput.Options)), p => { using var document = JsonDocument.Parse(File.ReadAllText(p)); });
            var metrics = polyphonicValidation.ScoreToRenderedAudio;
            Console.WriteLine($"Experimental: generated {generationScore.Tracks.Sum(t => t.Notes.Count)} notes in {generationScore.Tracks.Count} parallel voices. Render comparison: note precision {metrics.NotePrecision:P1}, recall {metrics.NoteRecall:P1}, missed {metrics.MissedNotes}, extra {metrics.ExtraNotes}.");
            foreach (var diagnostic in result.Diagnostics) Console.WriteLine($"[{diagnostic.Code}] {diagnostic.Message}");
            Console.WriteLine($"SoundScript: {Path.GetFullPath(args.Value("out")!)}");
            TranscriptionCompletion.Write(completionPath, mode == TranscriptionMode.Mixed ? "mixed" : "polyphonic", outputs, token);
            return 0;
        }
        var validation=MusicalComparison.Validate(generationScore,token);
        var reconstructed=new MonophonicTranscriber().Transcribe(PcmWaveInput.Decode(validation.PreviewWave),new(tempo??(int)result.Score.TempoMap[0].Bpm,Quantize:false),token);
        var sourceComparison=MusicalComparison.Compare(result.Score.Tracks[0].Notes,reconstructed.Score.Tracks[0].Notes);
        token.ThrowIfCancellationRequested();
        AtomicOutput.Write(args.Value("out")!,p=>File.WriteAllText(p,validation.Source),p=>SoundScriptOutput.Parse(File.ReadAllText(p)));
        if(args.Value("preview") is { } preview) AtomicOutput.Write(preview,p=>File.WriteAllBytes(p,validation.PreviewWave),p=>PcmWaveInput.Decode(File.ReadAllBytes(p)));
        if(args.Value("report") is { } report)
        {
            string json=JsonSerializer.Serialize(new { Media=media,Excerpt=excerpt,Generated=true,Transcription=result,RoundTrip=validation.ScoreToRenderedAudio,SourceObservationToRendered=sourceComparison,
                ComparisonMeaning="Source comparison uses detected observations as a proxy; it is not independent source ground truth." },AnalysisJsonOutput.Options);
            AtomicOutput.Write(report,p=>File.WriteAllText(p,json),p=>{using var document=JsonDocument.Parse(File.ReadAllText(p));});
        }
        Console.WriteLine($"{suitability.Status}: generated {generationScore.Tracks[0].Notes.Count} notes; tempo hypothesis {result.Score.TempoMap[0].Bpm:0} BPM; {(result.Extraction == null ? "detected-note periodicity" : "selected spectral share")} {result.PitchConfidence:P1}; timing grid fit {result.TimingGridFit:P1}.");
        Console.WriteLine($"Render comparison: pitch recall {validation.ScoreToRenderedAudio.PitchAccuracy:P1}, missed {validation.ScoreToRenderedAudio.MissedNotes}, extra {validation.ScoreToRenderedAudio.ExtraNotes}.");
        foreach(var diagnostic in result.Diagnostics) Console.WriteLine($"[{diagnostic.Code}] {diagnostic.Message}");
        Console.WriteLine($"SoundScript: {Path.GetFullPath(args.Value("out")!)}");
        TranscriptionCompletion.Write(completionPath, mode == TranscriptionMode.ExtractMelody ? "extract-melody" : "monophonic", outputs, token);
        return 0;
    }
}

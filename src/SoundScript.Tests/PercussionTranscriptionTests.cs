using System.Text.Json;
using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Parser;
using SoundScript.Transcription;
using SoundScript.Wave.Adapter;
using Xunit;

namespace SoundScript.Tests;

public sealed class PercussionTranscriptionTests
{
    // Authored DSP fixtures independent of the production percussion renderer.
    public static AnalysisAudio Fixture(string kind)
    {
        var samples = new float[48000]; uint seed = 95;
        double filtered = 0, previous = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double t = i / 16000.0;
            seed = unchecked(seed * 1103515245 + 12345);
            double noise = (seed / (double)uint.MaxValue * 2 - 1);
            filtered = filtered * .72 + noise * .28;
            if (kind == "noise") samples[i] = (float)(noise * .3);
            else if (kind == "sustain") samples[i] = (float)(.3 * Math.Sin(2 * Math.PI * 440 * t));
            else if (kind != "silence" && t >= .25 && t < 2.6)
            {
                int index = (int)((t - .25) / .5);
                double local = (t - .25) % .5;
                string hit = kind == "pattern" ? (index % 2 == 0 ? "kick" : "snare") : kind == "mixed" ? new[] { "kick", "snare", "hat" }[index % 3] : kind;
                double value = hit switch
                {
                    "kick" => .55 * Math.Sin(2 * Math.PI * (65 * local + .7 * (1 - Math.Exp(-local * 50)))) * Math.Exp(-local * 25),
                    "snare" => filtered * 1.2 * Math.Exp(-local * 28),
                    "hat" => (noise - previous) * .35 * Math.Exp(-local * 70),
                    "pitched" => .4 * Math.Sin(2 * Math.PI * 880 * local) * Math.Exp(-local * 30),
                    _ => noise * .4 * Math.Exp(-local * 60)
                };
                samples[i] = (float)value;
            }
            previous = noise;
        }
        return new(samples);
    }

    [Theory] [InlineData("kick", PercussionSound.Kick)] [InlineData("snare", PercussionSound.Snare)] [InlineData("hat", PercussionSound.Hat)]
    public void IndependentHitsHaveKnownOnsetsAndCoarseClasses(string kind, PercussionSound expected)
    {
        var result = new TranscriptionEngine().Transcribe(Fixture(kind), new(120), TranscriptionMode.Percussion);
        Assert.True(result.Suitability.CanGenerate);
        Assert.Equal(5, result.Percussion!.Hits.Count);
        Assert.All(result.Percussion.Hits, h => Assert.Equal(expected, h.Sound));
        for (int i = 0; i < 5; i++) Assert.InRange(Math.Abs(result.Percussion.Hits[i].StartSeconds - (.25 + i * .5)), 0, .02);
        Assert.All(result.Score.Tracks, t => Assert.Empty(t.Notes));
        Assert.Empty(result.Observations.Frames);
        var validation = PercussionComparison.Validate(result.Score);
        Assert.InRange(validation.ScoreToRenderedAudio.OnsetRecall, .8, 1);
        Assert.Contains("hit " + kind, validation.Source);
        var ast = SoundScriptOutput.Parse(validation.Source);
        Assert.All(ast.Statements.OfType<TrackNode>().SelectMany(t => t.Body), n => Assert.False(n is NoteNode));
        Assert.All(AstToNoteEventAdapter.Convert(ast).Values.SelectMany(t => t), e => { Assert.Equal(0, e.FrequencyHz); Assert.NotNull(e.Percussion); });
    }

    [Theory] [InlineData("pattern")] [InlineData("mixed")] [InlineData("pitched")] [InlineData("transient")]
    public void PatternsAndAmbiguousResonancesNeverBecomeNotes(string kind)
    {
        var result = new TranscriptionEngine().Transcribe(Fixture(kind), mode: TranscriptionMode.Percussion);
        Assert.Equal(5, result.Percussion!.Hits.Count);
        Assert.All(result.Score.Tracks, t => Assert.Empty(t.Notes));
        Assert.Equal(0, result.PitchConfidence);
        Assert.True(result.Percussion.TempoAlternatives.Count > 1);
        Assert.InRange(result.TimingGridFit, .9, 1);
        Assert.NotEmpty(TranscriptionPlayback.Render(new SoundScriptOutput().Source(result.Score)));
    }

    [Theory] [InlineData("silence")] [InlineData("noise")] [InlineData("sustain")]
    public void UnsupportedAudioRejectsWithoutInventingRhythm(string kind)
    {
        var result = new TranscriptionEngine().Transcribe(Fixture(kind), mode: TranscriptionMode.Percussion);
        Assert.False(result.Suitability.CanGenerate);
        Assert.Empty(result.Percussion!.Hits);
        Assert.Throws<InvalidDataException>(() => TranscriptionSuitability.ScoreForGeneration(result));
    }

    [Fact] public async Task SyncAsyncDeterminismAndCancellation()
    {
        var engine = new TranscriptionEngine(); var audio = Fixture("mixed");
        var sync = engine.Transcribe(audio, mode: TranscriptionMode.Percussion);
        var asyncResult = await engine.TranscribeAsync(audio, mode: TranscriptionMode.Percussion);
        Assert.Equal(JsonSerializer.Serialize(sync), JsonSerializer.Serialize(asyncResult));
        string source = new SoundScriptOutput().Source(sync.Score);
        Assert.Equal(TranscriptionPlayback.Render(source), TranscriptionPlayback.Render(source));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.TranscribeAsync(audio, mode: TranscriptionMode.Percussion, cancellationToken: cts.Token));
    }

    [Fact] public void HitSyntaxRoundTripsAndMidiFailsExplicitly()
    {
        var ast = SoundScriptOutput.Parse("tempo 120 track drums { rest :0.25 hit kick :0.5 v80 hit snare :0.25 hit hat :0.25 hit click :0.25 }");
        Assert.Equal(SsPrinter.Print(ast), SsPrinter.Print(SoundScriptOutput.Parse(SsPrinter.Print(ast))));
        Assert.ThrowsAny<Exception>(() => SoundScriptOutput.Parse("hit piano :1"));
        Assert.ThrowsAny<Exception>(() => SoundScriptOutput.Parse("hit kick :0"));
        Assert.Throws<NotSupportedException>(() => SoundScript.Midi.Interpreter.Interpret(ast));
    }

    [Fact] public void NestedHitsPreserveIndependentTrackTimingAndKeywordNames()
    {
        var ast = SoundScriptOutput.Parse("tempo 120 sequence hit { hit hat :0.25 } track drums { loop 2 { hit kick :0.5 } play hit phrase { hit snare :0.25 } } track other { rest :0.5 hit click :0.25 }");
        var tracks = AstToNoteEventAdapter.Convert(ast);
        Assert.Equal(new[] { 0d, .25, .5, .625 }, tracks["drums"].Select(n => n.StartTimeSeconds));
        Assert.Equal(.25, Assert.Single(tracks["other"]).StartTimeSeconds);
        Assert.ThrowsAny<Exception>(() => SoundScriptOutput.Parse("voice singer { hit kick :1 }"));
    }

    [Fact] public void ExplicitTempoAndUnsnapPreserveMeasuredOnsets()
    {
        var result = new PercussionTranscriber().Transcribe(Fixture("pattern"), new(93, Quantize: false));
        Assert.Equal(93, result.Score.TempoMap[0].Bpm);
        Assert.All(result.Percussion!.Hits, h => Assert.InRange(Math.Abs(h.StartBeat * 60 / 93 - h.StartSeconds), 0, .000001));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PercussionTranscriber().Transcribe(Fixture("kick"), new(0)));
        Assert.Throws<ArgumentException>(() => new PercussionTranscriber().Transcribe(Fixture("kick"), new() { Roles = ["bass"] }));
    }

    [Fact] public void CliRejectsRoleSelectionForPercussion()
    {
        var args = SoundScript.Cli.CliArguments.Parse(["transcribe", "drums.wav", "--mode", "percussion", "--out", "drums.ss"]);
        Assert.Equal("percussion", args.Value("mode"));
        Assert.Throws<SoundScript.Cli.CliUsageException>(() => SoundScript.Cli.CliArguments.Parse(["transcribe", "drums.wav", "--mode", "percussion", "--roles", "bass", "--out", "drums.ss"]));
    }

    [Fact] public void TruncatedAttackRejectsAndSingleHitDoesNotInventTempo()
    {
        var shortAudio = new AnalysisAudio(Fixture("kick").Samples.Skip(4000).Take(800).ToArray());
        Assert.False(new PercussionTranscriber().Transcribe(shortAudio).Suitability.CanGenerate);
        var single = new PercussionTranscriber().Transcribe(new(Fixture("kick").Samples.Take(10000).ToArray()));
        Assert.Single(single.Percussion!.Hits);
        Assert.Equal(0, single.TimingGridFit);
        Assert.Single(single.Percussion.TempoAlternatives);
        Assert.Contains(single.Diagnostics, d => d.Message.Contains("playback fallback"));
    }

    [Fact] public void EventComparisonDoesNotMatchOneObservedHitTwice()
    {
        var hit = new PercussionHit(PercussionSound.Kick, .25, .5, .125, 80, new(1, "authored"), 1, 1, 0, 0);
        var metrics = PercussionComparison.Compare([hit, hit with { StartSeconds = .27 }], [hit]);
        Assert.Equal(.5, metrics.OnsetRecall);
        Assert.Equal(1, metrics.MissedHits);
        Assert.Equal(1, metrics.OnsetPrecision);
    }

    [Fact] public void WeakPitchedAccompanimentDoesNotCreatePitchedScoreEvents()
    {
        var samples = Fixture("pattern").Samples.Select((x, i) => (float)(x + .02 * Math.Sin(2 * Math.PI * 330 * i / 16000))).ToArray();
        var result = new PercussionTranscriber().Transcribe(new(samples));
        Assert.InRange(result.Percussion!.Hits.Count, 4, 5);
        Assert.All(result.Score.Tracks, t => Assert.Empty(t.Notes));
        Assert.Contains(result.Diagnostics, d => d.Code == "coarse-classification");
    }

    [Fact] public void InspectUsesWaveAndMeasuresActualHitRenderLength()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ss");
        try
        {
            File.WriteAllText(path, "tempo 120 track drums { hit kick :0.5 hit hat :0.25 }");
            var analysis = SoundScript.Cli.SourceAnalysis.Load(path);
            Assert.Equal("wave", analysis.Metadata.AudioBackend);
            Assert.Equal(0, analysis.Metadata.NoteCount);
            Assert.Equal(2, analysis.Metadata.EventCount);
            using var stream = new MemoryStream(TranscriptionPlayback.Render(File.ReadAllText(path)));
            double duration = SoundScript.Wave.Io.WavReader.ReadMono(stream).Length / 44100.0;
            Assert.Equal(duration, analysis.Metadata.AudioDurationSeconds!.Value, 8);
            Assert.DoesNotContain("mid", analysis.Metadata.SupportedOutputTypes);
        }
        finally { File.Delete(path); }
    }
}

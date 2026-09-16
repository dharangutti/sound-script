using System.Text.Json;
using SoundScript.Cli;
using SoundScript.Transcription;
using SoundScript.Wave.Io;
using Xunit;

namespace SoundScript.Tests;

public sealed class MelodyExtractionTests
{
    private static readonly int[] Melody = [72, 76, 79, 74];
    // Original CC0 harmonic tones with piano-like decay, plus independent accompaniment.
    public static AnalysisAudio Mixture(string kind)
    {
        var samples = new float[AnalysisAudio.SampleRate * 2];
        var random = new Random(731);
        for (int i = 0; i < samples.Length; i++)
        {
            double t = i / (double)AnalysisAudio.SampleRate, local = t % .5;
            double Tone(int pitch) => Math.Sin(2 * Math.PI * 440 * Math.Pow(2, (pitch - 69) / 12.0) * t);
            double lead = Tone(Melody[(int)(t * 2)]);
            double envelope = Math.Min(1, local / .01) * Math.Exp(-local * 2);
            double noise = random.NextDouble() * 2 - 1;
            samples[i] = (float)(kind switch {
                "dominant" => (.4 * lead + .1 * Tone(Melody[(int)(t * 2)] + 12) + .08 * Tone(48) + .07 * Tone(55)) * envelope,
                "equal" => (.3 * lead + .3 * Tone(61)) * envelope,
                "accompaniment" => (.06 * lead + .23 * Tone(48) + .23 * Tone(55) + .23 * Tone(64)) * envelope,
                "ensemble" => (.14 * lead + .14 * Tone(48) + .14 * Tone(55) + .14 * Tone(64) + .15 * noise) * envelope,
                "octaves" => (.3 * lead + .3 * Tone(Melody[(int)(t * 2)] - 12)) * envelope,
                "drums" => .5 * noise * Math.Exp(-local * 40),
                "noise" => .4 * noise,
                _ => 0
            });
        }
        return new(samples);
    }

    [Fact] public async Task DominantMelodyHasKnownPitchesValidSourceAudiblePreviewAndIdenticalAsyncOutput()
    {
        var engine = new TranscriptionEngine(); var audio = Mixture("dominant");
        var result = engine.Transcribe(audio, new(120), TranscriptionMode.ExtractMelody);
        Assert.Equal("Experimental", result.Suitability.Status);
        Assert.Equal(Melody, result.Score.Tracks[0].Notes.Select(n => n.MidiPitch));
        Assert.InRange(result.Extraction!.MelodyCoverage, .8, 1);
        Assert.All(result.Score.Tracks[0].Notes, n => Assert.Contains("spectral", n.PitchEvidence.Method));
        var validation = MusicalComparison.Validate(TranscriptionSuitability.ScoreForGeneration(result));
        _ = SoundScriptOutput.Parse(validation.Source);
        Assert.Contains(PcmWaveInput.Decode(validation.PreviewWave).Samples, s => Math.Abs(s) > .01);
        string Json(TranscriptionResult r) => JsonSerializer.Serialize(r, AnalysisJsonOutput.Options);
        Assert.Equal(Json(result), Json(engine.Transcribe(audio, new(120), TranscriptionMode.ExtractMelody)));
        Assert.Equal(Json(result), Json(await engine.TranscribeAsync(audio, new(120), TranscriptionMode.ExtractMelody)));
    }

    [Theory]
    [InlineData("equal")] [InlineData("accompaniment")] [InlineData("ensemble")]
    [InlineData("drums")] [InlineData("noise")] [InlineData("silence")] [InlineData("octaves")]
    public void UnsupportedMixturesDoNotFabricateAMelody(string kind)
    {
        var result = new TranscriptionEngine().Transcribe(Mixture(kind), mode: TranscriptionMode.ExtractMelody);
        Assert.Equal("Rejected", result.Suitability.Status);
        Assert.Throws<InvalidDataException>(() => TranscriptionSuitability.ScoreForGeneration(result));
        if (kind != "silence") Assert.NotEmpty(result.Extraction!.RejectedSections);
        if (kind == "octaves") Assert.True(result.Extraction!.OctaveUncertainFraction > .8);
    }

    [Fact] public async Task DefaultAndExplicitMonophonicModesPreserveEveryBaseline()
    {
        var engine = new TranscriptionEngine();
        foreach (var fixture in TranscriptionFixture.All)
        {
            var audio = fixture.Audio(); var baseline = new MonophonicTranscriber().Transcribe(audio);
            string Json(TranscriptionResult r) => JsonSerializer.Serialize(r, AnalysisJsonOutput.Options);
            Assert.Equal(Json(baseline), Json(engine.Transcribe(audio)));
            Assert.Equal(Json(baseline), Json(await engine.TranscribeAsync(audio)));
            Assert.Null(engine.Transcribe(audio).Extraction);
        }
    }

    [Fact] public async Task CancellationAndUnknownModesAreExplicit()
    {
        var engine = new TranscriptionEngine(); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => engine.Transcribe(Mixture("dominant"), mode: TranscriptionMode.ExtractMelody, cancellationToken: cancel.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.TranscribeAsync(Mixture("dominant"), mode: TranscriptionMode.ExtractMelody, cancellationToken: cancel.Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Transcribe(new([]), mode: (TranscriptionMode)99));
        Assert.Throws<CliUsageException>(() => CliArguments.Parse(["transcribe", "input.wav", "--out", "x.ss", "--mode", "polyphonic"]));
    }

    [Theory] [InlineData("dominant", true)] [InlineData("equal", false)]
    public void CliUsesSameCoreAndWritesReportsForSuccessAndRejection(string kind, bool accepted)
    {
        string root = Path.Combine(Path.GetTempPath(), "soundscript-melody-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string input = Path.Combine(root, "input.wav"), output = Path.Combine(root, "output.ss"), report = Path.Combine(root, "report.json");
            WavWriter.Write(input, Mixture(kind).Samples, 16000);
            var args = CliArguments.Parse(["transcribe", input, "--mode", "extract-melody", "--out", output, "--report", report, "--tempo", "120"]);
            if (accepted) Assert.Equal(0, CommandHandlers.Execute(args));
            else Assert.Throws<InvalidDataException>(() => CommandHandlers.Execute(args));
            using var json = JsonDocument.Parse(File.ReadAllText(report));
            Assert.Equal(accepted, json.RootElement.GetProperty("Generated").GetBoolean());
            var core = new TranscriptionEngine().Transcribe(PcmWaveInput.Decode(File.ReadAllBytes(input)), new(120), TranscriptionMode.ExtractMelody);
            Assert.Equal(core.Extraction!.ExtractedNoteCount, json.RootElement.GetProperty("Transcription").GetProperty("Extraction").GetProperty("ExtractedNoteCount").GetInt32());
            if (accepted) Assert.Equal(new SoundScriptOutput().Source(TranscriptionSuitability.ScoreForGeneration(core)), File.ReadAllText(output));
            else Assert.False(File.Exists(output));
        }
        finally { Directory.Delete(root, true); }
    }
}

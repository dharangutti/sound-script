using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using SoundScript.Labs.Analysis;
using SoundScript.Labs.Compilation;
using SoundScript.Labs.Execution;
using SoundScript.Labs.IR;
using SoundScript.Labs.Output;
using SoundScript.Labs.Syntax;
using SoundScript.Wave.Io;
using Xunit;

namespace SoundScript.Labs.Tests;

public sealed class LabsTests
{
    private const string Tone = "experiment tone { generate sine { frequency 1kHz duration 128ms sample_rate 32kHz amplitude 0.5 } analyze { fft peaks rms frequency } export json export wav export pcm export float32 }";
    private static ExperimentIr Compile(string source) => LabsCompiler.Compile(LabsParser.Parse(source));

    [Fact]
    public void CoherentToneHasKnownFrequencyAmplitudeAndRms()
    {
        var run = ExperimentRunner.Run(Tone);
        Assert.Equal(4096, run.Buffer.Samples.Length);
        Assert.Equal(1000, run.Analysis.PeakFrequencyHz);
        Assert.InRange(run.Analysis.Rms!.Value, .35355338, .35355340);
        Assert.InRange(run.Analysis.Peaks[0].Amplitude, .4999999, .5000001);
        Assert.Equal(7.8125, run.Analysis.Fft!.BinWidthHz);
        Assert.Equal(0, run.Buffer.Samples[0]);
        Assert.Equal(.5f, run.Buffer.Samples[8]);
    }

    [Theory]
    [InlineData("tone.sslabs")]
    [InlineData("sweep.sslabs")]
    [InlineData("square.sslabs")]
    [InlineData("chirp.sslabs")]
    public void ShippedExamplesRepeatByteForByte(string name)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "examples", name));
        var first = ResultExporter.Export(ExperimentRunner.Run(source));
        var second = ResultExporter.Export(ExperimentRunner.Run(source));
        Assert.Equal(first.Keys, second.Keys);
        foreach (var key in first.Keys) Assert.Equal(first[key], second[key]);
    }

    [Fact]
    public void UnitsAndFormattingResolveToTheSameIr()
    {
        var other = Tone.Replace("1kHz", "1000Hz").Replace("128ms", "0.128s").Replace("32kHz", "32000Hz");
        Assert.Equal(Compile(Tone), Compile(other));
        Assert.Equal(Compile(Tone), Compile("// a comment\n" + Tone.Replace("duration", "; duration")));
        Assert.NotEqual(ExperimentRunner.Run(Tone).SourceSha256, ExperimentRunner.Run(other).SourceSha256);
    }

    [Fact]
    public void CultureCannotChangeIrOrOutput()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            var expected = ResultExporter.Export(ExperimentRunner.Run(Tone));
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var actual = ResultExporter.Export(ExperimentRunner.Run(Tone));
            foreach (var key in expected.Keys) Assert.Equal(expected[key], actual[key]);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Theory]
    [InlineData("frequency 1kHz", "frequency 16kHz")]
    [InlineData("frequency 1kHz", "frequency 20kHz")]
    [InlineData("frequency 1kHz", "frequency 0Hz")]
    [InlineData("frequency 1kHz", "frequency -1Hz")]
    [InlineData("frequency 1kHz", "frequency 1000")]
    [InlineData("frequency 1kHz", "frequency 1ms")]
    [InlineData("frequency 1kHz", "frequency NaNHz")]
    [InlineData("duration 128ms", "duration 0s")]
    [InlineData("duration 128ms", "duration 1000s")]
    [InlineData("duration 128ms", "duration 0.00001s")]
    [InlineData("sample_rate 32kHz", "sample_rate 32.1Hz")]
    [InlineData("sample_rate 32kHz", "sample_rate 385kHz")]
    [InlineData("amplitude 0.5", "amplitude 1.1")]
    [InlineData("amplitude 0.5", "amplitude 0")]
    [InlineData("amplitude 0.5", "amplitude 0,5")]
    [InlineData("amplitude 0.5", "amplitude 0.5 amplitude 0.4")]
    [InlineData("amplitude 0.5", "unknown 0.5")]
    [InlineData("generate sine", "generate ultrasound")]
    [InlineData("fft peaks", "fft resonance")]
    [InlineData("fft peaks", "fft fft")]
    [InlineData("export json", "export scpi")]
    [InlineData("export json", "export json export json")]
    [InlineData("frequency 1kHz", "frequency 99999999999999999999999999999kHz")]
    [InlineData("frequency 1kHz", "")]
    public void InvalidOrUnsupportedSourceIsRejected(string from, string to)
        => Assert.Throws<LabsException>(() => Compile(Tone.Replace(from, to)));

    [Theory]
    [InlineData("")]
    [InlineData("signal x {")]
    [InlineData("signal x { waveform sine frequency }")]
    [InlineData("experiment x { generate sine { frequency 1Hz duration 1s } capture sensor {} }")]
    public void MalformedSourceFailsWithoutHanging(string source)
        => Assert.Throws<LabsException>(() => Compile(source));

    [Fact]
    public void ExtraDefinitionsAndOversizedSourceAreRejected()
    {
        Assert.Throws<LabsException>(() => Compile(Tone + Tone));
        Assert.Throws<LabsException>(() => Compile(new string(' ', LabsParser.MaxSourceLength + 1)));
    }

    [Fact]
    public void SignalDefaultsAreResolvedAndAnalysisIsOptIn()
    {
        var run = ExperimentRunner.Run("signal minimal { waveform sine frequency 1000Hz duration 5s }");
        Assert.Equal(48000, run.Experiment.Signal.SampleRate);
        Assert.Equal(240000, run.Experiment.Signal.SampleCount);
        Assert.Null(run.Analysis.Rms);
        Assert.Null(run.Analysis.Fft);
        Assert.Equal(4, ResultExporter.Export(run).Count);
    }

    [Fact]
    public void DirectIrHasTheSameGuardrailsAsSource()
    {
        var ir = Compile(Tone);
        var invalid = new[]
        {
            ir with { SchemaVersion = 99 },
            ir with { Signal = ir.Signal with { StartHz = double.NaN } },
            ir with { Signal = ir.Signal with { SampleCount = int.MaxValue } },
            ir with { Signal = ir.Signal with { SampleRate = 0 } },
            ir with { Signal = ir.Signal with { EndHz = 2000 } },
            ir with { Signal = ir.Signal with { Amplitude = double.PositiveInfinity } },
            ir with { Signal = ir.Signal with { Waveform = (Waveform)99 } },
            ir with { Analysis = (AnalysisKind)128 },
            ir with { Exports = 0 }
        };
        foreach (var value in invalid) Assert.Throws<ArgumentException>(() => new Simulator().Execute(value));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(13)]
    public void FftMatchesIndependentDftIncludingPaddingAndEndpoints(int count)
    {
        var input = Enumerable.Range(0, count).Select(i => (float)((i * 7 % 11) / 10.0 - .5)).ToImmutableArray();
        var actual = AnalysisEngine.Spectrum(input);
        int size = (actual.Length - 1) * 2;
        for (int k = 0; k <= size / 2; k++)
        {
            double re = 0, im = 0;
            for (int i = 0; i < input.Length; i++)
            {
                re += input[i] * Math.Cos(-2 * Math.PI * i * k / size);
                im += input[i] * Math.Sin(-2 * Math.PI * i * k / size);
            }
            double expected = Math.Sqrt(re * re + im * im) / count * (k == 0 || k == size / 2 ? 1 : 2);
            Assert.InRange(Math.Abs(actual[k] - expected), 0, 1e-12);
        }
    }

    [Fact]
    public void SilenceHasNoFrequencyOrPeaks()
    {
        var ir = Compile(Tone);
        var result = AnalysisEngine.Analyze(ir, new(ir.Signal.SampleRate, new float[ir.Signal.SampleCount].ToImmutableArray()));
        Assert.Equal(0, result.Rms);
        Assert.Null(result.PeakFrequencyHz);
        Assert.Empty(result.Peaks);
    }

    [Fact]
    public void PeaksAreSortedAndRmsUsesEveryInputSample()
    {
        var ir = Compile(Tone);
        var samples = Enumerable.Range(0, 4096).Select(i => (float)(.6 * Math.Sin(2 * Math.PI * 1000 * i / 32000)
            + .2 * Math.Sin(2 * Math.PI * 3000 * i / 32000))).ToImmutableArray();
        var result = AnalysisEngine.Analyze(ir, new(32000, samples));
        Assert.Equal(1000, result.Peaks[0].FrequencyHz);
        Assert.Equal(3000, result.Peaks[1].FrequencyHz);
        Assert.InRange(result.Rms!.Value, Math.Sqrt(.2) - 1e-7, Math.Sqrt(.2) + 1e-7);
        Assert.InRange(result.Peaks[0].Amplitude, .5999999, .6000001);
    }

    [Fact]
    public void SquareUsesFixedDutyCycleAndRms()
    {
        var run = ExperimentRunner.Run(Tone.Replace("generate sine", "generate square"));
        Assert.All(run.Buffer.Samples.Take(16), sample => Assert.Equal(.5f, sample));
        Assert.All(run.Buffer.Samples.Skip(16).Take(16), sample => Assert.Equal(-.5f, sample));
        Assert.Equal(.5, run.Analysis.Rms);
    }

    [Theory]
    [InlineData(100, 2000)]
    [InlineData(2000, 100)]
    public void LinearSweepMatchesIntegratedFrequencyAndChirpAlias(int start, int end)
    {
        string source = FormattableString.Invariant($"signal swept {{ waveform sweep start {start}Hz end {end}Hz duration 100ms sample_rate 48kHz }}");
        var sweep = ExperimentRunner.Run(source);
        var chirp = ExperimentRunner.Run(source.Replace("waveform sweep", "waveform chirp"));
        Assert.Equal(sweep.Buffer.Samples.ToArray(), chirp.Buffer.Samples.ToArray());
        foreach (int i in new[] { 0, 1, 700, 2400, 4799 })
        {
            double t = i / 48000.0;
            double expected = Math.Sin(2 * Math.PI * (start * t + .5 * (end - start) / .1 * t * t));
            Assert.InRange(Math.Abs(sweep.Buffer.Samples[i] - expected), 0, 1e-6);
        }
    }

    [Fact]
    public void ExportsHaveCorrectFormatsAndVerifiedProvenance()
    {
        var run = ExperimentRunner.Run(Tone);
        var files = ResultExporter.Export(run);
        var wav = files["signal.wav"];
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal(wav.Length - 8, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(4)));
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(22)));
        Assert.Equal(32000, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(24)));
        Assert.Equal(16, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(34)));
        Assert.Equal(4096 * 2, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(40)));
        Assert.Equal(wav[44..], files["signal.pcm"]);
        for (int i = 0; i < run.Buffer.Samples.Length; i++)
            Assert.Equal(run.Buffer.Samples[i], BinaryPrimitives.ReadSingleLittleEndian(files["signal.f32"].AsSpan(i * 4)));
        using var json = JsonDocument.Parse(files["result.json"]);
        Assert.Equal(run.SourceSha256, json.RootElement.GetProperty("source_sha256").GetString());
        Assert.Equal(ResultExporter.Hash(ResultExporter.SerializeIr(run.Experiment)), json.RootElement.GetProperty("ir_sha256").GetString());
        foreach (var item in json.RootElement.GetProperty("outputs").EnumerateArray())
            Assert.Equal(ResultExporter.Hash(files[item.GetProperty("file").GetString()!]), item.GetProperty("sha256").GetString());
    }

    [Fact]
    public void ToneRetainsReviewedGoldenWaveAndFloatHashes()
    {
        var files = ResultExporter.Export(ExperimentRunner.Run(Tone));
        Assert.Equal("308d2a08aae61bde5e66602638fb33883a5c1a045258ba85eac16d8643d1d556", ResultExporter.Hash(files["signal.f32"]));
        Assert.Equal("66811c063b1a2d36c8ae656a8f49f178292a07371303c751c34ed1203a0fbcff", ResultExporter.Hash(files["signal.wav"]));
    }

    [Fact]
    public void SingleSampleAnalysisPadsAndExportOnlyWavStillHasMetadata()
    {
        var run = ExperimentRunner.Run("experiment one { generate sine { frequency 1Hz duration 125ms sample_rate 8Hz } analyze { fft rms frequency } export wav }");
        Assert.Single(run.Buffer.Samples);
        Assert.Equal(2, run.Analysis.Fft!.FftSize);
        Assert.Null(run.Analysis.PeakFrequencyHz);
        Assert.Equal(new[] { "result.json", "signal.wav" }, ResultExporter.Export(run).Keys);
    }

    [Fact]
    public void SerializedIrCanExecuteWithoutSyntax()
    {
        var ir = Compile(Tone);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
        };
        var restored = JsonSerializer.Deserialize<ExperimentIr>(ResultExporter.SerializeIr(ir), options)!;
        Assert.Equal(ir, restored);
        Assert.Equal(new Simulator().Execute(ir).Samples.ToArray(), new Simulator().Execute(restored).Samples.ToArray());
    }

    [Fact]
    public void LabsWavWorksWithExistingReaderAndWriter()
    {
        var run = ExperimentRunner.Run(Tone.Replace("32kHz", "44.1kHz").Replace("128ms", "100ms"));
        var bytes = ResultExporter.Export(run)["signal.wav"];
        using var expected = new MemoryStream();
        WavWriter.WriteTo(expected, run.Buffer.Samples.ToArray(), 44100);
        Assert.Equal(expected.ToArray(), bytes);
        using var stream = new MemoryStream(bytes);
        var decoded = WavReader.ReadMono(stream);
        Assert.Equal(run.Buffer.Samples.Length, decoded.Length);
        for (int i = 0; i < decoded.Length; i++)
            Assert.InRange(Math.Abs(decoded[i] - run.Buffer.Samples[i]), 0, 1.0 / short.MaxValue);
    }

    private sealed class IrOnlyBackend : IExperimentBackend
    {
        public string Id => "test-ir-only";
        public ExperimentIr? Received { get; private set; }
        public SampleBuffer Execute(ExperimentIr experiment)
        {
            Received = experiment;
            return new(experiment.Signal.SampleRate, new float[experiment.Signal.SampleCount].ToImmutableArray());
        }
    }

    [Fact]
    public void BackendSubstitutionRequiresOnlyIr()
    {
        var backend = new IrOnlyBackend();
        var run = ExperimentRunner.Run(Tone, backend);
        Assert.Equal(Compile(Tone), backend.Received);
        Assert.Equal(backend.Id, run.Backend);
        Assert.Equal(0, run.Analysis.Rms);
    }

    [Fact]
    public void InvalidBackendBuffersAreRejected()
    {
        var ir = Compile(Tone);
        Assert.Throws<ArgumentException>(() => AnalysisEngine.Analyze(ir, new(48000, [0])));
        var samples = new float[ir.Signal.SampleCount];
        samples[0] = float.NaN;
        Assert.Throws<ArgumentException>(() => AnalysisEngine.Analyze(ir, new(32000, samples.ToImmutableArray())));
    }

    [Fact]
    public void ExistingAudioParserAndRendererStillAcceptTheirOwnLanguage()
    {
        var ast = new SoundScript.Parser.Parser(new SoundScript.Parser.Tokenizer("melody { tempo 120 C4 q E4 q G4 q | C5 h }").Tokenize()).Parse();
        var first = SoundScript.Wave.WaveRenderer.RenderSha256(ast);
        _ = ExperimentRunner.Run(Tone);
        Assert.Equal(first, SoundScript.Wave.WaveRenderer.RenderSha256(ast));
    }

    [Fact]
    public void CliWritesOutputsRejectsBadSourceAndNeverOverwrites()
    {
        string root = Path.Combine(Path.GetTempPath(), "labs-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "tone.sslabs"), target = Path.Combine(root, "run");
            File.WriteAllText(source, Tone);
            using var output = new StringWriter();
            using var error = new StringWriter();
            Assert.Equal(0, LabsCommand.Run([source, "--out", target], output, error));
            Assert.Equal(4, Directory.GetFiles(target).Length);
            var before = File.ReadAllBytes(Path.Combine(target, "result.json"));
            Assert.Equal(1, LabsCommand.Run([source, "--out", target], output, error));
            Assert.Equal(before, File.ReadAllBytes(Path.Combine(target, "result.json")));
            File.WriteAllText(source, "signal invalid {");
            Assert.Equal(1, LabsCommand.Run([source, "--out", Path.Combine(root, "invalid")], output, error));
            Assert.False(Directory.Exists(Path.Combine(root, "invalid")));
            Assert.Empty(Directory.GetDirectories(root, ".labs-*"));
            Assert.Equal(2, LabsCommand.Run([], output, error));
            Assert.Equal(0, LabsCommand.Run(["--help"], output, error));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}

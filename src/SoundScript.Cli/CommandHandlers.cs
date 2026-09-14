using System.Text.Json;
using Melanchall.DryWetMidi.Core;
using SoundScript.Compose;
using SoundScript.Core;
using SoundScript.Media;
using SoundScript.Midi;
using SoundScript.Parser;
using SoundScript.Prosody;
using SoundScript.Timbre;
using SoundScript.Visual;
using SoundScript.Vocal;
using SoundScript.Vocal.Wordbank;
using SoundScript.Voice;
using SoundScript.Wave;
using SoundScript.Wave.Adapter;
using SoundScript.Wave.Io;
using SoundScript.Wave.Tts;
using SoundScript.Wordbank;

namespace SoundScript.Cli;

public static partial class CommandHandlers
{
    public static int Execute(CliArguments args)
    {
        foreach (var option in new[] { "append", "css" })
            if (args.Value(option) is { } path && !File.Exists(path)) throw new FileNotFoundException($"Input file not found: {path}", path);
        foreach (var option in new[] { "out", "emit-ss" })
            if (args.Value(option) is { } path)
            {
                AtomicOutput.ValidatePath(path);
                if (File.Exists(args.Input) && PathEquals(path, args.Input)) throw new CliUsageException("Output must not overwrite the input source.");
                foreach (var sourceOption in new[] { "append", "css" })
                    if (args.Value(sourceOption) is { } source && PathEquals(path, source)) throw new CliUsageException($"Output must not overwrite --{sourceOption}.");
            }
        if (args.Value("emit-ss") is { } emitted && args.Value("out") is { } output && PathEquals(emitted, output))
            throw new CliUsageException("--out and --emit-ss must name different files.");
        return args.Command switch
        {
            "validate" or "inspect" => Analyze(args), "run" => Run(args),
            "compose" or "prosody" => Compose(args), "wave" => Wave(args),
            "render" => Render(args), "visual" => Visual(args), "video" => Video(args),
            "vocal generate" => VocalGenerate(args), "vocal batch" => VocalBatch(args),
            "wordbank ensure" => WordbankEnsure(args), "wordbank normalize" => WordbankNormalize(args),
            _ => throw new CliUsageException($"Unknown command {args.Command}.")
        };
    }

    private static bool PathEquals(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static int Analyze(CliArguments args)
    {
        using var progress = BeginProgress(args);
        progress.Stage(args.Command == "validate" ? "Compiling" : "Inspecting");
        var analysis = SourceAnalysis.Load(args.Input, args.Value("target"));
        progress.CompleteStage();
        object? results = null;
        if (args.Has("at"))
        {
            if (analysis.Metadata.VisualDurationSeconds is null) throw new CliUsageException("--at requires a visual/media program.");
            results = new { state = analysis.Timeline!.StateAt(CliArguments.ParseTime(args.Value("at")!)) };
        }
        if (args.Value("target") == "video") results = VideoPreflight.Check(args, analysis);
        progress.Completed(args.Input);
        if (args.Has("json")) Diagnostics.WriteResult(args.Command, args.Input, analysis.Diagnostics, analysis.Metadata, results);
        else
        {
            PrintDiagnostics(analysis);
            if (args.Command == "validate") Console.WriteLine($"Valid: {args.Input} ({analysis.Diagnostics.Count} warning(s)).");
            else PrintMetadata(analysis.Metadata);
            if (results is not null) Console.WriteLine(JsonSerializer.Serialize(results, Diagnostics.Json));
        }
        return 0;
    }

    private static void PrintMetadata(ProgramMetadata m)
    {
        Console.WriteLine($"Kind: {m.Kind}");
        if (m.Tempo is { } tempo) Console.WriteLine(FormattableString.Invariant($"Tempo: {tempo:0.###} BPM"));
        if (m.DurationSeconds is { } duration) Console.WriteLine(FormattableString.Invariant($"Duration: {duration:0.######} s"));
        Console.WriteLine($"Tracks: {m.TrackCount}; notes: {m.NoteCount}; events: {m.EventCount}; visuals: {m.VisualCount}");
        if (m.AudioDurationSeconds is { } audio) Console.WriteLine(FormattableString.Invariant($"Audio duration: {audio:0.######} s ({m.DurationBasis})"));
        if (m.VisualDurationSeconds is { } visual) Console.WriteLine(FormattableString.Invariant($"Visual duration: {visual:0.######} s; audio sync points: {m.AudioSyncSeconds.Length}"));
        if (m.StyleRuleCount is { } rules) Console.WriteLine($"Stylesheet rules: {rules}");
        Console.WriteLine("Supported outputs: " + (m.SupportedOutputTypes.Count == 0 ? "none (stylesheet input)" : string.Join(", ", m.SupportedOutputTypes)));
    }

    private static void PrintDiagnostics(SourceAnalysis analysis)
    {
        foreach (var diagnostic in analysis.Diagnostics) Console.Error.WriteLine(diagnostic);
    }

    private static int Run(CliArguments args)
    {
        using var progress = BeginProgress(args);
        progress.Stage("Compiling");
        var analysis = SourceAnalysis.Load(args.Input, "midi");
        progress.CompleteStage();
        var output = args.Value("out") ?? "output.mid";
        progress.Stage("Writing MIDI");
        WriteMedia(output, temporary => MidiGenerator.Write(analysis.Midi!, temporary));
        progress.Completed(output);
        PrintDiagnostics(analysis);
        Console.WriteLine($"Wrote {analysis.Metadata.NoteCount} notes across {analysis.Metadata.TrackCount} track(s) to {output} at {analysis.Metadata.Tempo} BPM.");
        return 0;
    }

    private static int Compose(CliArguments args)
    {
        using var progress = BeginProgress(args);
        progress.Stage("Composing");
        ConfigureWordbank(args);
        if (string.IsNullOrWhiteSpace(args.Input)) throw new CliUsageException("Nothing to compose: the text is empty.");
        var wave = args.Has("wave");
        var output = args.Value("out") ?? (wave ? "output.wav" : "output.mid");
        var ast = args.Command == "compose" ? PhonemeComposer.BuildAst(args.Input) : ProsodyComposer.BuildAst(args.Input);
        var interpreted = Interpreter.Interpret(ast);
        progress.CompleteStage();
        if (args.Value("append") is { } append)
        {
            var loaded = SourceAnalysis.Load(append, "midi"); interpreted = loaded.Midi!; PrintDiagnostics(loaded);
            if (args.Command == "compose") PhonemeComposer.AppendTo(interpreted, args.Input);
            else ProsodyComposer.AppendTo(interpreted, args.Input);
        }
        progress.Stage(wave ? "Rendering audio" : "Writing MIDI");
        if (wave) WriteMedia(output, temporary => { if (args.Has("stereo")) WaveRenderer.RenderStereo(ast, temporary); else WaveRenderer.Render(ast, temporary); });
        else WriteMedia(output, temporary => MidiGenerator.Write(interpreted, temporary));
        progress.Completed(output);
        if (args.Value("emit-ss") is { } emit)
            AtomicOutput.Write(emit, temporary => File.WriteAllText(temporary, SsPrinter.Print(ast)), temporary => ProgramLoader.Load(temporary));
        foreach (var warning in interpreted.Warnings) Console.Error.WriteLine(Diagnostics.Warning(warning, null));
        Console.WriteLine($"Composed {(args.Command == "prosody" ? "word-level prosody" : args.Command)} to {output}" + (wave ? " via SoundScript.Wave (no MIDI step)." : "."));
        return 0;
    }

    private static int Render(CliArguments args)
    {
        using var progress = BeginProgress(args);
        progress.Stage("Preparing MIDI and SoundCSS");
        if (!File.Exists(args.Input)) throw new FileNotFoundException($"MIDI not found: {args.Input}", args.Input);
        _ = MidiFile.Read(args.Input); _ = SourceAnalysis.ValidateCss(args.Value("css")!);
        progress.CompleteStage();
        var output = args.Value("out") ?? "output.wav";
        if (Path.GetExtension(output).ToLowerInvariant() is not (".wav" or ".ogg")) throw new CliUsageException("render output must use .wav or .ogg.");
        progress.Stage("Rendering audio");
        WriteMedia(output, temporary => OfflineRenderer.RenderFile(args.Input, args.Value("css")!, temporary, new OfflineRenderer.RenderOptions { SourceText = args.Value("text") }));
        progress.Completed(output);
        Console.WriteLine($"Rendered {args.Input} with {args.Value("css")} to {output}.");
        return 0;
    }

    private static int Visual(CliArguments args)
    {
        using var progress = BeginProgress(args);
        progress.Stage("Compiling visual timeline");
        var loaded = ProgramLoader.Load(args.Input); var timeline = VisualInterpreter.Interpret(loaded.Program);
        progress.Completed(args.Input);
        foreach (var (message, location) in loaded.SourceWarnings) Console.Error.WriteLine(Diagnostics.Warning(message, args.Input, location));
        Console.WriteLine(FormattableString.Invariant($"Temporal storyboard: {timeline.Duration.TotalSeconds:0.#########}s total, {timeline.Visuals.Count} visual interval(s), {timeline.AudioSyncPoints.Count} audio anchor(s)."));
        foreach (var visual in timeline.Visuals)
            Console.WriteLine(FormattableString.Invariant($"  [{visual.Start.TotalSeconds:0.#########}s..{visual.End.TotalSeconds:0.#########}s) {visual.Name}") + string.Concat(visual.Automations.Select(a => FormattableString.Invariant($" | {a.Property} {a.From} -> {a.To} over {a.Duration.TotalSeconds:0.#########}s"))));
        foreach (var anchor in timeline.AudioSyncPoints) Console.WriteLine(FormattableString.Invariant($"  audio sync at {anchor.Time.TotalSeconds:0.#########}s"));
        foreach (var query in args.Values("at"))
        {
            var time = CliArguments.ParseTime(query); var state = timeline.StateAt(time);
            var elements = string.Join(" + ", state.Elements.Select(e => e.Name + (e.Properties.Count == 0 ? "" : "(" + string.Join(", ", e.Properties.Select(p => FormattableString.Invariant($"{p.Property}={p.Value:0.#########}"))) + ")")));
            Console.WriteLine(FormattableString.Invariant($"  t={time.TotalSeconds:0.#########}s => ") + (elements.Length == 0 ? "<empty>" : elements));
        }
        if (!args.Has("at")) Console.WriteLine("Query any moment without rendering frames: --at 0 --at 1.5 --at 4.5");
        return 0;
    }

    private static int Video(CliArguments args)
    {
        using var progress = BeginProgress(args);
        progress.Stage("Compiling");
        var analysis = SourceAnalysis.Load(args.Input, "video");
        progress.CompleteStage();
        progress.Stage("Preparing media timeline");
        var preflight = VideoPreflight.Check(args, analysis);
        progress.Detail(FormattableString.Invariant($"Duration: {preflight.DurationSeconds:0.###} s"));
        progress.Detail($"Resolution: {preflight.Width}x{preflight.Height}");
        progress.Detail($"FPS: {preflight.Fps}");
        progress.Detail($"Frames: {preflight.EstimatedFrameCount}");
        progress.CompleteStage();
        if (args.Has("check"))
        {
            if (args.Has("json")) Diagnostics.WriteResult(args.Command, args.Input, analysis.Diagnostics, analysis.Metadata, preflight);
            else { PrintDiagnostics(analysis); PrintMetadata(analysis.Metadata); Console.WriteLine(JsonSerializer.Serialize(preflight, Diagnostics.Json)); }
            return 0;
        }
        var plan = TemporalVisualSceneBuilder.Build(TemporalVideoExportPlanBuilder.Build(analysis.Timeline!, new(preflight.Fps)));
        var directory = Path.Combine(Path.GetTempPath(), $"soundscript-video-{Guid.NewGuid():N}"); Directory.CreateDirectory(directory);
        try
        {
            var audio = Path.Combine(directory, "audio.wav");
            progress.Stage("Rendering frames");
            var frameProgress = progress.CreateProgress("Frames rendered", preflight.EstimatedFrameCount);
            TemporalVideoFrameRenderer.WritePpmFrames(plan, directory, preflight.Width, preflight.Height, frameProgress, CliRuntime.CancellationToken, args.Integer("jobs", SafeDefaultJobs()));
            progress.CompleteStage();
            progress.Stage("Rendering deterministic audio");
            File.WriteAllBytes(audio, TemporalAudioRenderer.RenderToWavBytes(analysis.Program!, analysis.Timeline!.Duration));
            progress.CompleteStage();
            progress.Stage("Encoding WebM with FFmpeg");
            progress.Detail("Codec: VP9 + Opus");
            FfmpegWebmExporter.EncodeAndVerify(preflight.Ffmpeg, directory, audio, args.Value("out")!, plan, CliRuntime.CancellationToken,
                verificationStarted: () => { progress.CompleteStage(); progress.Stage("Verifying output"); });
            // EncodeAndVerify performs the decode verification before returning.
            progress.Detail("Video stream: OK"); progress.Detail("Audio stream: OK");
            progress.CompleteStage();
        }
        catch (DependencyException) { throw; }
        catch (OperationCanceledException)
        {
            progress.Cancelled();
            throw;
        }
        catch (Exception ex) { throw new ExportException($"Video export failed: {ex.Message}", ex); }
        finally { AtomicOutput.TryDeleteDirectory(directory); }
        progress.Completed(args.Value("out")!);
        PrintDiagnostics(analysis);
        Console.WriteLine($"Rendered and decode-verified {args.Value("out")}: {preflight.EstimatedFrameCount} StateAt(t) scenes at {preflight.Fps} FPS with synchronized deterministic SoundScript audio.");
        return 0;
    }

    private static int SafeDefaultJobs() => Math.Clamp(Environment.ProcessorCount, 1, 8);

    private static CliProgress BeginProgress(CliArguments args)
    {
        var progress = new CliProgress(args.Has("verbose"));
        CliRuntime.Progress = progress;
        return progress;
    }

    public static void WriteMedia(string path, Action<string> generate) => AtomicOutput.Write(path, generate, temporary =>
    {
        using var stream = File.OpenRead(temporary); var header = new byte[4]; stream.ReadExactly(header); stream.Close();
        switch (System.Text.Encoding.ASCII.GetString(header))
        {
            case "MThd": _ = MidiFile.Read(temporary); break;
            case "RIFF": _ = SourceAnalysis.ReadWavDuration(temporary); break;
            case "OggS":
                VerifyOgg(temporary);
                break;
            default: throw new ExportException("Generated media has an unrecognized header.");
        }
    });

    private static void VerifyOgg(string path)
    {
        using var input = File.OpenRead(path); using var reader = new BinaryReader(input);
        bool end = false;
        while (input.Position < input.Length)
        {
            var header = reader.ReadBytes(27);
            if (header.Length != 27 || System.Text.Encoding.ASCII.GetString(header, 0, 4) != "OggS" || header[4] != 0) throw new ExportException("Invalid Ogg page header.");
            end = (header[5] & 4) != 0;
            var lengths = reader.ReadBytes(header[26]);
            if (lengths.Length != header[26]) throw new ExportException("Truncated Ogg segment table.");
            var size = lengths.Sum(x => (int)x);
            if (reader.ReadBytes(size).Length != size) throw new ExportException("Truncated Ogg page.");
        }
        if (!end) throw new ExportException("Ogg is missing its end-of-stream page.");
    }

    private static void ConfigureWordbank(CliArguments args)
    {
        if (!TryConfigureWordbankCatalog(args, out var error)) throw new DependencyException(error!);
        if (args.Value("locale") is { } locale && !WordbankCatalog.TrySetActive(locale, out error)) throw new CliUsageException(error!);
    }
    private static bool TryConfigureWordbankCatalog(CliArguments args, out string? error)
    {
        var directory = args.Value("wordbank-dir") ?? Environment.GetEnvironmentVariable("WORDBANK_DIR"); error = null;
        if (string.IsNullOrWhiteSpace(directory)) return true;
        if (!WordbankCatalog.TryLoadFromRoot(directory, out error)) return false;
        CorpusCatalog.TryLoadFromWordbankRoot(directory, out _); return true;
    }
    private static void EnsureEmbeddedCorpus() { if (!CorpusCatalog.IsLoaded) CorpusCatalog.TryLoadEmbedded(); }
    private static VocalEngineOptions BuildVocalOptions(CliArguments args) => new()
    {
        Voice = args.Value("voice") ?? args.Value("offline-tts-voice") ?? "en", Locale = args.Value("locale"),
        Seed = args.Integer("seed", 7), Continuous = args.Has("continuous"),
        Pronunciations = args.Value("css") is { } css ? SoundCSSParser.ParsePronunciations(File.ReadAllText(css)) : null
    };
    private static bool TryGetFlagValue(CliArguments args, string flag, out string value) { value = args.Value(flag) ?? ""; return args.Has(flag); }
    private static bool TryGetDoubleFlag(CliArguments args, string flag, out double value) { value = args.Number(flag, 0); return args.Has(flag); }
    private static bool TryGetOfflineTts(CliArguments args, out string directory, out string? engine)
    { directory = args.Value("offline-tts-dir") ?? "vocal-stems"; engine = args.Value("offline-tts"); return args.Has("offline-tts"); }

    private static int VocalGenerate(CliArguments args)
    {
        using var progress = BeginProgress(args);
        progress.Stage("Preparing vocal engine");
        ConfigureWordbank(args); EnsureEmbeddedCorpus();
        var engine = args.Value("engine") is { } name ? VocalEngineFactory.Create(name) : VocalEngineFactory.CreateDefault();
        var output = args.Value("out") ?? $"{TtsDirectoryMapper.Slugify(args.Input)}.wav"; var options = BuildVocalOptions(args);
        progress.CompleteStage(); progress.Stage("Generating vocal audio");
        WriteMedia(output, temporary => engine.Synthesize(args.Input, temporary, options));
        progress.Completed(output);
        Console.WriteLine($"Generated vocal stem ({engine.Name}) → {output}"); return 0;
    }
    private static int VocalBatch(CliArguments args)
    {
        using var progress = BeginProgress(args);
        progress.Stage("Preparing vocal batch");
        ConfigureWordbank(args); EnsureEmbeddedCorpus(); _ = SourceAnalysis.Load(args.Input, "wave");
        var engine = args.Value("engine") is { } name ? VocalEngineFactory.Create(name) : VocalEngineFactory.CreateDefault();
        progress.CompleteStage(); progress.Stage("Generating vocal stems");
        var items = VocalBatchExporter.ExportFromScript(args.Input, args.Value("out-dir")!, engine, BuildVocalOptions(args), args.Has("skip-existing"));
        progress.Completed(args.Value("out-dir")!);
        Console.WriteLine($"Generated {items.Count} vocal stem(s) ({engine.Name}) in {args.Value("out-dir")}.");
        foreach (var item in items) Console.WriteLine($"  {Path.GetFileName(item.FilePath)} ← \"{item.Text}\""); return 0;
    }
    private static int WordbankEnsure(CliArguments args)
    {
        using var progress = BeginProgress(args);
        progress.Stage("Preparing wordbank");
        ConfigureWordbank(args); EnsureEmbeddedCorpus();
        progress.CompleteStage(); progress.Stage("Ensuring lemma");
        var result = new WordbankAutoGenerate(new WordbankAutoGenerateOptions { Voice = args.Value("voice") }).EnsureLemma(args.Input, args.Value("locale") ?? WordbankCatalog.ActiveLocaleCode, args.Has("auto-generate-missing"));
        if (result.Status is not (WordbankAutoGenerateStatus.AlreadyPresent or WordbankAutoGenerateStatus.Generated)) throw new DependencyException(result.Reason!);
        progress.Completed(result.Path!);
        if (result.Status == WordbankAutoGenerateStatus.AlreadyPresent)
            Console.WriteLine($"Resolved '{args.Input}' ({args.Value("locale") ?? WordbankCatalog.ActiveLocaleCode}) → {result.Path}");
        else
            Console.WriteLine($"Generated '{args.Input}' ({args.Value("locale") ?? WordbankCatalog.ActiveLocaleCode}) via {WordbankAutoGenerate.GeneratorName} {result.GeneratorVersion} → {result.Path}");
        return 0;
    }
    private static int WordbankNormalize(CliArguments args)
    {
        using var progress = BeginProgress(args);
        progress.Stage("Preparing wordbank");
        ConfigureWordbank(args); EnsureEmbeddedCorpus(); var locale = args.Value("locale") ?? WordbankCatalog.ActiveLocaleCode; var normalizer = new WordbankNormalizer();
        progress.CompleteStage();
        if (args.Has("all"))
        {
            progress.Stage("Normalizing wordbank");
            int normalized = 0, missing = 0;
            var lemmas = CorpusCatalog.GetLemmaKeys(locale).ToArray();
            foreach (var lemma in lemmas)
            {
                if (normalizer.Normalize(lemma, locale).Success) normalized++; else missing++;
                progress.Progress("Lemmas normalized", normalized + missing, lemmas.Length);
            }
            progress.Completed(locale);
            Console.WriteLine($"Normalized {normalized} lemma(s) for '{locale}' ({missing} missing source audio)."); return 0;
        }
        progress.Stage("Normalizing lemma");
        var result = normalizer.Normalize(args.Input, locale);
        if (!result.Success) throw new DependencyException(result.Reason!);
        progress.Completed(result.Path!);
        Console.WriteLine($"Normalized '{args.Input}' ({locale}) → {result.Path}"); return 0;
    }
}

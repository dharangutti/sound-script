using SoundScript.Core;
using SoundScript.Parser;
using SoundScript.Midi;
using SoundScript.Vocal;
using SoundScript.Wave;
using SoundScript.Wave.Adapter;
using SoundScript.Wave.Io;
using SoundScript.Wave.Tts;
namespace SoundScript.Cli;
public static partial class CommandHandlers
{
public static int Wave(CliArguments args)
{
    using var progress = BeginProgress(args);
    if (!TryConfigureWordbankCatalog(args, out var wordbankError))
    {
        Console.Error.WriteLine(wordbankError);
        return 3;
    }

    EnsureEmbeddedCorpus();

    var scriptPath = args.Input;
    if (!File.Exists(scriptPath))
    {
        Console.Error.WriteLine($"Script not found: {scriptPath}");
        return 3;
    }

    var outputPath = args.Value("out");

    outputPath ??= Path.Combine(Directory.GetCurrentDirectory(), "output.wav");

    var stereo = args.Has("stereo");
    var scriptDirectory = Path.GetDirectoryName(Path.GetFullPath(scriptPath)) ?? Directory.GetCurrentDirectory();

    try
    {
        progress.Stage("Compiling");
        var analysis = SourceAnalysis.Load(scriptPath, "wave");
        progress.CompleteStage();
        var loaded = new LoadResult { Program = analysis.Program! };
        foreach (var diagnostic in analysis.Diagnostics) Console.Error.WriteLine(diagnostic);
        foreach (var warning in loaded.Warnings)
            Console.Error.WriteLine($"warning: {warning}");

        progress.Stage("Preparing Wave timeline");
        var adapted = analysis.Wave ?? AstToNoteEventAdapter.Adapt(loaded.Program);
        IReadOnlyList<WaveExternalOverlay>? externalOverlays = null;
        IReadOnlyList<SampleOverlayRequest>? additionalOverlays = null;

        if (TryGetFlagValue(args, "--tts-dir", out var ttsDir))
        {
            var resolvedTtsDir = WavePathResolver.Resolve(scriptDirectory, ttsDir);
            additionalOverlays = TtsDirectoryMapper.BuildOverlays(adapted.SpeakTimings, resolvedTtsDir);
        }

        var hasOfflineTts = TryGetOfflineTts(args, out var offlineTtsDir, out var offlineEngineName);
        var cssProvided = TryGetFlagValue(args, "--css", out var cssPathArg) && !string.IsNullOrWhiteSpace(cssPathArg);

        // A --css stylesheet only affects speak lines rendered through the
        // wordbank/composite vocal engine (--offline-tts). Without that, --css
        // was silently ignored — word rules "didn't take effect" and output was
        // inconsistent. When --css is given without --offline-tts and the script
        // has speak lines, auto-enable wordbank offline-tts so the rules apply.
        var hasSpeakLines = false;
        foreach (var (speak, _) in adapted.SpeakTimings)
        {
            if (string.IsNullOrWhiteSpace(speak.SamplePath))
            {
                hasSpeakLines = true;
                break;
            }
        }

        var autoCss = cssProvided && !hasOfflineTts && hasSpeakLines;

        if (hasOfflineTts || autoCss)
        {
            var dir = hasOfflineTts ? offlineTtsDir : "vocal-stems";
            var engineName = hasOfflineTts ? offlineEngineName : "wordbank";
            var resolvedOfflineTtsDir = WavePathResolver.Resolve(scriptDirectory, dir);
            var engine = string.IsNullOrWhiteSpace(engineName)
                ? VocalEngineFactory.CreateDefault()
                : VocalEngineFactory.Create(engineName);
            var vocalOptions = BuildVocalOptions(args);
            VocalBatchExporter.ExportFromScript(scriptPath, resolvedOfflineTtsDir, engine, vocalOptions);
            additionalOverlays = TtsDirectoryMapper.BuildOverlays(adapted.SpeakTimings, resolvedOfflineTtsDir);
            Console.Error.WriteLine(autoCss
                ? $"note: --css applies to speak lines via wordbank offline-tts (auto-enabled). Stems in {resolvedOfflineTtsDir}."
                : $"offline-tts: generated vocal stems in {resolvedOfflineTtsDir} via {engine.Name}.");
        }
        else if (cssProvided && !hasSpeakLines)
        {
            Console.Error.WriteLine("note: --css only styles speak lines; this script has none, so it had no effect.");
        }

        if (TryGetFlagValue(args, "--vocal", out var vocalPath))
        {
            var vocalGain = TryGetDoubleFlag(args, "--vocal-gain", out var gain) ? gain : 1.0;
            var vocalAtBeats = TryGetDoubleFlag(args, "--vocal-at", out var atBeats) ? atBeats : 0.0;
            var vocalSamples = WavReader.ReadMono(WavePathResolver.Resolve(scriptDirectory, vocalPath));
            var tempo = Interpreter.Interpret(loaded.Program).Tempo;
            var startSeconds = vocalAtBeats * (60.0 / tempo);
            externalOverlays = [new WaveExternalOverlay(vocalSamples, startSeconds, vocalGain)];
        }

        progress.CompleteStage();
        var renderOptions = new WaveRenderOptions
        {
            ScriptDirectory = scriptDirectory,
            AdditionalSampleOverlays = additionalOverlays,
            ExternalOverlays = externalOverlays,
        };

        progress.Stage("Rendering audio");
        if (stereo)
            WriteMedia(outputPath, temporary => WaveRenderer.RenderStereo(adapted, loaded.Program, temporary, renderOptions));
        else
            WriteMedia(outputPath, temporary => WaveRenderer.Render(adapted, loaded.Program, temporary, renderOptions));
        progress.Completed(outputPath);

        Console.WriteLine($"Rendered {scriptPath} directly to {outputPath} (no MIDI step).");
        return 0;
    }
    catch (OperationCanceledException)
    {
        progress.Cancelled();
        throw;
    }
}

}

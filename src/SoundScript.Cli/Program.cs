using SoundScript.Compose;
using SoundScript.Core;
using SoundScript.Midi;
using SoundScript.Parser;
using SoundScript.Prosody;
using SoundScript.Timbre;
using SoundScript.Vocal;
using SoundScript.Voice;
using SoundScript.Wave;
using SoundScript.Wave.Adapter;
using SoundScript.Wave.Io;
using SoundScript.Wave.Tts;
using SoundScript.Wordbank;

if (args.Length >= 1 && (args[0] == "--version" || args[0] == "-v"))
{
    Console.WriteLine($"soundscript {VersionInfo.Number} ({VersionInfo.Display})");
    return 0;
}

if (args.Length < 2)
{
    PrintUsage();
    return 1;
}

return args[0].ToLowerInvariant() switch
{
    "run" => Run(args),
    "compose" => Compose(args),
    "prosody" => Prosody(args),
    "render" => Render(args),
    "wave" => Wave(args),
    "vocal" => Vocal(args),
    _ => PrintUsage()
};

static int PrintUsage()
{
    Console.Error.WriteLine("Usage: soundscript --version | -v");
    Console.Error.WriteLine("       soundscript run <script.ss> [output.mid]");
    Console.Error.WriteLine("       soundscript compose \"<text>\" [output.mid|output.wav] [--locale en|es|fr] [--append <script.ss>] [--emit-ss <path.ss>] [--wave] [--stereo]");
    Console.Error.WriteLine("       soundscript prosody \"<text>\" [output.mid|output.wav] [--locale en|es|fr] [--append <script.ss>] [--emit-ss <path.ss>] [--wave] [--stereo]");
    Console.Error.WriteLine("       soundscript render <file.mid> --css <style.ssc> [--out <output.wav|ogg>] [--text \"<source text>\"]");
    Console.Error.WriteLine("       soundscript wave <script.ss|script.ssw> [output.wav] [--stereo] [--vocal <stem.wav>] [--vocal-at=<beats>] [--vocal-gain=<0-1>] [--tts-dir <folder>] [--offline-tts [espeak|prosody]] [--offline-tts-dir <folder>]");
    Console.Error.WriteLine("       soundscript vocal generate \"<text>\" --out <file.wav> [--engine espeak|prosody] [--voice <id>] [--seed=<n>]");
    Console.Error.WriteLine("       soundscript vocal batch <script.ss|script.ssw> --out-dir <folder> [--engine espeak|prosody] [--voice <id>] [--seed=<n>] [--skip-existing]");
    return 1;
}

static int Run(string[] args)
{
    var scriptPath = args[1];
    if (!File.Exists(scriptPath))
    {
        Console.Error.WriteLine($"Script not found: {scriptPath}");
        return 1;
    }

    var outputPath = args.Length > 2
        ? args[2]
        : Path.Combine(Directory.GetCurrentDirectory(), "output.mid");

    try
    {
        var loaded = ProgramLoader.Load(scriptPath);
        var interpreted = Interpreter.Interpret(loaded.Program, scriptPath);
        VocalInterpreter.Apply(loaded.Program, interpreted);
        foreach (var warning in loaded.Warnings)
            interpreted.Warnings.Add(warning);

        MidiGenerator.Write(interpreted, outputPath);

        foreach (var warning in interpreted.Warnings)
            Console.Error.WriteLine($"warning: {warning}");

        var noteCount = interpreted.Tracks.Sum(t => t.Notes.Count);
        var summary = $"Wrote {noteCount} notes across {interpreted.Tracks.Count} track(s)";
        if (interpreted.VocalTracks.Count > 0)
        {
            var syllableCount = interpreted.VocalTracks.Sum(v => v.Syllables.Count);
            summary += $" and {syllableCount} sung syllable(s) across {interpreted.VocalTracks.Count} voice(s)";
        }

        Console.WriteLine($"{summary} to {outputPath} at {interpreted.Tempo} BPM.");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static int Compose(string[] args)
{
    var text = args[1];
    if (string.IsNullOrWhiteSpace(text))
    {
        Console.Error.WriteLine("Nothing to compose: the text is empty.");
        return 1;
    }

    string? outputPath = null;
    string? appendScriptPath = null;
    string? emitSsPath = null;
    var waveOutput = false;
    var stereo = false;

    for (var i = 2; i < args.Length; i++)
    {
        if (string.Equals(args[i], "--append", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("--append requires a script path: compose \"text\" --append file.ss");
                return 1;
            }

            appendScriptPath = args[++i];
        }
        else if (string.Equals(args[i], "--emit-ss", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("--emit-ss requires an output path: compose \"text\" --emit-ss melody.ss");
                return 1;
            }

            emitSsPath = args[++i];
        }
        else if (string.Equals(args[i], "--wave", StringComparison.OrdinalIgnoreCase))
        {
            waveOutput = true;
        }
        else if (string.Equals(args[i], "--stereo", StringComparison.OrdinalIgnoreCase))
        {
            stereo = true;
        }
        else if (string.Equals(args[i], "--locale", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("--locale requires a locale code: compose \"text\" --locale en");
                return 1;
            }

            if (!WordbankCatalog.TrySetActive(args[++i], out var localeError))
            {
                Console.Error.WriteLine(localeError);
                return 1;
            }
        }
        else
        {
            outputPath = args[i];
        }
    }

    if (emitSsPath is not null && appendScriptPath is not null)
    {
        Console.Error.WriteLine(
            "--emit-ss and --append cannot be combined: --emit-ss exports only the standalone composed AST, " +
            "which would not match the merged --append MIDI output.");
        return 1;
    }

    if (waveOutput && appendScriptPath is not null)
    {
        Console.Error.WriteLine(
            "--wave and --append cannot be combined: --append merges into an interpreted MIDI program, " +
            "which has no single AST to render through SoundScript.Wave.");
        return 1;
    }

    outputPath ??= Path.Combine(
        Directory.GetCurrentDirectory(),
        waveOutput ? "output.wav" : "output.mid");

    try
    {
        if (waveOutput)
        {
            var ast = PhonemeComposer.BuildAst(text);
            if (emitSsPath is not null)
                File.WriteAllText(emitSsPath, SsPrinter.Print(ast));

            if (stereo)
                WaveRenderer.RenderStereo(ast, outputPath);
            else
                WaveRenderer.Render(ast, outputPath);

            var waveSyllables = PhonemeComposer.SplitSyllables(text);
            var waveNoteCount = AstToNoteEventAdapter.Convert(ast).Values.Sum(t => t.Count);
            var waveTempo = Interpreter.Interpret(ast).Tempo;
            var waveSummary = $"Composed {waveSyllables.Count} syllable(s) into {waveNoteCount} note(s)";
            if (emitSsPath is not null)
                waveSummary += $" and .ss source to {emitSsPath}";
            Console.WriteLine($"{waveSummary} and rendered to {outputPath} via SoundScript.Wave (no MIDI step) at {waveTempo} BPM.");
            return 0;
        }

        SoundScript.Core.InterpretedProgram interpreted;
        if (appendScriptPath is not null)
        {
            if (!File.Exists(appendScriptPath))
            {
                Console.Error.WriteLine($"Script not found: {appendScriptPath}");
                return 1;
            }

            var loaded = ProgramLoader.Load(appendScriptPath);
            interpreted = Interpreter.Interpret(loaded.Program, appendScriptPath);
            VocalInterpreter.Apply(loaded.Program, interpreted);
            foreach (var warning in loaded.Warnings)
                interpreted.Warnings.Add(warning);

            PhonemeComposer.AppendTo(interpreted, text);
        }
        else
        {
            var ast = PhonemeComposer.BuildAst(text);
            if (emitSsPath is not null)
                File.WriteAllText(emitSsPath, SsPrinter.Print(ast));

            interpreted = Interpreter.Interpret(ast);
        }

        MidiGenerator.Write(interpreted, outputPath);

        foreach (var warning in interpreted.Warnings)
            Console.Error.WriteLine($"warning: {warning}");

        var syllables = PhonemeComposer.SplitSyllables(text);
        var noteCount = interpreted.Tracks.Sum(t => t.Notes.Count);
        var summary = appendScriptPath is not null
            ? $"Composed {syllables.Count} syllable(s) and appended the phoneme track to {appendScriptPath}: " +
              $"{noteCount} note(s) across {interpreted.Tracks.Count} track(s)"
            : $"Composed {syllables.Count} syllable(s) into {noteCount} note(s)";
        if (emitSsPath is not null)
            summary += $" and .ss source to {emitSsPath}";
        Console.WriteLine($"{summary} to {outputPath} at {interpreted.Tempo} BPM.");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static int Prosody(string[] args)
{
    var text = args[1];
    if (string.IsNullOrWhiteSpace(text))
    {
        Console.Error.WriteLine("Nothing to compose: the text is empty.");
        return 1;
    }

    string? outputPath = null;
    string? appendScriptPath = null;
    string? emitSsPath = null;
    var waveOutput = false;
    var stereo = false;

    for (var i = 2; i < args.Length; i++)
    {
        if (string.Equals(args[i], "--append", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("--append requires a script path: prosody \"text\" --append file.ss");
                return 1;
            }

            appendScriptPath = args[++i];
        }
        else if (string.Equals(args[i], "--emit-ss", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("--emit-ss requires an output path: prosody \"text\" --emit-ss melody.ss");
                return 1;
            }

            emitSsPath = args[++i];
        }
        else if (string.Equals(args[i], "--wave", StringComparison.OrdinalIgnoreCase))
        {
            waveOutput = true;
        }
        else if (string.Equals(args[i], "--stereo", StringComparison.OrdinalIgnoreCase))
        {
            stereo = true;
        }
        else if (string.Equals(args[i], "--locale", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("--locale requires a locale code: prosody \"text\" --locale en");
                return 1;
            }

            if (!WordbankCatalog.TrySetActive(args[++i], out var localeError))
            {
                Console.Error.WriteLine(localeError);
                return 1;
            }
        }
        else
        {
            outputPath = args[i];
        }
    }

    if (emitSsPath is not null && appendScriptPath is not null)
    {
        Console.Error.WriteLine(
            "--emit-ss and --append cannot be combined: --emit-ss exports only the standalone composed AST, " +
            "which would not match the merged --append MIDI output.");
        return 1;
    }

    if (waveOutput && appendScriptPath is not null)
    {
        Console.Error.WriteLine(
            "--wave and --append cannot be combined: --append merges into an interpreted MIDI program, " +
            "which has no single AST to render through SoundScript.Wave.");
        return 1;
    }

    outputPath ??= Path.Combine(
        Directory.GetCurrentDirectory(),
        waveOutput ? "output.wav" : "output.mid");

    try
    {
        if (waveOutput)
        {
            var ast = ProsodyComposer.BuildAst(text);
            if (emitSsPath is not null)
                File.WriteAllText(emitSsPath, SsPrinter.Print(ast));

            if (stereo)
                WaveRenderer.RenderStereo(ast, outputPath);
            else
                WaveRenderer.Render(ast, outputPath);

            var waveSyllableCount = WordTokenizer.Tokenize(text).Sum(w => w.Syllables.Count);
            var waveNoteCount = AstToNoteEventAdapter.Convert(ast).Values.Sum(t => t.Count);
            var waveTempo = Interpreter.Interpret(ast).Tempo;
            var waveSummary = $"Composed {waveSyllableCount} syllable(s) into {waveNoteCount} note(s) (word-level prosody)";
            if (emitSsPath is not null)
                waveSummary += $" and .ss source to {emitSsPath}";
            Console.WriteLine($"{waveSummary} and rendered to {outputPath} via SoundScript.Wave (no MIDI step) at {waveTempo} BPM.");
            return 0;
        }

        SoundScript.Core.InterpretedProgram interpreted;
        if (appendScriptPath is not null)
        {
            if (!File.Exists(appendScriptPath))
            {
                Console.Error.WriteLine($"Script not found: {appendScriptPath}");
                return 1;
            }

            var loaded = ProgramLoader.Load(appendScriptPath);
            interpreted = Interpreter.Interpret(loaded.Program, appendScriptPath);
            VocalInterpreter.Apply(loaded.Program, interpreted);
            foreach (var warning in loaded.Warnings)
                interpreted.Warnings.Add(warning);

            ProsodyComposer.AppendTo(interpreted, text);
        }
        else
        {
            var ast = ProsodyComposer.BuildAst(text);
            if (emitSsPath is not null)
                File.WriteAllText(emitSsPath, SsPrinter.Print(ast));

            interpreted = Interpreter.Interpret(ast);
        }

        MidiGenerator.Write(interpreted, outputPath);

        foreach (var warning in interpreted.Warnings)
            Console.Error.WriteLine($"warning: {warning}");

        var syllableCount = WordTokenizer.Tokenize(text).Sum(w => w.Syllables.Count);
        var noteCount = interpreted.Tracks.Sum(t => t.Notes.Count);
        var summary = appendScriptPath is not null
            ? $"Composed {syllableCount} syllable(s) and appended the prosody track to {appendScriptPath}: " +
              $"{noteCount} note(s) across {interpreted.Tracks.Count} track(s)"
            : $"Composed {syllableCount} syllable(s) into {noteCount} note(s)";
        if (emitSsPath is not null)
            summary += $" and .ss source to {emitSsPath}";
        Console.WriteLine($"{summary} to {outputPath} at {interpreted.Tempo} BPM.");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static int Render(string[] args)
{
    var midiPath = args[1];
    if (!File.Exists(midiPath))
    {
        Console.Error.WriteLine($"MIDI not found: {midiPath}");
        return 1;
    }

    string? cssPath = null;
    string? outputPath = null;
    string? sourceText = null;

    for (var i = 2; i < args.Length; i++)
    {
        if (string.Equals(args[i], "--css", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("--css requires a SoundCSS file path.");
                return 1;
            }

            cssPath = args[++i];
        }
        else if (string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("--out requires an output audio path.");
                return 1;
            }

            outputPath = args[++i];
        }
        else if (string.Equals(args[i], "--text", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("--text requires the source plain text for phoneme alignment.");
                return 1;
            }

            sourceText = args[++i];
        }
        else
        {
            Console.Error.WriteLine($"Unknown render argument: {args[i]}");
            return 1;
        }
    }

    if (cssPath is null || !File.Exists(cssPath))
    {
        Console.Error.WriteLine("Render requires --css <style.ssc> pointing to an existing SoundCSS file.");
        return 1;
    }

    outputPath ??= Path.Combine(Directory.GetCurrentDirectory(), "output.wav");

    try
    {
        var options = new OfflineRenderer.RenderOptions { SourceText = sourceText };
        OfflineRenderer.RenderFile(midiPath, cssPath, outputPath, options);

        Console.WriteLine($"Rendered {midiPath} with {cssPath} to {outputPath}.");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static int Wave(string[] args)
{
    var scriptPath = args[1];
    if (!File.Exists(scriptPath))
    {
        Console.Error.WriteLine($"Script not found: {scriptPath}");
        return 1;
    }

    string? outputPath = null;
    for (var i = 2; i < args.Length; i++)
    {
        if (args[i].StartsWith("--", StringComparison.Ordinal))
            continue;
        if (i > 0 && WaveFlagTakesValue(args[i - 1]))
            continue;

        outputPath = args[i];
        break;
    }

    outputPath ??= Path.Combine(Directory.GetCurrentDirectory(), "output.wav");

    var stereo = args.Contains("--stereo");
    var scriptDirectory = Path.GetDirectoryName(Path.GetFullPath(scriptPath)) ?? Directory.GetCurrentDirectory();

    try
    {
        var loaded = ProgramLoader.Load(scriptPath);
        foreach (var warning in loaded.Warnings)
            Console.Error.WriteLine($"warning: {warning}");

        var adapted = AstToNoteEventAdapter.Adapt(loaded.Program);
        IReadOnlyList<WaveExternalOverlay>? externalOverlays = null;
        IReadOnlyList<SampleOverlayRequest>? additionalOverlays = null;

        if (TryGetFlagValue(args, "--tts-dir", out var ttsDir))
            additionalOverlays = TtsDirectoryMapper.BuildOverlays(adapted.SpeakTimings, ttsDir);

        if (TryGetOfflineTts(args, scriptPath, scriptDirectory, out var offlineTtsDir, out var offlineEngineName))
        {
            var engine = string.IsNullOrWhiteSpace(offlineEngineName)
                ? VocalEngineFactory.CreateDefault()
                : VocalEngineFactory.Create(offlineEngineName);
            var vocalOptions = BuildVocalOptions(args);
            VocalBatchExporter.ExportFromScript(scriptPath, offlineTtsDir, engine, vocalOptions);
            additionalOverlays = TtsDirectoryMapper.BuildOverlays(adapted.SpeakTimings, offlineTtsDir);
            Console.Error.WriteLine($"offline-tts: generated vocal stems in {offlineTtsDir} via {engine.Name}.");
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

        var renderOptions = new WaveRenderOptions
        {
            ScriptDirectory = scriptDirectory,
            AdditionalSampleOverlays = additionalOverlays,
            ExternalOverlays = externalOverlays,
        };

        if (stereo)
            WaveRenderer.RenderStereo(loaded.Program, outputPath, renderOptions);
        else
            WaveRenderer.Render(loaded.Program, outputPath, renderOptions);

        Console.WriteLine($"Rendered {scriptPath} directly to {outputPath} (no MIDI step).");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static bool WaveFlagTakesValue(string flag) =>
    flag is "--vocal" or "--tts-dir" or "--vocal-at" or "--vocal-gain"
        or "--offline-tts" or "--offline-tts-dir" or "--offline-tts-voice";

static int Vocal(string[] args)
{
    if (args.Length < 3)
    {
        PrintVocalUsage();
        return 1;
    }

    return args[1].ToLowerInvariant() switch
    {
        "generate" => VocalGenerate(args),
        "batch" => VocalBatch(args),
        _ => PrintVocalUsage(),
    };
}

static int PrintVocalUsage()
{
    Console.Error.WriteLine("Usage: soundscript vocal generate \"<text>\" --out <file.wav> [--engine espeak|prosody] [--voice <id>] [--seed=<n>]");
    Console.Error.WriteLine("       soundscript vocal batch <script.ss|script.ssw> --out-dir <folder> [--engine espeak|prosody] [--voice <id>] [--seed=<n>] [--skip-existing]");
    return 1;
}

static int VocalGenerate(string[] args)
{
    string? text = null;
    string? outPath = null;
    string? engineName = null;

    for (var i = 2; i < args.Length; i++)
    {
        if (TryMatchFlag(args, ref i, "--out", out var parsedOut))
        {
            if (parsedOut is not null)
                outPath = parsedOut;
            continue;
        }

        if (TryMatchFlag(args, ref i, "--engine", out var parsedEngine))
        {
            if (parsedEngine is not null)
                engineName = parsedEngine;
            continue;
        }

        if (args[i].StartsWith("--", StringComparison.Ordinal))
            continue;
        text ??= args[i];
    }

    if (string.IsNullOrWhiteSpace(text))
    {
        Console.Error.WriteLine("Missing speak text.");
        return PrintVocalUsage();
    }

    outPath ??= Path.Combine(Directory.GetCurrentDirectory(), $"{TtsDirectoryMapper.Slugify(text)}.wav");
    var options = BuildVocalOptions(args);

    try
    {
        var engine = string.IsNullOrWhiteSpace(engineName)
            ? VocalEngineFactory.CreateDefault()
            : VocalEngineFactory.Create(engineName);
        engine.Synthesize(text, outPath, options);
        Console.WriteLine($"Generated vocal stem ({engine.Name}) → {outPath}");
        if (engine.Name == "prosody")
        {
            Console.Error.WriteLine(
                "note: prosody produces synthetic phoneme tones (buzzy speech-like blips), not human speech. " +
                "Install espeak-ng on your system and use --engine espeak for spoken words.");
        }
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static int VocalBatch(string[] args)
{
    if (args.Length < 4)
        return PrintVocalUsage();

    var scriptPath = args[2];
    if (scriptPath.StartsWith("--", StringComparison.Ordinal))
        return PrintVocalUsage();

    if (!File.Exists(scriptPath))
    {
        Console.Error.WriteLine($"Script not found: {scriptPath}");
        return 1;
    }

    if (!TryGetFlagValue(args, "--out-dir", out var outDir) || string.IsNullOrWhiteSpace(outDir))
    {
        Console.Error.WriteLine("Missing --out-dir <folder>.");
        return PrintVocalUsage();
    }

    var engineName = TryGetFlagValue(args, "--engine", out var namedEngine) ? namedEngine : null;
    var skipExisting = args.Contains("--skip-existing");
    var options = BuildVocalOptions(args);

    try
    {
        var engine = string.IsNullOrWhiteSpace(engineName)
            ? VocalEngineFactory.CreateDefault()
            : VocalEngineFactory.Create(engineName);
        var items = VocalBatchExporter.ExportFromScript(scriptPath, outDir, engine, options, skipExisting);
        Console.WriteLine($"Generated {items.Count} vocal stem(s) ({engine.Name}) in {outDir}.");
        foreach (var item in items)
            Console.WriteLine($"  {Path.GetFileName(item.FilePath)} ← \"{item.Text}\"");
        if (engine.Name == "prosody")
        {
            Console.Error.WriteLine(
                "note: prosody produces synthetic phoneme tones (buzzy speech-like blips), not human speech. " +
                "Install espeak-ng on your system and use --engine espeak for spoken words.");
        }
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static VocalEngineOptions BuildVocalOptions(string[] args)
{
    var voice = TryGetFlagValue(args, "--voice", out var v) ? v
        : TryGetFlagValue(args, "--offline-tts-voice", out var ov) ? ov
        : "en";
    var seed = TryGetIntFlag(args, "--seed", out var s) ? s : 7;
    return new VocalEngineOptions { Voice = voice, Seed = seed };
}

static bool TryGetOfflineTts(
    string[] args,
    string scriptPath,
    string scriptDirectory,
    out string ttsDirectory,
    out string? engineName)
{
    ttsDirectory = string.Empty;
    engineName = null;

    for (var i = 0; i < args.Length; i++)
    {
        if (!string.Equals(args[i], "--offline-tts", StringComparison.OrdinalIgnoreCase))
            continue;

        if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            engineName = args[i + 1];
        }

        ttsDirectory = TryGetFlagValue(args, "--offline-tts-dir", out var dir) && !string.IsNullOrWhiteSpace(dir)
            ? dir
            : Path.Combine(scriptDirectory, "vocal-stems");

        return true;
    }

    return false;
}

static bool TryMatchFlag(string[] args, ref int index, string flag, out string? value)
{
    value = null;
    if (!string.Equals(args[index], flag, StringComparison.OrdinalIgnoreCase))
        return false;

    if (index + 1 >= args.Length)
        return true;

    value = args[index + 1];
    index++;
    return true;
}

static bool TryGetIntFlag(string[] args, string flag, out int value)
{
    value = 0;
    if (!TryGetFlagValue(args, flag, out var text))
        return false;

    return int.TryParse(text, out value);
}

static bool TryGetFlagValue(string[] args, string flag, out string value)
{
    value = string.Empty;
    for (var i = 0; i < args.Length; i++)
    {
        if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
        {
            value = args[i + 1];
            return true;
        }

        var prefix = flag + "=";
        if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = args[i][prefix.Length..];
            return true;
        }
    }

    return false;
}

static bool TryGetDoubleFlag(string[] args, string flag, out double value)
{
    value = 0;
    if (!TryGetFlagValue(args, flag, out var text))
        return false;

    return double.TryParse(text, System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out value);
}

using SoundScript.Core.Ast;
using SoundScript.Parser;
using SoundScript.Wave.Adapter;
using SoundScript.Wave.Tts;

namespace SoundScript.Vocal;

public sealed record VocalBatchItem(string Text, string FilePath, double StartTimeSeconds);

/// <summary>Generates slug-named WAV stems for every <c>speak</c> phrase in a script.</summary>
public static class VocalBatchExporter
{
    public static IReadOnlyList<VocalBatchItem> ExportFromScript(
        string scriptPath,
        string outputDirectory,
        IVocalEngine engine,
        VocalEngineOptions options,
        bool skipExisting = false)
    {
        var loaded = ProgramLoader.Load(scriptPath);
        return ExportFromProgram(loaded.Program, outputDirectory, engine, options, skipExisting);
    }

    public static IReadOnlyList<VocalBatchItem> ExportFromProgram(
        ProgramNode program,
        string outputDirectory,
        IVocalEngine engine,
        VocalEngineOptions options,
        bool skipExisting = false)
    {
        Directory.CreateDirectory(outputDirectory);

        var adapted = AstToNoteEventAdapter.Adapt(program);
        var results = new List<VocalBatchItem>();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var index = 0;

        foreach (var (speak, startSeconds) in adapted.SpeakTimings)
        {
            if (!string.IsNullOrWhiteSpace(speak.SamplePath))
                continue;

            index++;
            var slug = TtsDirectoryMapper.Slugify(speak.Text);
            counts.TryGetValue(slug, out var seen);
            counts[slug] = seen + 1;

            var fileName = seen == 0 ? $"{slug}.wav" : $"{index:D3}-{slug}.wav";
            var filePath = Path.Combine(outputDirectory, fileName);

            if (!skipExisting || !File.Exists(filePath))
            {
                var stemOptions = new VocalEngineOptions
                {
                    Voice = options.Voice,
                    Locale = options.Locale,
                    Seed = speak.Seed ?? options.Seed,
                    OutputGain = options.OutputGain,
                    Pronunciations = options.Pronunciations,
                };
                engine.Synthesize(speak.Text, filePath, stemOptions);
            }

            results.Add(new VocalBatchItem(speak.Text, filePath, startSeconds));
        }

        return results;
    }
}

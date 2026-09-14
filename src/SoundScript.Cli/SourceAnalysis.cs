using System.Text.RegularExpressions;
using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Midi;
using SoundScript.Parser;
using SoundScript.Timbre;
using SoundScript.Visual;
using SoundScript.Voice;
using SoundScript.Wave.Adapter;
using SoundScript.Wave.Io;

namespace SoundScript.Cli;

public sealed class ProgramMetadata
{
    public string Kind { get; set; } = "music";
    public string? AudioBackend { get; set; }
    public double? Tempo { get; set; }
    public double? DurationSeconds { get; set; }
    public int TrackCount { get; set; }
    public int NoteCount { get; set; }
    public int EventCount { get; set; }
    public int VisualCount { get; set; }
    public double? AudioDurationSeconds { get; set; }
    public double? VisualDurationSeconds { get; set; }
    public string DurationBasis { get; set; } = "scheduled notes";
    public double[] AudioSyncSeconds { get; set; } = [];
    public List<string> SupportedOutputTypes { get; set; } = [];
    public int? StyleRuleCount { get; set; }
}

public sealed class SourceAnalysis
{
    public ProgramNode? Program { get; private set; }
    public VisualTimeline? Timeline { get; private set; }
    public InterpretedProgram? Midi { get; private set; }
    public WaveAdaptationResult? Wave { get; private set; }
    public ProgramMetadata Metadata { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = [];

    public static SourceAnalysis Load(string input, string? target = null)
    {
        var analysis = new SourceAnalysis();
        var path = Path.GetFullPath(input);
        if (!File.Exists(path)) throw new FileNotFoundException($"Input file not found: {path}", path);
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".ss" or ".ssw" or ".ssv" or ".ssc"))
            throw new CliUsageException("Supported source types: .ss, .ssw, .ssv, .ssc.");
        if (extension == ".ssc")
        {
            if (target is not null) throw new CliUsageException("A SoundCSS stylesheet needs a MIDI input to render; --target is not applicable.");
            analysis.Metadata.Kind = "soundcss";
            analysis.Metadata.StyleRuleCount = ValidateCss(path);
            analysis.Metadata.DurationBasis = "not applicable";
            return analysis;
        }
        var loaded = ProgramLoader.Load(path);
        analysis.Program = loaded.Program;
        foreach (var (message, location) in loaded.SourceWarnings)
            analysis.Diagnostics.Add(Cli.Diagnostics.Warning(message, path, location, "SS2101"));
        var nodes = Walk(loaded.Program.Statements).ToArray();
        CheckRecursion(loaded.Program);
        var waveOnly = nodes.Any(n => n is SpeakNode or SampleNode or EffectNode);
        var visual = nodes.Any(n => n is VisualNode or VisualWaitNode or AudioSyncNode);
        analysis.Timeline = VisualInterpreter.Interpret(loaded.Program);
        var metadata = analysis.Metadata;
        metadata.Kind = visual ? "media" : waveOnly || extension == ".ssw" ? "wave" : "music";
        metadata.VisualCount = analysis.Timeline.Visuals.Count;
        metadata.VisualDurationSeconds = visual ? analysis.Timeline.Duration.TotalSeconds : null;
        metadata.AudioSyncSeconds = analysis.Timeline.AudioSyncPoints.Select(a => a.Time.TotalSeconds).ToArray();
        bool wave = target is "wave" or "video" || target != "midi" && (waveOnly || extension == ".ssw" || visual);
        if (wave)
        {
            var adapted = AstToNoteEventAdapter.Adapt(loaded.Program);
            analysis.Wave = adapted;
            var effects = EffectSettingsFactory.FromProgram(loaded.Program);
            metadata.AudioBackend = "wave";
            metadata.Tempo = adapted.TempoMap.GetBpmAt(0);
            metadata.TrackCount = adapted.Tracks.Count;
            metadata.NoteCount = adapted.Tracks.Values.Sum(t => t.Count);
            metadata.EventCount = metadata.NoteCount + adapted.SampleOverlays.Count;
            var endSamples = adapted.Tracks.Values.SelectMany(t => t).Select(n =>
                Math.Max(0, Math.Round(n.StartTimeSeconds * WavWriter.SampleRate)) +
                Math.Ceiling((Math.Max(0, n.DurationSeconds) + Math.Max(0, n.Timbre.Envelope.Release)) * WavWriter.SampleRate)).DefaultIfEmpty(0).Max();
            foreach (var overlay in adapted.SampleOverlays)
            {
                // Media's established profile resolves samples from the process working directory.
                // Wave resolves them relative to the entry file.
                var samplePath = Path.GetFullPath(overlay.RelativePath, target == "video" ? Environment.CurrentDirectory : Path.GetDirectoryName(path)!);
                if (target == "video" && !File.Exists(samplePath))
                {
                    analysis.Diagnostics.Add(Cli.Diagnostics.Warning($"Media export skips unavailable sample '{overlay.RelativePath}' under its existing asset policy.", path, FindSample(nodes, overlay.RelativePath), "SS2106"));
                    continue;
                }
                try
                {
                    var duration = ReadWavDuration(samplePath);
                    endSamples = Math.Max(endSamples, Math.Round(overlay.StartTimeSeconds * WavWriter.SampleRate) + Math.Max(1, Math.Round(duration * WavWriter.SampleRate)));
                }
                catch (Exception ex)
                {
                    ex.Data["SoundScript.Location"] = FindSample(nodes, overlay.RelativePath);
                    throw;
                }
            }
            endSamples = SoundScript.Wave.Effects.MasterEffectChain.MeasureOutputLength(checked((long)endSamples), effects, WavWriter.SampleRate);
            metadata.AudioDurationSeconds = endSamples / WavWriter.SampleRate;
            metadata.DurationBasis = "Wave PCM length including release, sample overlays, and effect tails; no synthesis";
            metadata.SupportedOutputTypes.Add("wav");
            if (!waveOnly) metadata.SupportedOutputTypes.Add("mid");
        }
        else
        {
            analysis.Midi = Interpreter.Interpret(loaded.Program, path);
            VocalInterpreter.Apply(loaded.Program, analysis.Midi);
            var interpreted = analysis.Midi;
            metadata.AudioBackend = "midi";
            metadata.Tempo = interpreted.Tempo;
            metadata.TrackCount = interpreted.Tracks.Count + interpreted.VocalTracks.Count;
            metadata.NoteCount = interpreted.Tracks.Sum(t => t.Notes.Count) + interpreted.VocalTracks.Sum(t => t.Syllables.Count);
            metadata.EventCount = metadata.NoteCount + interpreted.Tracks.Sum(t => t.ProgramChanges.Count);
            var ends = interpreted.Tracks.SelectMany(t => t.Notes).Select(n => interpreted.TempoMap.BeatsToMilliseconds(0, n.StartBeat) / 1000 + n.DurationMs / 1000)
                .Concat(interpreted.VocalTracks.SelectMany(t => t.Syllables).Select(n => interpreted.TempoMap.BeatsToMilliseconds(0, n.StartBeat) / 1000 + n.DurationMs / 1000));
            metadata.AudioDurationSeconds = ends.DefaultIfEmpty(0).Max();
            foreach (var warning in interpreted.Warnings)
                analysis.Diagnostics.Add(Cli.Diagnostics.Warning(warning, path, interpreted.SourceWarnings.FirstOrDefault(w => w.Message == warning).Location));
            metadata.SupportedOutputTypes.Add("mid");
            try { _ = AstToNoteEventAdapter.Adapt(loaded.Program); metadata.SupportedOutputTypes.Add("wav"); }
            catch (NotSupportedException) { /* A valid MIDI program can exceed Wave's existing capabilities. */ }
        }
        metadata.DurationSeconds = Math.Max(metadata.AudioDurationSeconds ?? 0, metadata.VisualDurationSeconds ?? 0);
        if (visual)
        {
            metadata.SupportedOutputTypes.Add("webm");
            if (metadata.AudioDurationSeconds > 0 && metadata.VisualDurationSeconds > metadata.AudioDurationSeconds)
            {
                var node = nodes.OfType<VisualNode>().LastOrDefault();
                analysis.Diagnostics.Add(Cli.Diagnostics.Warning("Visual extends beyond audio duration; video export pads audio with silence.", path, node is null ? null : SourceLocation.For(node), "SS2104"));
            }
            else if (metadata.AudioDurationSeconds > metadata.VisualDurationSeconds)
                analysis.Diagnostics.Add(Cli.Diagnostics.Warning("Audio extends beyond visual duration; video export truncates audio under the existing timing policy.", path, SourceLocation.For(loaded.Program.Statements.Last()), "SS2105"));
        }
        return analysis;
    }

    public static IEnumerable<AstNode> Children(AstNode node) => node switch
    {
        TrackNode n => n.Body, VoiceNode n => n.Body, MelodyNode n => n.Body,
        SequenceNode n => n.Body, BlockNode n => n.Body, LoopNode n => n.Body, PhraseNode n => n.Body,
        _ => []
    };
    public static IEnumerable<AstNode> Walk(IEnumerable<AstNode> nodes)
    {
        foreach (var node in nodes) { yield return node; foreach (var child in Walk(Children(node))) yield return child; }
    }

    private static SourceLocation? FindSample(IEnumerable<AstNode> nodes, string path) => nodes
        .Where(n => n is SampleNode sample && sample.Path == path || n is SpeakNode speak && speak.SamplePath == path)
        .Select(SourceLocation.For).FirstOrDefault();

    // Guard the existing adapters against recursive expansion; no alternate evaluator.
    private static void CheckRecursion(ProgramNode program)
    {
        var definitions = program.Statements.Where(n => n is BlockNode or SequenceNode).ToDictionary(
            n => n is BlockNode b ? b.Name : ((SequenceNode)n).Name, n => n, StringComparer.OrdinalIgnoreCase);
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Visit(string name, AstNode caller)
        {
            if (done.Contains(name) || !definitions.TryGetValue(name, out var definition)) return;
            if (!active.Add(name))
            {
                var ex = new InvalidOperationException($"Recursive block call detected: '{name}'.");
                SourceLocation.Attach(ex, caller);
                throw ex;
            }
            foreach (var play in Walk(Children(definition)).OfType<PlayNode>()) Visit(play.SequenceName, play);
            active.Remove(name); done.Add(name);
        }
        foreach (var play in Walk(program.Statements.Where(n => n is not (BlockNode or SequenceNode))).OfType<PlayNode>()) Visit(play.SequenceName, play);
    }

    public static double ReadWavDuration(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        string Id() => System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (Id() != "RIFF") throw new InvalidDataException($"Not a RIFF WAV: {path}");
        var size = reader.ReadUInt32();
        if (Id() != "WAVE" || size + 8L > stream.Length) throw new InvalidDataException($"Invalid WAV header: {path}");
        uint rate = 0, bytes = 0; ushort alignment = 0;
        bool data = false;
        while (stream.Position + 8 <= stream.Length)
        {
            var id = Id(); var length = reader.ReadUInt32(); var next = stream.Position + length + (length % 2);
            if (stream.Position + length > stream.Length) throw new InvalidDataException($"Truncated WAV: {path}");
            if (id == "fmt ")
            {
                if (length < 16) throw new InvalidDataException("Invalid WAV format chunk.");
                var format = reader.ReadUInt16(); var channels = reader.ReadUInt16(); rate = reader.ReadUInt32();
                _ = reader.ReadUInt32(); alignment = reader.ReadUInt16(); var bits = reader.ReadUInt16();
                if (format != 1 || channels is not (1 or 2) || bits != 16 || alignment != channels * 2 || rate == 0)
                    throw new InvalidDataException("Only mono/stereo 16-bit PCM WAV with a valid sample rate is supported.");
            }
            if (id == "data") { bytes = length; data = true; }
            stream.Position = next;
        }
        if (!data || rate == 0 || alignment == 0 || bytes % alignment != 0) throw new InvalidDataException("Missing or invalid WAV data/format chunk.");
        return bytes / (double)alignment / rate;
    }

    public static int ValidateCss(string path)
    {
        var source = File.ReadAllText(path);
        var clean = Regex.Replace(source, @"//[^\r\n]*", m => new string(' ', m.Length));
        string? selector = null;
        int start = 0, count = 0;
        void Fail(Exception ex, int offset)
        {
            int line = 1 + source[..offset].Count(c => c == '\n');
            int column = offset - source.LastIndexOf('\n', Math.Max(0, offset - 1));
            ex.Data["SoundScript.Location"] = new SourceLocation(path, line, column);
            throw ex;
        }
        for (int i = 0; i < clean.Length; i++)
        {
            if (clean[i] == '@' && selector is null)
            { while (i < clean.Length && clean[i] != '\n') i++; start = i + 1; continue; }
            if (clean[i] == '{')
            {
                if (selector is not null) Fail(new FormatException("Nested SoundCSS block."), i);
                selector = clean[start..i].Trim();
                if (selector.Length == 0) Fail(new FormatException("Missing SoundCSS selector."), i);
                start = i + 1;
            }
            else if (clean[i] is ';' or '}' && selector is not null)
            {
                var declaration = clean[start..i].Trim();
                if (declaration.Length > 0)
                {
                    var rule = selector + " { " + declaration + "; }";
                    try { SoundCSSParser.ParseOverrides(rule); SoundCSSParser.ParsePronunciations(rule); }
                    catch (Exception ex) { Fail(ex, start + clean[start..i].TakeWhile(char.IsWhiteSpace).Count()); }
                }
                if (clean[i] == '}') { selector = null; count++; }
                start = i + 1;
            }
            else if (clean[i] == '}') Fail(new FormatException("Unexpected closing SoundCSS brace."), i);
        }
        if (selector is not null) Fail(new FormatException("Unterminated SoundCSS block."), Math.Min(start, source.Length));
        if (!string.IsNullOrWhiteSpace(clean[start..])) Fail(new FormatException("Expected SoundCSS selector block."), start);
        SoundCSSParser.ParseOverrides(source); SoundCSSParser.ParsePronunciations(source); SoundCSSParser.ParsePhonemeSequence(source);
        return count;
    }
}

using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Media;
using SoundScript.Parser;
using SoundScript.Visual;
using SoundScript.Wave;

namespace SoundScript;

/// <summary>Metadata for a decimal runtime value. The bounds are the intersection of approved target policies.</summary>
public sealed record SoundScriptRuntimeParameter(string Name, decimal Default, decimal Minimum, decimal Maximum)
{
    /// <summary>Runtime values use decimal, including when supplied as host objects.</summary>
    public Type ValueType => typeof(decimal);
    internal void Validate(decimal value)
    {
        if (value < Minimum || value > Maximum)
            throw new ArgumentOutOfRangeException(Name, value,
                FormattableString.Invariant($"Parameter '{Name}' received {value}. Expected decimal {Minimum} through {Maximum}."));
    }
}

/// <summary>Actual structural compilation calls for one runtime program; rendering is counted separately by the host.</summary>
public sealed record RuntimeCompilationStatistics(int Tokenizations, int Parses, int TimelineCompilations);

/// <summary>Validated mutable parameter state over an owned, fixed media structure.</summary>
/// <remarks>State operations are synchronized. Use SetMany for an atomic multi-parameter update and Bind
/// to capture matching audio and visual state. No source is retained or parsed during updates.</remarks>
public sealed class SoundScriptRuntimeProgram
{
    private readonly object gate = new();
    private readonly RuntimeParseResult template;
    private readonly VisualTimeline timeline;
    private readonly WaveRenderOptions options;
    private readonly Dictionary<string, SoundScriptRuntimeParameter> definitions;
    private readonly Dictionary<string, decimal> values;
    private SoundScriptRuntimeSnapshot? current;
    private long revision;
    private int tokenizationCount, parseCount, timelineCompilationCount;

    internal SoundScriptRuntimeProgram(string source, WaveRenderOptions options)
    {
        this.options = options;
        var tokens = Tokenize(source);
        template = Parse(tokens);
        timeline = CompileTimeline(template.Program);
        Parameters = Array.AsReadOnly(template.Parameters.Select(p => new SoundScriptRuntimeParameter(p.Name, p.Default, p.Minimum, p.Maximum)).ToArray());
        definitions = Parameters.ToDictionary(p => p.Name, StringComparer.Ordinal);
        values = Parameters.ToDictionary(p => p.Name, p => p.Default, StringComparer.Ordinal);
    }

    private SoundScriptRuntimeProgram(SoundScriptRuntimeProgram compiled)
    {
        template = compiled.template; timeline = compiled.timeline; options = compiled.options;
        Parameters = compiled.Parameters; definitions = compiled.definitions;
        values = Parameters.ToDictionary(p => p.Name, p => p.Default, StringComparer.Ordinal);
        tokenizationCount = compiled.tokenizationCount; parseCount = compiled.parseCount;
        timelineCompilationCount = compiled.timelineCompilationCount;
    }

    /// <summary>Create independent default parameter state over the same compiled structure, without parsing.</summary>
    /// <remarks>The new instance starts at revision zero. Neither current values nor snapshots are shared.
    /// Instances own managed memory only and do not require disposal.</remarks>
    public SoundScriptRuntimeProgram CreateInstance() => new(this);

    /// <summary>Read-only decimal parameter metadata in declaration order.</summary>
    public IReadOnlyList<SoundScriptRuntimeParameter> Parameters { get; }
    /// <summary>Structural compile counts for the owned or shared compiled structure.</summary>
    public RuntimeCompilationStatistics Statistics => new(tokenizationCount, parseCount, timelineCompilationCount);
    /// <summary>The state revision; no-op or rejected updates do not advance it.</summary>
    public long Revision { get { lock (gate) return revision; } }
    /// <summary>Read the current value of a declared parameter.</summary>
    public decimal Get(string name) { lock (gate) { _ = Definition(name); return values[name]; } }

    /// <summary>Validate and set one decimal value without source parsing.</summary>
    public void Set(string name, decimal value)
    {
        lock (gate)
        {
            Definition(name).Validate(value);
            if (values[name] == value) return;
            values[name] = value; revision++; current = null;
        }
    }

    /// <summary>Accept external data only when it is decimal; strings are never evaluated or coerced.</summary>
    public void Set(string name, object? value)
    {
        _ = Definition(name);
        if (value is not decimal number)
            throw new ArgumentException($"Parameter '{name}' requires System.Decimal; received {value?.GetType().Name ?? "null"}. Supply a decimal value such as 0.5m.", nameof(value));
        Set(name, number);
    }

    /// <summary>Validate all values before atomically updating state. A rejected batch changes nothing.</summary>
    public void SetMany(IReadOnlyDictionary<string, decimal> updates)
    {
        ArgumentNullException.ThrowIfNull(updates);
        // Capture caller-owned data once. Callers must not mutate their collection during this capture.
        var captured = updates.ToArray();
        lock (gate)
        {
            foreach (var (name, value) in captured) Definition(name).Validate(value);
            if (captured.All(p => values[p.Key] == p.Value)) return;
            foreach (var (name, value) in captured) values[name] = value;
            revision++; current = null;
        }
    }

    /// <summary>Atomically restore all declared defaults.</summary>
    public void Reset() => SetMany(Parameters.ToDictionary(p => p.Name, p => p.Default, StringComparer.Ordinal));

    /// <summary>Capture a stable render snapshot. Existing snapshots survive later updates.</summary>
    public SoundScriptRuntimeSnapshot Bind()
    {
        lock (gate)
        {
            if (current is not null) return current;
            var program = template.Program;
            if (template.Gains.Count > 0)
            {
                program = new ProgramNode();
                program.Statements.AddRange(template.Program.Statements);
                foreach (var group in template.Gains.GroupBy(g => g.Statement))
                {
                    var original = (TrackNode)template.Program.Statements[group.Key];
                    var track = new TrackNode { Name = original.Name };
                    track.Body.AddRange(original.Body);
                    foreach (var slot in group)
                        track.Body[slot.Body] = new GainNode { Value = (double)values[slot.Parameter] };
                    program.Statements[group.Key] = track;
                }
            }
            return current = new(program, timeline, options, template.Visuals.ToDictionary(
                v => (v.Name, v.Property), v => values[v.Parameter]), revision);
        }
    }

    /// <summary>Render complete mono audio padded to the visual timeline from the current snapshot.</summary>
    public byte[] RenderAudio() => Bind().RenderAudio();
    /// <summary>Project the current state at the existing media clock's time.</summary>
    public TemporalVisualScene SceneAt(TimeSpan time) => Bind().SceneAt(time);
    private SoundScriptRuntimeParameter Definition(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Supply a non-empty, case-sensitive runtime parameter name from Parameters.", nameof(name));
        return definitions.TryGetValue(name, out var p) ? p : throw new ArgumentException(
            $"Unknown runtime parameter '{name}'. Names are case-sensitive. " +
            (Parameters.Count == 0 ? "This program declares no runtime parameters." : "Available: " + string.Join(", ", Parameters.Select(d => d.Name)) + "."), nameof(name));
    }

    /// <summary>Compact parameter-state inspection without exposing compiled internals or media payloads.</summary>
    public override string ToString()
    {
        lock (gate) return $"Runtime revision {revision}: " + (values.Count == 0 ? "no parameters" :
            string.Join(", ", values.Select(p => FormattableString.Invariant($"{p.Key}={p.Value}"))));
    }

    // Count actual calls at the sole structural-compilation boundary. Updates have
    // neither a source string nor a parser, and cannot invalidate this structure.
    private List<Token> Tokenize(string source)
    {
        tokenizationCount++;
        return new Tokenizer(source).Tokenize();
    }
    private RuntimeParseResult Parse(List<Token> tokens)
    {
        parseCount++;
        return new Parser.Parser(tokens).ParseRuntime();
    }
    private VisualTimeline CompileTimeline(ProgramNode program)
    {
        timelineCompilationCount++;
        return VisualInterpreter.Interpret(program);
    }
}

/// <summary>An owned, stable parameter snapshot rendered by the existing SoundScript engines.</summary>
/// <remarks>Independent renders may run concurrently. External assets must remain stable. MIDI receives
/// private notation copies because its existing interpreter records derived note/rest start times.</remarks>
public sealed class SoundScriptRuntimeSnapshot
{
    private readonly ProgramNode program;
    private readonly VisualTimeline timeline;
    private readonly WaveRenderOptions options;
    private readonly IReadOnlyDictionary<(string Name, string Property), decimal> visuals;
    internal SoundScriptRuntimeSnapshot(ProgramNode program, VisualTimeline timeline, WaveRenderOptions options,
        IReadOnlyDictionary<(string Name, string Property), decimal> visuals, long revision)
    {
        this.program = program; this.timeline = timeline; this.options = options; this.visuals = visuals; Revision = revision;
    }
    /// <summary>The captured state revision.</summary>
    public long Revision { get; }
    /// <summary>The fixed visual timeline duration; audio tails may extend beyond it.</summary>
    public TimeSpan VisualDuration => timeline.Duration;
    /// <summary>Render complete mono PCM WAV, padded to visual duration without truncating audio.</summary>
    public byte[] RenderAudio() => TemporalAudioRenderer.RenderCompleteWavBytes(program, timeline.Duration, options);
    /// <summary>Render ordinary mono WAV without visual padding.</summary>
    public byte[] RenderWave() => WaveRenderer.RenderToBytes(program, options);
    /// <summary>Render MIDI through the existing interpreter using private notation copies.</summary>
    public byte[] RenderMidi() => new SoundScriptCompilation(RuntimeMidiCopy.Copy(program), null, []).RenderMidi();
    /// <summary>Query the existing half-open visual timeline and project bound constant properties.</summary>
    public TemporalVisualScene SceneAt(TimeSpan time)
    {
        var state = timeline.StateAt(time);
        if (visuals.Count > 0)
            state = state with { Elements = Array.AsReadOnly(state.Elements.Select(e => e with
            {
                Properties = Array.AsReadOnly(e.Properties.Select(p => visuals.TryGetValue((e.Name, p.Property.ToLowerInvariant()), out var value)
                    ? p with { Value = value } : p).ToArray())
            }).ToArray()) };
        return TemporalVisualSceneBuilder.Build(state);
    }
}

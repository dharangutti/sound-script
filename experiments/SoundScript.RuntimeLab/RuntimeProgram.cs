using SoundScript.Core.Ast;
using SoundScript.Media;
using SoundScript.Visual;

namespace SoundScript.RuntimeLab;

/// <summary>Lab-only, single-owner runtime state over a private compiled template.</summary>
public sealed class RuntimeProgram
{
    private readonly CompiledTemplate template;
    private readonly Dictionary<string, int> indices;
    private readonly decimal[] values;
    private RuntimeFrame? current;

    private RuntimeProgram(CompiledTemplate template, CompilationCounters counters)
    {
        this.template = template;
        Counters = counters;
        Parameters = Array.AsReadOnly(template.Parameters);
        indices = template.Parameters.Select((p, i) => (p.Name, Index: i)).ToDictionary(p => p.Name, p => p.Index, StringComparer.Ordinal);
        values = template.Parameters.Select(p => p.Default).ToArray();
    }

    public static RuntimeProgram Compile(string source)
    {
        var counters = new CompilationCounters();
        return new(TemplateCompiler.Compile(source, counters), counters);
    }

    public IReadOnlyList<RuntimeParameter> Parameters { get; }
    public CompilationCounters Counters { get; }
    public long Revision { get; private set; }
    public long BindCount { get; private set; }

    public void Set(string name, decimal value)
    {
        if (!indices.TryGetValue(name, out var index)) throw new ArgumentException($"Unknown runtime parameter '{name}'.", nameof(name));
        template.Parameters[index].Validate(value);
        if (values[index] == value) return;
        values[index] = value;
        Revision++;
        current = null;
    }

    /// <summary>External host data is accepted only as decimal, never parsed as source or coerced from strings.</summary>
    public void Set(string name, object? value)
    {
        if (value is not decimal number) throw new ArgumentException("Runtime parameters require System.Decimal values.", nameof(value));
        Set(name, number);
    }

    /// <summary>Capture a stable audio/visual state. Later Set calls cannot change this frame.</summary>
    public RuntimeFrame Bind()
    {
        if (current is not null) return current;
        var snapshot = (decimal[])values.Clone();
        var program = template.Program;
        if (template.Gains.Length != 0)
        {
            // Copy on write. Never use record 'with' on list-owning AST nodes:
            // it would alias their mutable lists. Unchanged nodes remain private.
            program = new ProgramNode();
            program.Statements.AddRange(template.Program.Statements);
            foreach (var group in template.Gains.GroupBy(slot => slot.Statement))
            {
                var original = (TrackNode)template.Program.Statements[group.Key];
                var track = new TrackNode { Name = original.Name };
                track.Body.AddRange(original.Body);
                foreach (var slot in group) track.Body[slot.Body] = new GainNode { Value = (double)snapshot[slot.Parameter] };
                program.Statements[group.Key] = track;
            }
        }
        BindCount++;
        return current = new RuntimeFrame(program, template.Timeline, template.Visuals, snapshot, Revision);
    }

    public byte[] RenderAudio() => Bind().RenderAudio();
    public TemporalVisualScene SceneAt(TimeSpan time) => Bind().SceneAt(time);

    // Test-only access permits identity/structure assertions without making AST mutation a host API.
    internal ProgramNode TemplateForTests => template.Program;
}

/// <summary>A bound snapshot using exclusively the production timing and rendering paths.</summary>
public sealed class RuntimeFrame
{
    private readonly ProgramNode program;
    private readonly VisualTimeline timeline;
    private readonly Dictionary<(string Name, string Property), decimal> visualValues;

    internal RuntimeFrame(ProgramNode program, VisualTimeline timeline, VisualSlot[] slots, decimal[] values, long revision)
    {
        this.program = program;
        this.timeline = timeline;
        Revision = revision;
        visualValues = slots.ToDictionary(s => (s.Name, s.Property), s => values[s.Parameter]);
    }

    public long Revision { get; }
    public TimeSpan VisualDuration => timeline.Duration;

    // The existing renderer still performs its normal AST-to-note lowering on each
    // render. This is not a parser/compiler invocation or a cached/pre-rendered WAV.
    public byte[] RenderAudio() => TemporalAudioRenderer.RenderCompleteWavBytes(program, timeline.Duration);

    public TemporalVisualScene SceneAt(TimeSpan time)
    {
        var state = timeline.StateAt(time);
        if (visualValues.Count != 0)
            state = state with { Elements = Array.AsReadOnly(state.Elements.Select(element => element with
            {
                Properties = Array.AsReadOnly(element.Properties.Select(property =>
                    visualValues.TryGetValue((element.Name, property.Property.ToLowerInvariant()), out var value)
                        ? property with { Value = value } : property).ToArray())
            }).ToArray()) };
        return TemporalVisualSceneBuilder.Build(state);
    }

    internal ProgramNode ProgramForTests => program;
}

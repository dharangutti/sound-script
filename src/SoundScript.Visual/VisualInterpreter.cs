using SoundScript.Core.Ast;

namespace SoundScript.Visual;

/// <summary>
/// Compiles SoundScript's visual AST rail into immutable, absolute-time
/// intervals. It deliberately has no FPS, canvas, codec, or frame dependency:
/// those are concerns of a future renderer that samples <see cref="VisualTimeline"/>.
/// </summary>
public static class VisualInterpreter
{
    public static VisualTimeline Interpret(ProgramNode program)
    {
        ArgumentNullException.ThrowIfNull(program);

        var cursor = TimeSpan.Zero;
        var duration = TimeSpan.Zero;
        var sourceOrder = 0;
        var visuals = new List<ScheduledVisual>();
        var audioSyncPoints = new List<AudioSyncPoint>();

        foreach (var statement in program.Statements)
        {
            switch (statement)
            {
                case VisualNode visual:
                {
                    ValidateVisual(visual);

                    var start = visual.At ?? cursor;
                    var end = Add(start, visual.Duration, $"Visual '{visual.Name}' extends beyond the supported time range.");
                    var automations = CompileAutomations(visual);
                    visuals.Add(new ScheduledVisual(
                        visual.Name,
                        start,
                        end,
                        sourceOrder++,
                        automations));

                    if (!visual.At.HasValue)
                        cursor = end;

                    duration = Max(duration, end);
                    break;
                }

                case VisualWaitNode wait:
                    if (wait.Duration <= TimeSpan.Zero)
                        throw new InvalidOperationException("Visual wait duration must be greater than zero.");

                    cursor = Add(cursor, wait.Duration, "Visual narrative cursor extends beyond the supported time range.");
                    duration = Max(duration, cursor);
                    break;

                case AudioSyncNode:
                    audioSyncPoints.Add(new AudioSyncPoint(cursor));
                    duration = Max(duration, cursor);
                    break;
            }
        }

        var orderedVisuals = visuals
            .OrderBy(visual => visual.Start)
            .ThenBy(visual => visual.SourceOrder)
            .ToArray();

        return new VisualTimeline(
            Array.AsReadOnly(orderedVisuals),
            Array.AsReadOnly(audioSyncPoints.ToArray()),
            duration);
    }

    private static void ValidateVisual(VisualNode visual)
    {
        if (string.IsNullOrWhiteSpace(visual.Name))
            throw new InvalidOperationException("Visual name cannot be empty.");

        if (visual.Duration <= TimeSpan.Zero)
            throw new InvalidOperationException($"Visual '{visual.Name}' duration must be greater than zero.");

        if (visual.At is { } at && at < TimeSpan.Zero)
            throw new InvalidOperationException($"Visual '{visual.Name}' placement cannot be negative.");
    }

    private static IReadOnlyList<ScheduledVisualAutomation> CompileAutomations(VisualNode visual)
    {
        var properties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var automations = new List<ScheduledVisualAutomation>();

        foreach (var automation in visual.Automations)
        {
            if (string.IsNullOrWhiteSpace(automation.Property))
                throw new InvalidOperationException($"Visual '{visual.Name}' has an animation without a property name.");

            if (!properties.Add(automation.Property))
            {
                throw new InvalidOperationException(
                    $"Visual '{visual.Name}' animates '{automation.Property}' more than once.");
            }

            if (automation.Duration <= TimeSpan.Zero)
            {
                throw new InvalidOperationException(
                    $"Animation '{automation.Property}' on visual '{visual.Name}' must last longer than zero.");
            }

            if (automation.Duration > visual.Duration)
            {
                throw new InvalidOperationException(
                    $"Animation '{automation.Property}' lasts longer than visual '{visual.Name}'.");
            }

            automations.Add(new ScheduledVisualAutomation(
                automation.Property,
                automation.From,
                automation.To,
                automation.Duration));
        }

        return Array.AsReadOnly(automations
            .OrderBy(automation => automation.Property, StringComparer.Ordinal)
            .ToArray());
    }

    private static TimeSpan Add(TimeSpan left, TimeSpan right, string error)
    {
        try
        {
            return checked(left + right);
        }
        catch (OverflowException)
        {
            throw new InvalidOperationException(error);
        }
    }

    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left >= right ? left : right;
}

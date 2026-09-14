using System.Text.RegularExpressions;
using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Parser;
using SoundScript.Midi;
using SoundScript.Visual;
using SoundScript.Wave.Adapter;

namespace SoundScript.Playground;

public sealed record AuthoringDiagnostic(string Severity, string Message, int? Line = null, int? Column = null);
public sealed record AuthoringTrack(string Name, double DurationSeconds, int Events);
public sealed record AuthoringReport(IReadOnlyList<AuthoringDiagnostic> Diagnostics, IReadOnlyList<AuthoringTrack> Tracks,
    double AudioDuration, double VisualDuration, int VisualCount, string AudioBasis)
{
    public bool HasErrors => Diagnostics.Any(d => d.Severity == "error");
    public double TotalDuration => Math.Max(AudioDuration, VisualDuration);
}

/// <summary>Read-only authoring checks. No rendering, correction, or runtime mutation.</summary>
public static class AuthoringAnalysis
{
    public static AuthoringReport Analyze(string source, bool wave = false)
    {
        var diagnostics = new List<AuthoringDiagnostic>();
        var tracks = new List<AuthoringTrack>();
        var visualDuration = 0d;
        var visualCount = 0;
        var basis = wave ? "Scheduled Wave events (excludes effect tails and external samples)" : "Scheduled MIDI notes (excludes release tails)";
        try
        {
            var tokens = new Tokenizer(source).Tokenize();
            var program = new Parser.Parser(tokens).Parse();
            if (program.Statements.OfType<ImportNode>().Any())
                throw new InvalidOperationException("Browser validation cannot resolve imports. Use the CLI with the script's base directory.");
            void Warn(string message, string? name = null)
            {
                var token = name is null ? (Token?)null : tokens.FirstOrDefault(t => t.Value == name);
                diagnostics.Add(new("warning", message, token?.Line, token?.Column));
            }
            foreach (var group in program.Statements.OfType<TrackNode>().GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                Warn($"Duplicate track identifier '{group.Key}'; playback may merge source sections.", group.Key);
            var timeline = VisualInterpreter.Interpret(program);
            visualDuration = timeline.Duration.TotalSeconds;
            visualCount = timeline.Visuals.Count;
            foreach (var group in timeline.Visuals.GroupBy(v => v.Name).Where(g => g.Count() > 1))
                Warn($"Duplicate visual name '{group.Key}'; source selection is ambiguous.", group.Key);
            foreach (var visual in timeline.Visuals)
            {
                foreach (var a in visual.Automations)
                {
                    if (a.From == 0 && a.To == 0 && a.Property.ToLowerInvariant() is "width" or "height" or "radius" or "size" or "opacity")
                        Warn($"Visual '{visual.Name}' has zero {a.Property} throughout its lifetime.", visual.Name);
                }
                if (visual.Presentation is { Shape: "text", Fill: "none" })
                    Warn($"Text visual '{visual.Name}' has no fill and is invisible.", visual.Name);
            }
            if (visualCount > 200) Warn($"{visualCount} visual objects may make preview/export expensive.");
            if (wave)
            {
                var adapted = AstToNoteEventAdapter.Adapt(program);
                tracks.AddRange(adapted.Tracks.Select(t => new AuthoringTrack(t.Key,
                    t.Value.Select(n => n.StartTimeSeconds + n.DurationSeconds).DefaultIfEmpty().Max(), t.Value.Count)));
                if (adapted.SampleOverlays.Count > 0) Warn("External sample duration and availability require export-time checks; event duration is a lower bound.");
            }
            else
            {
                var interpreted = Interpreter.Interpret(program);
                tracks.AddRange(interpreted.Tracks.Select(t => new AuthoringTrack(t.Name,
                    t.Notes.Select(n => interpreted.TempoMap.BeatsToMilliseconds(0, n.StartBeat + n.DurationBeats) / 1000).DefaultIfEmpty().Max(), t.Notes.Count)));
                foreach (var message in interpreted.Warnings) Warn(message);
                if (interpreted.VocalTracks.Count > 0) Warn("Vocal tracks need their existing vocal export path; MIDI note statistics omit vocals.");
            }
            foreach (var track in tracks.Where(t => t.Events == 0)) Warn($"Track '{track.Name}' has no sounding events.", track.Name);
            var audio = tracks.Select(t => t.DurationSeconds).DefaultIfEmpty().Max();
            if (audio > 0 && visualCount > 0 && Math.Abs(audio - visualDuration) > 0.05)
                Warn($"Scheduled audio is {audio:0.###}s; visual timeline is {visualDuration:0.###}s. The existing media renderer fits PCM to the visual timeline; review possible truncation or silence.");
        }
        catch (Exception ex)
        {
            var match = Regex.Match(ex.Message, @"line (\d+), column (\d+)", RegexOptions.IgnoreCase);
            diagnostics.Add(new("error", ex.Message, match.Success ? int.Parse(match.Groups[1].Value) : null,
                match.Success ? int.Parse(match.Groups[2].Value) : null));
        }
        return new(diagnostics, tracks, tracks.Select(t => t.DurationSeconds).DefaultIfEmpty().Max(), visualDuration, visualCount, basis);
    }
}

using System.Text.RegularExpressions;

namespace IndustrialMonitoring;

public enum EquipmentStatus { Healthy, Warning, Critical }

public sealed record MonitoringScenario(
    string EquipmentId, double Temperature, double Vibration, double Load, EquipmentStatus Status)
{
    public void Validate()
    {
        // Keep generated source text and output names independent of untrusted path/syntax characters.
        if (EquipmentId is null || !Regex.IsMatch(EquipmentId, @"\A[A-Za-z0-9 _-]{1,40}\z"))
            throw new ArgumentException("EquipmentId must contain 1–40 letters, digits, spaces, underscores or hyphens.");
        if (!double.IsFinite(Temperature) || Temperature is < 0 or > 120 ||
            !double.IsFinite(Vibration) || Vibration is < 0 or > 1 ||
            !double.IsFinite(Load) || Load is < 0 or > 100 || !Enum.IsDefined(Status))
            throw new ArgumentException("Expected temperature 0–120, vibration 0–1, load 0–100 and a known status.");
    }
}

public sealed record MonitoringSources(string Music, string Wave, string Visual, int Tempo, int NoteCount);

public static class MonitoringSourceBuilder
{
    public static MonitoringSources Build(MonitoringScenario scenario)
    {
        scenario.Validate();
        int severity = (int)scenario.Status;
        int tempo = 80 + (int)Math.Round(scenario.Load / 5) + severity * 20;
        string pitch = scenario.Temperature >= 90 ? "C6" : scenario.Temperature >= 80 ? "G5" : "C5";
        string dynamic = new[] { "mp", "mf", "ff" }[severity];
        string instrument = new[] { "flute", "piano", "violin" }[severity];
        int count = 2 << severity;
        string duration = new[] { "h", "q", "e" }[severity];
        string notes = string.Join(" ", Enumerable.Repeat($"staccato {pitch} {duration}", count));
        string music = $"perform expressive\ntempo {tempo}\ntrack cue {{ instrument {instrument} {dynamic} {notes} }}\n";
        string wave = music + FormattableString.Invariant($"effect delay time=0.12 feedback=0.2 mix={0.05 + scenario.Vibration * 0.25:0.###}\n");
        string color = new[] { "#16a34a", "#d97706", "#dc2626" }[severity];
        string visual = wave + FormattableString.Invariant($$"""
            sync audio
            visual "background" for 4s at 0s {
                shape rectangle
                fill "#0f172a"
                set x 640
                set y 360
                set width 1280
                set height 720
            }
            visual "equipment" for 4s at 0s {
                shape text
                fill "#ffffff"
                text "{{scenario.EquipmentId}} - {{scenario.Status}}"
                fontSize 48
                set x 640
                set y 130
                set width 1160
                set height 90
            }
            visual "telemetry" for 4s at 0s {
                shape text
                fill "#e2e8f0"
                text "Temp {{scenario.Temperature:0.#}} C, Vibration {{scenario.Vibration:0.##}}, Load {{scenario.Load:0.#}}%"
                fontSize 32
                set x 640
                set y 250
                set width 1160
                set height 80
            }
            visual "status-cue" for 4s at 0s {
                shape circle
                fill "{{color}}"
                set x 640
                set y 455
                set width {{100 + severity * 80}}
                set height {{100 + severity * 80}}
                animate opacity 0.3 -> 1 over {{3 - severity}}s
            }
            visual "caption" for 4s at 0s {
                shape text
                fill "#94a3b8"
                text "MONITORING SONIFICATION DEMO"
                fontSize 26
                set x 640
                set y 650
                set width 1100
                set height 50
            }
            """);
        return new(music, wave, visual, tempo, count);
    }
}

using System.Globalization;

namespace ProgrammableMedia;

public enum EquipmentStatus { Healthy, Warning, Critical }

// Application data becomes source through a small application-owned builder.
public sealed record MonitoringScenario(EquipmentStatus Status)
{
    public string BuildSource()
    {
        var (tempo, pitch, color, size) = Status switch
        {
            EquipmentStatus.Healthy => (90, "C4", "#16a34a", 100),
            EquipmentStatus.Warning => (120, "E4", "#d97706", 160),
            EquipmentStatus.Critical => (160, "G5", "#dc2626", 220),
            _ => throw new ArgumentOutOfRangeException(nameof(Status))
        };
        return FormattableString.Invariant($$"""
            tempo {{tempo}}
            track alarm { {{pitch}} q {{pitch}} q {{pitch}} q {{pitch}} q }
            sync audio
            visual "status" for 4s {
                shape circle
                fill "{{color}}"
                set width {{size}}
                set height {{size}}
                set y 360
                animate x 200 -> 1080 over 4s
            }
            visual "label" for 4s at 0s {
                shape text
                text "{{Status}}"
                fill "{{color}}"
                set x 640
                set y 120
            }
            """);
    }
}

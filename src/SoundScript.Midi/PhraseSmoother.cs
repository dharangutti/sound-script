using SoundScript.Core.Notation;

namespace SoundScript.Midi;

/// <summary>Smooths melodic transitions across phrase and sequence boundaries.</summary>
internal static class PhraseSmoother
{
    internal static (NotatedNote Note, bool Adjusted) Apply(int? previousPhraseMidi, NotatedNote current) =>
        OctaveSmoother.Apply(previousPhraseMidi, current);
}

namespace SoundScript.Midi;

/// <summary>Orchestration helpers applied after chord voicing and before harmonic spacing.</summary>
internal static class ChordOrchestration
{
    internal static (int[] Notes, bool Adjusted) Apply(IReadOnlyList<int> midiNumbers, OrchestrationSettings settings)
    {
        if (!settings.HasAny || midiNumbers.Count == 0)
            return (midiNumbers.ToArray(), false);

        var notes = midiNumbers.OrderBy(note => note).ToList();
        var adjusted = false;

        if (settings.ReinforceBass)
        {
            var bass = notes[0] - 12;
            if (!notes.Contains(bass))
            {
                notes.Insert(0, bass);
                adjusted = true;
            }
        }

        if (settings.BrightenTop)
        {
            var top = notes[^1] + 12;
            if (!notes.Contains(top))
            {
                notes.Add(top);
                adjusted = true;
            }
        }

        if (settings.DoubleOctave)
        {
            var originals = midiNumbers.OrderBy(note => note).ToArray();
            foreach (var note in originals)
            {
                var doubled = note + 12;
                if (!notes.Contains(doubled))
                {
                    notes.Add(doubled);
                    adjusted = true;
                }
            }
        }

        notes.Sort();
        return (notes.ToArray(), adjusted);
    }
}

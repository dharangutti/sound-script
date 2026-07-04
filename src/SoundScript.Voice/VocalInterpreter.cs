using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;
using SoundScript.Voice.Phonetics;

namespace SoundScript.Voice;

/// <summary>
/// Parallel interpreter for <see cref="VoiceNode"/> blocks.
///
/// Runs after the musical <c>Interpreter</c> and never touches instrumental
/// tracks: it reads the already-built tempo map for millisecond timing and
/// appends <see cref="InterpretedVocalTrack"/>s to the program. Voice timelines
/// start at beat 0, parallel to instrument tracks.
/// </summary>
public static class VocalInterpreter
{
    public static void Apply(ProgramNode program, InterpretedProgram interpreted)
    {
        foreach (var statement in program.Statements)
        {
            if (statement is VoiceNode voice)
                interpreted.VocalTracks.Add(InterpretVoice(voice, interpreted));
        }
    }

    private static InterpretedVocalTrack InterpretVoice(VoiceNode voice, InterpretedProgram interpreted)
    {
        var track = new InterpretedVocalTrack { Name = voice.Name };
        var currentBeat = 0.0;
        var currentVelocity = 72;
        DynamicLevel? currentDynamic = null;

        foreach (var statement in voice.Body)
        {
            switch (statement)
            {
                case VocalTimbreNode timbre:
                    track.ProgramNumber = timbre.ProgramNumber;
                    break;
                case DynamicNode dynamic:
                    currentDynamic = dynamic.Level;
                    break;
                case VelocityNode velocity:
                    currentVelocity = velocity.Velocity;
                    break;
                case RestNode rest:
                    currentBeat = RoundBeat(currentBeat + rest.Rest.DurationBeats);
                    break;
                case SingNode sing:
                    currentBeat = EmitSing(track, sing, currentBeat, currentVelocity, currentDynamic, interpreted);
                    break;
            }
        }

        return track;
    }

    private static double EmitSing(
        InterpretedVocalTrack track,
        SingNode sing,
        double currentBeat,
        int currentVelocity,
        DynamicLevel? currentDynamic,
        InterpretedProgram interpreted)
    {
        var syllables = LyricAligner.ToSyllables(sing.Lyric);
        var slots = LyricAligner.Align(syllables, sing.Notes.Count, out var overflowed);

        if (overflowed)
        {
            AddWarning(interpreted,
                $"Lyric \"{sing.Lyric}\" has {syllables.Count} syllables for {sing.Notes.Count} notes — tail merged onto final note.");
        }

        for (var i = 0; i < sing.Notes.Count; i++)
        {
            var note = sing.Notes[i];
            var slot = slots[i];
            var durationBeats = note.Notation.DurationBeats;
            var durationMs = interpreted.TempoMap.BeatsToMilliseconds(currentBeat, durationBeats);
            var velocity = note.Velocity ?? currentDynamic?.ToVelocity() ?? currentVelocity;

            track.Syllables.Add(new TimedSyllable(
                slot?.Text ?? string.Empty,
                slot?.IsWordEnd ?? false,
                note.Notation.ToMidiNumber(),
                currentBeat,
                durationBeats,
                durationMs,
                Math.Clamp(velocity, 1, 127),
                IsMelisma: slot is null));

            currentBeat = RoundBeat(currentBeat + durationBeats);
        }

        return currentBeat;
    }

    private static void AddWarning(InterpretedProgram interpreted, string warning)
    {
        if (!interpreted.Warnings.Contains(warning))
            interpreted.Warnings.Add(warning);
    }

    private static double RoundBeat(double beats) =>
        Math.Round(beats, 9, MidpointRounding.AwayFromZero);
}

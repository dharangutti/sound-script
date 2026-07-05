using SoundScript.Compose;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;

namespace SoundScript.Prosody;

/// <summary>
/// Builds a <see cref="NoteNode"/> from an explicit prosody-resolved pitch
/// (pitch class, accidental, octave) and a phoneme's <see cref="GestureKind"/>
/// (articulation/velocity) and duration. A small parallel to
/// <c>SoundScript.Compose.GestureBuilder.BuildNote</c> — that one takes its
/// pitch from the gesture itself and cannot express accidentals, since
/// <see cref="MusicalGesture"/> has no accidental field. Prosody pitch is
/// computed in semitones and can land on a black key, so notes are built
/// directly here instead.
/// </summary>
internal static class ProsodyNoteBuilder
{
    private const int SwellVelocity = 58;
    private const int FadeVelocity = 52;

    /// <summary>Builds one note carrying an explicit prosody pitch and a phoneme's gesture kind/duration.</summary>
    internal static NoteNode BuildNote(
        GestureKind kind,
        PitchClass pitchClass,
        AccidentalType accidental,
        int octave,
        NoteDuration duration)
    {
        var notation = new NotatedNote
        {
            PitchClass = pitchClass,
            Accidental = accidental,
            Octave = octave,
            StandardDuration = duration,
            DurationBeats = duration.ToBeats(),
            Articulation = ToArticulation(kind)
        };

        return new NoteNode
        {
            Notation = notation,
            Velocity = ToVelocity(kind)
        };
    }

    private static ArticulationType? ToArticulation(GestureKind kind) => kind switch
    {
        GestureKind.Staccato => ArticulationType.Staccato,
        GestureKind.Legato => ArticulationType.Legato,
        GestureKind.Accent => ArticulationType.Accent,
        _ => null
    };

    private static int? ToVelocity(GestureKind kind) => kind switch
    {
        GestureKind.Swell => SwellVelocity,
        GestureKind.Fade => FadeVelocity,
        _ => null
    };
}

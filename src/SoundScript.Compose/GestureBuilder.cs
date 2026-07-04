using SoundScript.Core.Ast;
using SoundScript.Core.Notation;

namespace SoundScript.Compose;

/// <summary>
/// Turns a <see cref="MusicalGesture"/> into existing SoundScript AST nodes.
/// No new grammar and no new node types: staccato/legato/accent become the
/// standard per-note articulations, while swell and fade are expressed through
/// the existing phrase envelope (crescendo/decrescendo) plus a characteristic
/// note velocity, since a single MIDI note cannot change velocity mid-sound.
/// </summary>
public static class GestureBuilder
{
    // Fixed velocities that keep swell entrances soft and fades airy relative
    // to the engine's default velocity of 64.
    private const int SwellVelocity = 58;
    private const int FadeVelocity = 52;

    /// <summary>Builds the note a gesture contributes to the phrase body.</summary>
    public static NoteNode BuildNote(MusicalGesture gesture)
    {
        var notation = new NotatedNote
        {
            PitchClass = gesture.Pitch,
            Octave = gesture.Octave,
            StandardDuration = gesture.Duration,
            DurationBeats = gesture.Duration.ToBeats(),
            Articulation = ToArticulation(gesture.Kind)
        };

        return new NoteNode
        {
            Notation = notation,
            Velocity = ToVelocity(gesture.Kind)
        };
    }

    /// <summary>
    /// Builds the phrase-level envelope a gesture implies, or null when the
    /// gesture is fully described by its per-note articulation.
    /// </summary>
    public static PhraseEnvelopeNode? BuildEnvelope(GestureKind kind) => kind switch
    {
        GestureKind.Swell => new PhraseEnvelopeNode { Envelope = PhraseEnvelopeType.Crescendo },
        GestureKind.Fade => new PhraseEnvelopeNode { Envelope = PhraseEnvelopeType.Decrescendo },
        _ => null
    };

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

using SoundScript.Core.Ast;
using SoundScript.Core.Notation;

namespace SoundScript.Midi;

internal sealed class PhraseScope
{
    public DynamicLevel? Dynamic { get; set; }
    public PhraseCurveType Curve { get; set; } = PhraseCurveType.Balanced;
    public PhraseTransitionMode Transition { get; set; } = PhraseTransitionMode.Smooth;
    public PhraseEnvelopeType Envelope { get; set; } = PhraseEnvelopeType.None;
    public ArticulationType? DefaultArticulation { get; set; }
    public double? SwingRatio { get; set; }
    public double PushBeats { get; set; }
    public double PullBeats { get; set; }
    public int NoteIndex { get; set; }
    public int NoteCount { get; init; }
}

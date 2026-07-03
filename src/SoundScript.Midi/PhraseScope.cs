using SoundScript.Core.Ast;
using SoundScript.Core.Notation;

namespace SoundScript.Midi;

internal sealed class PhraseScope
{
    public DynamicLevel? Dynamic { get; set; }
    public PhraseCurveType Curve { get; set; } = PhraseCurveType.Balanced;
    public PhraseTransitionMode Transition { get; set; } = PhraseTransitionMode.Smooth;
    public int NoteIndex { get; set; }
    public int NoteCount { get; init; }
}

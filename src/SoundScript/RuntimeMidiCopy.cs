using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;

namespace SoundScript;

// The MIDI interpreter writes NotatedNote/NotatedRest.StartTime. Isolate just that
// legacy mutation at the adapter boundary, without changing renderer algorithms.
internal static class RuntimeMidiCopy
{
    internal static ProgramNode Copy(ProgramNode source)
    {
        var copy = new ProgramNode();
        copy.Statements.AddRange(source.Statements.Select(Clone));
        return copy;
    }
    private static AstNode Clone(AstNode node)
    {
        AstNode copy = node switch
        {
            NoteNode note => note with { Notation = note.Notation with { } },
            RestNode rest => rest with { Rest = new NotatedRest { StandardDuration = rest.Rest.StandardDuration,
                DurationBeats = rest.Rest.DurationBeats, StartTime = rest.Rest.StartTime } },
            TrackNode t => Fill(new TrackNode { Name = t.Name }, t.Body),
            VoiceNode t => Fill(new VoiceNode { Name = t.Name }, t.Body),
            SequenceNode t => Fill(new SequenceNode { Name = t.Name }, t.Body),
            BlockNode t => Fill(new BlockNode { Name = t.Name }, t.Body),
            MelodyNode t => Fill(new MelodyNode(), t.Body),
            PhraseNode t => Fill(new PhraseNode(), t.Body),
            LoopNode t => Fill(new LoopNode { Count = t.Count }, t.Body),
            SingNode s => CloneSing(s),
            _ => node
        };
        if (!ReferenceEquals(copy, node) && SourceLocation.For(node) is { } location) SourceLocation.Set(copy, location);
        return copy;
    }
    private static AstNode Fill(AstNode node, List<AstNode> source)
    {
        var body = node switch
        {
            TrackNode t => t.Body, VoiceNode t => t.Body, SequenceNode t => t.Body, BlockNode t => t.Body,
            MelodyNode t => t.Body, PhraseNode t => t.Body, LoopNode t => t.Body,
            _ => throw new InvalidOperationException()
        };
        body.AddRange(source.Select(Clone));
        return node;
    }
    private static SingNode CloneSing(SingNode source)
    {
        var copy = new SingNode { Lyric = source.Lyric };
        copy.Notes.AddRange(source.Notes.Select(n => (NoteNode)Clone(n)));
        return copy;
    }
}

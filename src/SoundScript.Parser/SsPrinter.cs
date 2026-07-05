using System.Globalization;
using System.Text;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;

namespace SoundScript.Parser;

/// <summary>
/// Serializes a pre-interpretation AST (<see cref="ProgramNode"/>) back into
/// human-formatted .ss DSL source text that the existing <see cref="Tokenizer"/>/
/// <see cref="SoundScript.Parser.Parser"/> pair can re-parse into an equivalent program.
///
/// Only handles the node shapes that <c>PhonemeComposer.BuildAst</c> and
/// <c>ProsodyComposer.BuildAst</c> can produce today: <see cref="TempoNode"/>,
/// <see cref="TrackNode"/>, <see cref="PhraseNode"/>, <see cref="PhraseEnvelopeNode"/>,
/// and <see cref="NoteNode"/>. Anything else — or any of these node types holding a
/// value with no textual DSL surface — throws <see cref="NotSupportedException"/>
/// rather than silently dropping data.
/// </summary>
public static class SsPrinter
{
    private const string Indent = "    ";

    public static string Print(ProgramNode program)
    {
        var sb = new StringBuilder();
        PrintStatements(program.Statements, sb, indentLevel: 0);
        return sb.ToString();
    }

    private static void PrintStatements(IReadOnlyList<AstNode> statements, StringBuilder sb, int indentLevel)
    {
        for (var i = 0; i < statements.Count; i++)
        {
            if (i > 0)
                sb.Append('\n');

            PrintStatement(statements[i], sb, indentLevel);
        }
    }

    private static void PrintStatement(AstNode node, StringBuilder sb, int indentLevel)
    {
        switch (node)
        {
            case TempoNode tempo:
                AppendLine(sb, indentLevel, $"tempo {tempo.Bpm}");
                break;
            case TrackNode track:
                PrintTrack(track, sb, indentLevel);
                break;
            case PhraseNode phrase:
                PrintPhrase(phrase, sb, indentLevel);
                break;
            case PhraseEnvelopeNode envelope:
                PrintEnvelope(envelope, sb, indentLevel);
                break;
            case NoteNode note:
                AppendLine(sb, indentLevel, FormatNote(note));
                break;
            default:
                throw new NotSupportedException(
                    $"SsPrinter cannot serialize AST node '{node.GetType().Name}' to .ss source. " +
                    "Only Tempo, Track, Phrase, PhraseEnvelope, and Note nodes are currently supported " +
                    "(the full surface produced by PhonemeComposer/ProsodyComposer). Extend SsPrinter " +
                    "before emitting a program containing this construct.");
        }
    }

    private static void PrintTrack(TrackNode track, StringBuilder sb, int indentLevel)
    {
        if (!IsValidBareIdentifier(track.Name))
            throw new NotSupportedException(
                $"SsPrinter cannot emit track name '{track.Name}': it is not a valid bare identifier " +
                "(the .ss grammar has no quoted-string form for track names).");

        AppendLine(sb, indentLevel, $"track {track.Name} {{");
        PrintBlockBody(track.Body, sb, indentLevel + 1, blankLineBetweenPhrases: true);
        AppendLine(sb, indentLevel, "}");
    }

    private static void PrintPhrase(PhraseNode phrase, StringBuilder sb, int indentLevel)
    {
        AppendLine(sb, indentLevel, "phrase {");
        PrintBlockBody(phrase.Body, sb, indentLevel + 1, blankLineBetweenPhrases: false);
        AppendLine(sb, indentLevel, "}");
    }

    private static void PrintBlockBody(IReadOnlyList<AstNode> body, StringBuilder sb, int indentLevel, bool blankLineBetweenPhrases)
    {
        for (var i = 0; i < body.Count; i++)
        {
            if (blankLineBetweenPhrases && i > 0 && body[i] is PhraseNode)
                sb.Append('\n');

            PrintStatement(body[i], sb, indentLevel);
        }
    }

    private static void PrintEnvelope(PhraseEnvelopeNode envelope, StringBuilder sb, int indentLevel)
    {
        var keyword = envelope.Envelope switch
        {
            PhraseEnvelopeType.Crescendo => "crescendo",
            PhraseEnvelopeType.Decrescendo => "decrescendo",
            PhraseEnvelopeType.None => throw new NotSupportedException(
                "SsPrinter cannot emit a PhraseEnvelopeNode with Envelope=None: the .ss grammar has no " +
                "textual form for an explicit 'no envelope' statement (the parser only expresses that by " +
                "omitting the node entirely). Remove the node from the AST instead of printing it."),
            _ => throw new NotSupportedException($"SsPrinter cannot emit unknown PhraseEnvelopeType '{envelope.Envelope}'.")
        };

        AppendLine(sb, indentLevel, keyword);
    }

    private static string FormatNote(NoteNode note)
    {
        var notation = note.Notation;

        if (notation.IsTied)
            throw new NotSupportedException(
                "SsPrinter cannot emit a tied NotatedNote: the parser collapses a tie ('~') into a single " +
                "NotatedNote with combined duration and no memory of the original two note tokens, so there " +
                "is no way to faithfully re-emit the original tie syntax.");

        if (notation.Octave < 0 || notation.Octave > 8)
            throw new NotSupportedException(
                $"SsPrinter cannot emit note octave {notation.Octave}: outside the parser's accepted range (0-8).");

        var sb = new StringBuilder();
        sb.Append(FormatPitch(notation.PitchClass));
        sb.Append(FormatAccidental(notation.Accidental));
        sb.Append(notation.Octave.ToString(CultureInfo.InvariantCulture));

        sb.Append(' ');
        sb.Append(FormatDuration(notation));

        if (notation.Articulation.HasValue)
        {
            sb.Append(' ');
            sb.Append(FormatArticulation(notation.Articulation.Value));
        }

        if (note.Velocity.HasValue)
        {
            sb.Append(" v");
            sb.Append(note.Velocity.Value.ToString(CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    private static string FormatPitch(PitchClass pitchClass) => pitchClass switch
    {
        PitchClass.C => "C",
        PitchClass.D => "D",
        PitchClass.E => "E",
        PitchClass.F => "F",
        PitchClass.G => "G",
        PitchClass.A => "A",
        PitchClass.B => "B",
        _ => throw new NotSupportedException($"SsPrinter cannot emit unknown PitchClass '{pitchClass}'.")
    };

    private static string FormatAccidental(AccidentalType accidental) => accidental switch
    {
        AccidentalType.None => "",
        AccidentalType.Sharp => "#",
        AccidentalType.Flat => "b",
        AccidentalType.Natural => "♮",
        _ => throw new NotSupportedException($"SsPrinter cannot emit unknown AccidentalType '{accidental}'.")
    };

    private static string FormatDuration(NotatedNote notation)
    {
        if (notation.StandardDuration.HasValue)
        {
            return notation.StandardDuration.Value switch
            {
                NoteDuration.Quarter => "q",
                NoteDuration.Half => "h",
                NoteDuration.Eighth => "e",
                NoteDuration.Whole => "w",
                _ => throw new NotSupportedException(
                    $"SsPrinter cannot emit unknown NoteDuration '{notation.StandardDuration.Value}'.")
            };
        }

        return ":" + notation.DurationBeats.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatArticulation(ArticulationType articulation) => articulation switch
    {
        ArticulationType.Staccato => "staccato",
        ArticulationType.Legato => "legato",
        ArticulationType.Accent => "accent",
        _ => throw new NotSupportedException($"SsPrinter cannot emit unknown ArticulationType '{articulation}'.")
    };

    private static bool IsValidBareIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name) || !char.IsLetter(name[0]))
            return false;

        for (var i = 1; i < name.Length; i++)
        {
            if (!char.IsLetterOrDigit(name[i]))
                return false;
        }

        return true;
    }

    private static void AppendLine(StringBuilder sb, int indentLevel, string text)
    {
        for (var i = 0; i < indentLevel; i++)
            sb.Append(Indent);

        sb.Append(text);
        sb.Append('\n');
    }
}

using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;

namespace SoundScript.Parser;

public sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _position;

    public Parser(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens;
    }

    public ProgramNode Parse()
    {
        var program = new ProgramNode();

        while (!Check(TokenType.EndOfFile))
        {
            program.Statements.Add(ParseTopLevelStatement());
        }

        return program;
    }

    private AstNode ParseTopLevelStatement()
    {
        if (Match(TokenType.Import))
            return ParseImportStatement();

        if (Match(TokenType.Melody))
            return ParseMelodyBlock();

        if (Match(TokenType.Track))
            return ParseTrackBlock();

        if (Match(TokenType.Sequence))
            return ParseSequenceBlock();

        if (Match(TokenType.Block))
            return ParseNamedBlock();

        if (Match(TokenType.Play))
            return ParsePlayStatement();

        if (Match(TokenType.Loop))
            return ParseLoopBlock();

        if (Match(TokenType.Tempo))
            return ParseTempoStatement();

        if (Match(TokenType.Bpm))
            return ParseBpmStatement();

        if (Match(TokenType.Time))
            return ParseTimeSignatureStatement();

        if (Match(TokenType.Instrument))
            return ParseInstrumentStatement();

        if (Match(TokenType.Velocity))
            return ParseVelocityStatement();

        if (Match(TokenType.Rest))
            return ParseRestStatement();

        if (Match(TokenType.Dynamic))
            return ParseDynamicStatement();

        if (Check(TokenType.Articulation))
        {
            var articulation = ParseOptionalPrefixArticulation();
            if (Check(TokenType.Note))
                return ParseNoteStatement(articulation);

            var articulationTarget = Peek();
            throw Invalid(articulationTarget, "Expected note after articulation.");
        }

        if (Check(TokenType.Note))
            return ParseNoteStatement();

        if (Check(TokenType.Chord))
            return ParseChordStatement();

        var unexpected = Peek();
        ThrowIfInvalidNoteAttempt(unexpected);
        throw Invalid(unexpected, $"Unexpected token '{unexpected.Value}'.");
    }

    private ImportNode ParseImportStatement()
    {
        var pathToken = Expect(TokenType.StringLiteral, "import path");
        return new ImportNode { Path = pathToken.Value };
    }

    private MelodyNode ParseMelodyBlock()
    {
        Expect(TokenType.LeftBrace, "{");
        var melody = new MelodyNode();

        while (!Check(TokenType.RightBrace) && !Check(TokenType.EndOfFile))
        {
            melody.Body.Add(ParseBodyStatement(allowLoop: false));
        }

        Expect(TokenType.RightBrace, "}");
        return melody;
    }

    private TrackNode ParseTrackBlock()
    {
        var name = ParseName("track name");
        Expect(TokenType.LeftBrace, "{");

        var track = new TrackNode { Name = name };

        while (!Check(TokenType.RightBrace) && !Check(TokenType.EndOfFile))
        {
            track.Body.Add(ParseBodyStatement(allowLoop: true));
        }

        Expect(TokenType.RightBrace, "}");
        return track;
    }

    private SequenceNode ParseSequenceBlock()
    {
        var name = ParseName("sequence name");
        Expect(TokenType.LeftBrace, "{");

        var sequence = new SequenceNode { Name = name };

        while (!Check(TokenType.RightBrace) && !Check(TokenType.EndOfFile))
        {
            sequence.Body.Add(ParseBodyStatement(allowLoop: true));
        }

        Expect(TokenType.RightBrace, "}");
        return sequence;
    }

    private BlockNode ParseNamedBlock()
    {
        var name = ParseName("block name");
        Expect(TokenType.LeftBrace, "{");

        var block = new BlockNode { Name = name };

        while (!Check(TokenType.RightBrace) && !Check(TokenType.EndOfFile))
        {
            block.Body.Add(ParseBlockBodyStatement());
        }

        Expect(TokenType.RightBrace, "}");
        return block;
    }

    private AstNode ParseBlockBodyStatement()
    {
        if (Match(TokenType.Play))
            return ParsePlayStatement();

        if (Match(TokenType.Bar))
            return new BarNode();

        if (Match(TokenType.Rest))
            return ParseRestStatement();

        if (Match(TokenType.Dynamic))
            return ParseDynamicStatement();

        if (Check(TokenType.Articulation))
        {
            var articulation = ParseOptionalPrefixArticulation();
            if (Check(TokenType.Note))
                return ParseNoteStatement(articulation);

            var articulationTarget = Peek();
            throw Invalid(articulationTarget, "Expected note after articulation.");
        }

        if (Check(TokenType.Note))
            return ParseNoteStatement();

        if (Check(TokenType.Chord))
            return ParseChordStatement();

        var unexpected = Peek();
        ThrowIfInvalidNoteAttempt(unexpected);
        throw Invalid(unexpected, $"Unexpected token '{unexpected.Value}'.");
    }

    private PlayNode ParsePlayStatement()
    {
        var name = ParseName("block or sequence name");
        return new PlayNode { SequenceName = name };
    }

    private LoopNode ParseLoopBlock()
    {
        var countToken = Expect(TokenType.Number, "loop count");
        if (!int.TryParse(countToken.Value, out var count) || count <= 0)
            throw Invalid(countToken, "Loop count must be a positive integer.");

        Expect(TokenType.LeftBrace, "{");

        var loop = new LoopNode { Count = count };

        while (!Check(TokenType.RightBrace) && !Check(TokenType.EndOfFile))
        {
            if (Check(TokenType.Loop))
                throw Invalid(Peek(), "Nested loops are not supported.");

            loop.Body.Add(ParseBodyStatement(allowLoop: false));
        }

        Expect(TokenType.RightBrace, "}");
        return loop;
    }

    private AstNode ParseBodyStatement(bool allowLoop)
    {
        if (allowLoop && Match(TokenType.Loop))
            return ParseLoopBlock();

        if (Match(TokenType.Bpm))
            return ParseBpmStatement();

        if (Match(TokenType.Tempo))
            return ParseTempoStatement();

        if (Match(TokenType.Time))
            return ParseTimeSignatureStatement();

        if (Match(TokenType.Instrument))
            return ParseInstrumentStatement();

        if (Match(TokenType.Gain))
            return ParseGainStatement();

        if (Match(TokenType.Humanize))
            return ParseHumanizeStatement();

        if (Match(TokenType.Velocity))
            return ParseVelocityStatement();

        if (Match(TokenType.Play))
            return ParsePlayStatement();

        if (Match(TokenType.Bar))
            return new BarNode();

        if (Match(TokenType.Rest))
            return ParseRestStatement();

        if (Match(TokenType.Dynamic))
            return ParseDynamicStatement();

        if (Check(TokenType.Articulation))
        {
            var articulation = ParseOptionalPrefixArticulation();
            if (Check(TokenType.Note))
                return ParseNoteStatement(articulation);

            var articulationTarget = Peek();
            throw Invalid(articulationTarget, "Expected note after articulation.");
        }

        if (Check(TokenType.Note))
            return ParseNoteStatement();

        if (Check(TokenType.Chord))
            return ParseChordStatement();

        var unexpected = Peek();
        ThrowIfInvalidNoteAttempt(unexpected);
        throw Invalid(unexpected, $"Unexpected token '{unexpected.Value}'.");
    }

    private GainNode ParseGainStatement()
    {
        var token = Expect(TokenType.Number, "gain value");
        return new GainNode { Value = ParseUnitInterval(token, "Gain") };
    }

    private HumanizeNode ParseHumanizeStatement()
    {
        var token = Expect(TokenType.Number, "humanize value");
        if (!double.TryParse(token.Value, out var value) || value < 0)
            throw Invalid(token, "Humanize must be a non-negative number.");

        return new HumanizeNode { Value = value };
    }

    private static double ParseUnitInterval(Token token, string label)
    {
        if (!double.TryParse(token.Value, out var value) || value < 0 || value > 1)
            throw Invalid(token, $"{label} must be between 0.0 and 1.0.");

        return value;
    }

    private TempoNode ParseTempoStatement()
    {
        var token = Expect(TokenType.Number, "tempo value");
        return new TempoNode { Bpm = ParsePositiveInt(token, "Tempo") };
    }

    private BpmNode ParseBpmStatement()
    {
        var token = Expect(TokenType.Number, "BPM value");
        return new BpmNode { Bpm = ParsePositiveInt(token, "BPM") };
    }

    private TimeSignatureNode ParseTimeSignatureStatement()
    {
        var numeratorToken = Expect(TokenType.Number, "time signature numerator");
        Expect(TokenType.Slash, "/");
        var denominatorToken = Expect(TokenType.Number, "time signature denominator");

        var numerator = ParsePositiveInt(numeratorToken, "Time signature numerator");
        var denominator = ParsePositiveInt(denominatorToken, "Time signature denominator");

        return new TimeSignatureNode
        {
            Numerator = numerator,
            Denominator = denominator
        };
    }

    private InstrumentNode ParseInstrumentStatement()
    {
        var nameToken = Expect(TokenType.Identifier, "instrument name");
        return new InstrumentNode
        {
            Name = nameToken.Value,
            ProgramNumber = InstrumentMap.Resolve(nameToken.Value)
        };
    }

    private VelocityNode ParseVelocityStatement()
    {
        var token = Expect(TokenType.Number, "velocity value");
        var velocity = ParsePositiveInt(token, "Velocity");
        if (velocity > 127)
            throw Invalid(token, "Velocity must be between 1 and 127.");

        return new VelocityNode { Velocity = velocity };
    }

    private AstNode ParseNoteStatement(ArticulationType? prefixArticulation = null)
    {
        var noteToken = Expect(TokenType.Note, "note");

        if (IsDominantSeventhAmbiguity(noteToken))
            return ParseDominantSeventhChord(noteToken);

        var note = BuildNoteNode(noteToken, prefixArticulation);

        while (Match(TokenType.Tie))
        {
            var tieToken = Previous();
            var tiedToken = Expect(TokenType.Note, "tied note");
            if (IsDominantSeventhAmbiguity(tiedToken))
                throw Invalid(tiedToken, "Invalid tie: chords cannot be tied.");

            var tiedNote = BuildNoteNode(tiedToken);
            note = note with { Notation = NotationParser.ParseTie(note.Notation, tiedNote.Notation, tieToken) };
        }

        return note;
    }

    private NoteNode BuildNoteNode(Token noteToken, ArticulationType? prefixArticulation = null)
    {
        var (durationBeats, standardDuration) = ParseOptionalDuration();
        var articulation = prefixArticulation;

        if (Match(TokenType.Articulation))
        {
            var articulationToken = Previous();
            articulation = NotationParser.ParseArticulation(articulationToken.Value, articulationToken);
        }

        var notation = NotationParser.BuildNotatedNote(
            noteToken.Value,
            noteToken,
            durationBeats,
            standardDuration,
            articulation);

        return new NoteNode
        {
            Notation = notation,
            Velocity = ParseOptionalVelocity()
        };
    }

    private RestNode ParseRestStatement()
    {
        var (durationBeats, standardDuration) = ParseRestDuration();
        return new RestNode
        {
            Rest = NotationParser.ParseRest(durationBeats, standardDuration)
        };
    }

    private DynamicNode ParseDynamicStatement()
    {
        var token = Previous();
        return new DynamicNode
        {
            Level = NotationParser.ParseDynamic(token.Value, token)
        };
    }

    private ArticulationType? ParseOptionalPrefixArticulation()
    {
        if (!Match(TokenType.Articulation))
            return null;

        var token = Previous();
        return NotationParser.ParseArticulation(token.Value, token);
    }

    private (double Beats, NoteDuration? StandardDuration) ParseRestDuration()
    {
        if (Match(TokenType.Colon))
        {
            var durationToken = Expect(TokenType.Number, "rest duration");
            return (ParseDuration(durationToken), null);
        }

        if (Match(TokenType.For))
        {
            var durationToken = Expect(TokenType.Number, "rest duration");
            return (ParseDuration(durationToken), null);
        }

        if (Match(TokenType.Duration))
        {
            var durationToken = Previous();
            if (Match(TokenType.Duration))
            {
                var extraToken = Previous();
                throw Invalid(extraToken, $"Invalid rest duration: 'rest {durationToken.Value}{extraToken.Value}'");
            }

            NotationParser.ValidateRestDurationToken(durationToken.Value, durationToken);
            var (standardDuration, beats) = NotationParser.ParseDurationAlias(durationToken.Value, durationToken);
            return (beats, standardDuration);
        }

        var next = Peek();
        throw Invalid(next, $"Invalid rest duration: 'rest {next.Value}'");
    }

    private ChordNode ParseChordStatement()
    {
        var chordToken = Expect(TokenType.Chord, "chord");
        var chord = ParseChordToken(chordToken);
        var (durationBeats, _) = ParseOptionalDuration();
        chord = chord with
        {
            DurationBeats = durationBeats,
            Velocity = ParseOptionalVelocity()
        };
        return chord;
    }

    private bool IsDominantSeventhAmbiguity(Token noteToken)
    {
        if (!noteToken.Value.EndsWith('7'))
            return false;

        var text = noteToken.Value;
        var index = 1;

        if (index < text.Length && text[index] is '#' or 'b' or 'B')
            index++;

        if (index >= text.Length || text[index] != '7')
            return false;

        if (index + 1 < text.Length)
            return false;

        return Check(TokenType.Duration);
    }

    private ChordNode ParseDominantSeventhChord(Token token)
    {
        var text = token.Value;
        var root = text[0];
        var index = 1;
        var isSharp = false;
        var isFlat = false;

        if (index < text.Length && text[index] == '#')
        {
            isSharp = true;
            index++;
        }
        else if (index < text.Length && (text[index] == 'b' || text[index] == 'B'))
        {
            isFlat = true;
            index++;
        }

        var (durationBeats, _) = ParseOptionalDuration();
        var chord = new ChordNode
        {
            Root = root,
            IsSharp = isSharp,
            IsFlat = isFlat,
            Quality = ChordQuality.Dominant7,
            Octave = 4,
            DurationBeats = durationBeats,
            Velocity = ParseOptionalVelocity()
        };
        return chord;
    }

    private static NoteNode ParseNoteToken(Token token) =>
        new()
        {
            Notation = NotationParser.BuildNotatedNote(token.Value, token)
        };

    private static ChordNode ParseChordToken(Token token)
    {
        var text = token.Value;
        var root = text[0];
        var index = 1;
        var isSharp = false;
        var isFlat = false;

        if (index < text.Length && text[index] == '#')
        {
            isSharp = true;
            index++;
        }
        else if (index < text.Length && (text[index] == 'b' || text[index] == 'B'))
        {
            isFlat = true;
            index++;
        }

        var remaining = text[index..];
        var (quality, suffixLength) = ParseChordQuality(remaining);
        index += suffixLength;

        var octave = 4;
        if (index < text.Length)
        {
            if (!int.TryParse(text[index..], out octave))
                throw Invalid(token, $"Invalid chord '{text}'.");
        }

        return new ChordNode
        {
            Root = root,
            IsSharp = isSharp,
            IsFlat = isFlat,
            Quality = quality,
            Octave = octave
        };
    }

    private static (ChordQuality Quality, int Length) ParseChordQuality(string suffix)
    {
        if (suffix.StartsWith("maj7", StringComparison.OrdinalIgnoreCase))
            return (ChordQuality.Major7, 4);
        if (suffix.StartsWith("maj", StringComparison.OrdinalIgnoreCase))
            return (ChordQuality.Major, 3);
        if (suffix.StartsWith("min", StringComparison.OrdinalIgnoreCase))
            return (ChordQuality.Minor, 3);
        if (suffix.StartsWith("dim", StringComparison.OrdinalIgnoreCase))
            return (ChordQuality.Diminished, 3);
        if (suffix.StartsWith("aug", StringComparison.OrdinalIgnoreCase))
            return (ChordQuality.Augmented, 3);
        if (suffix.StartsWith('m') && (suffix.Length == 1 || char.IsDigit(suffix[1])))
            return (ChordQuality.Minor, 1);

        throw new InvalidOperationException($"Unknown chord quality in '{suffix}'.");
    }

    private (double Beats, NoteDuration? StandardDuration) ParseOptionalDuration()
    {
        if (Match(TokenType.Colon))
        {
            var durationToken = Expect(TokenType.Number, "duration");
            return (ParseDuration(durationToken), null);
        }

        if (Match(TokenType.For))
        {
            var durationToken = Expect(TokenType.Number, "duration");
            return (ParseDuration(durationToken), null);
        }

        if (Match(TokenType.Duration))
        {
            var durationToken = Previous();
            var (standardDuration, beats) = NotationParser.ParseDurationAlias(durationToken.Value, durationToken);
            return (beats, standardDuration);
        }

        return (1.0, null);
    }

    private int? ParseOptionalVelocity()
    {
        if (!Match(TokenType.VelocityPrefix))
            return null;

        if (!int.TryParse(Previous().Value, out var velocity) || velocity <= 0 || velocity > 127)
            throw Invalid(Previous(), "Velocity must be between 1 and 127.");

        return velocity;
    }

    private static double ParseDuration(Token token)
    {
        if (!double.TryParse(token.Value, out var duration) || duration <= 0)
            throw Invalid(token, "Duration must be a positive number.");

        return duration;
    }

    private string ParseName(string description)
    {
        var token = Peek();
        if (token.Type is TokenType.Identifier or TokenType.Melody or TokenType.Bpm or TokenType.Tempo
            or TokenType.Time or TokenType.Play or TokenType.For or TokenType.Instrument
            or TokenType.Gain or TokenType.Humanize
            or TokenType.Sequence or TokenType.Block or TokenType.Loop or TokenType.Velocity or TokenType.Track
            or TokenType.Rest or TokenType.Articulation or TokenType.Dynamic)
        {
            Advance();
            return token.Value;
        }

        throw Invalid(token, $"Expected {description}.");
    }

    private static int ParsePositiveInt(Token token, string label)
    {
        if (!int.TryParse(token.Value, out var value) || value <= 0)
            throw Invalid(token, $"{label} must be a positive integer.");

        return value;
    }

    private Token Expect(TokenType type, string description)
    {
        if (!Check(type))
            throw Invalid(Peek(), $"Expected {description}.");

        return Advance();
    }

    private bool Match(TokenType type)
    {
        if (!Check(type))
            return false;

        Advance();
        return true;
    }

    private bool Check(TokenType type) => Peek().Type == type;

    private Token Advance() => _tokens[_position++];

    private Token Peek() => _tokens[_position];

    private Token Previous() => _tokens[_position - 1];

    private static void ThrowIfInvalidNoteAttempt(Token token)
    {
        if (token.Type == TokenType.Duration && token.Value.Length > 1)
            throw Invalid(token, $"Unknown duration: '{token.Value}'");

        if (token.Type == TokenType.Identifier)
        {
            if (NotationParser.TryGetInvalidArticulationMessage(token.Value, out var articulationMessage))
                throw Invalid(token, articulationMessage);

            if (NotationParser.TryGetInvalidDynamicMessage(token.Value, out var dynamicMessage))
                throw Invalid(token, dynamicMessage);

            if (NotationParser.TryGetInvalidNoteMessage(token.Value, out var message))
                throw Invalid(token, message);
        }
    }

    private static InvalidOperationException Invalid(Token token, string message) =>
        new($"{message} (line {token.Line}, column {token.Column}).");
}

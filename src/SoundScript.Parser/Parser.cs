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

        if (Match(TokenType.Pattern))
            return ParseNamedPattern();

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

        if (Match(TokenType.Orchestration))
            return ParseOrchestrationStatement();

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

    private PatternNode ParseNamedPattern()
    {
        var name = ParseName("pattern name");
        Expect(TokenType.LeftBrace, "{");

        var pattern = new PatternNode { Name = name };

        while (!Check(TokenType.RightBrace) && !Check(TokenType.EndOfFile))
        {
            if (Match(TokenType.PatternRhythm))
            {
                pattern.RhythmBeats.AddRange(ParsePatternRhythmDurations());
                pattern = pattern with { Kind = PatternKind.Rhythm };
                continue;
            }

            if (!Match(TokenType.PatternDirective))
            {
                var unexpected = Peek();
                throw Invalid(unexpected, $"Unexpected token '{unexpected.Value}' in pattern body.");
            }

            var directive = Previous().Value.ToLowerInvariant();
            pattern = directive switch
            {
                "up" => pattern with { Kind = PatternKind.Arpeggio, Direction = PatternDirection.Up },
                "down" => pattern with { Kind = PatternKind.Arpeggio, Direction = PatternDirection.Down },
                "updown" => pattern with { Kind = PatternKind.Arpeggio, Direction = PatternDirection.UpDown },
                "strum" => pattern with { Kind = PatternKind.Strum, Direction = PatternDirection.Up },
                _ => throw Invalid(Previous(), $"Unknown pattern directive '{directive}'.")
            };
        }

        Expect(TokenType.RightBrace, "}");
        return pattern;
    }

    private IEnumerable<double> ParsePatternRhythmDurations()
    {
        while (Check(TokenType.Duration) || Check(TokenType.Number))
        {
            if (Check(TokenType.Duration))
            {
                var token = Advance();
                yield return NotationParser.ParseDurationAlias(token.Value, token).Beats;
                continue;
            }

            var numberToken = Expect(TokenType.Number, "rhythm duration");
            if (!double.TryParse(numberToken.Value, out var beats) || beats <= 0)
                throw Invalid(numberToken, "Rhythm duration must be a positive number.");

            yield return beats;
        }
    }

    private AstNode ParseBlockBodyStatement()
    {
        if (Match(TokenType.Play))
            return ParsePlayStatement();

        if (Match(TokenType.Phrase))
            return ParsePhraseBlock();

        if (Match(TokenType.Bar))
            return new BarNode(Previous().Line);

        if (Match(TokenType.Rest))
            return ParseRestStatement();

        if (Match(TokenType.Dynamic))
            return ParseDynamicStatement();

        if (Match(TokenType.Orchestration))
            return ParseOrchestrationStatement();

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
        var name = ParseName("block, sequence, or pattern name");

        if (Check(TokenType.Chord))
            return new PlayNode { SequenceName = name, PatternChord = ParseChordStatement() };

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

    private PhraseNode ParsePhraseBlock()
    {
        Expect(TokenType.LeftBrace, "{");

        var phrase = new PhraseNode();

        while (!Check(TokenType.RightBrace) && !Check(TokenType.EndOfFile))
            phrase.Body.Add(ParsePhraseBodyStatement());

        Expect(TokenType.RightBrace, "}");
        return phrase;
    }

    private AstNode ParsePhraseBodyStatement()
    {
        if (Match(TokenType.Curve))
            return ParsePhraseCurveStatement();

        if (Match(TokenType.Transition))
            return ParsePhraseTransitionStatement();

        if (Match(TokenType.PhraseArticulation))
            return ParsePhraseArticulationStatement();

        if (Match(TokenType.Crescendo))
            return new PhraseEnvelopeNode { Envelope = PhraseEnvelopeType.Crescendo };

        if (Match(TokenType.Decrescendo))
            return new PhraseEnvelopeNode { Envelope = PhraseEnvelopeType.Decrescendo };

        if (Match(TokenType.Swing))
            return ParsePhraseSwingStatement();

        if (Match(TokenType.Push))
            return ParsePhrasePushStatement();

        if (Match(TokenType.Pull))
            return ParsePhrasePullStatement();

        if (Match(TokenType.Play))
            return ParsePlayStatement();

        if (Match(TokenType.Bar))
            return new BarNode(Previous().Line);

        if (Match(TokenType.Rest))
            return ParseRestStatement();

        if (Match(TokenType.Dynamic))
            return ParseDynamicStatement();

        if (Match(TokenType.Orchestration))
            return ParseOrchestrationStatement();

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

    private PhraseCurveNode ParsePhraseCurveStatement()
    {
        var token = Expect(TokenType.Identifier, "phrase curve");
        return new PhraseCurveNode
        {
            Curve = token.Value.ToLowerInvariant() switch
            {
                "soft" or "gentle" => PhraseCurveType.Soft,
                "hard" or "strong" or "aggressive" => PhraseCurveType.Hard,
                "balanced" => PhraseCurveType.Balanced,
                "expressive" => PhraseCurveType.Expressive,
                "swell" => PhraseCurveType.Swell,
                "fade" => PhraseCurveType.Fade,
                _ => throw Invalid(token, $"Unknown phrase curve '{token.Value}'.")
            }
        };
    }

    private PhraseTransitionNode ParsePhraseTransitionStatement()
    {
        var token = Expect(TokenType.Identifier, "phrase transition");
        return new PhraseTransitionNode
        {
            Mode = token.Value.ToLowerInvariant() switch
            {
                "smooth" => PhraseTransitionMode.Smooth,
                "abrupt" or "sharp" => PhraseTransitionMode.Abrupt,
                "soft" => PhraseTransitionMode.Soft,
                "expressive" => PhraseTransitionMode.Expressive,
                _ => throw Invalid(token, $"Unknown phrase transition '{token.Value}'.")
            }
        };
    }

    private PhraseArticulationNode ParsePhraseArticulationStatement()
    {
        if (!Check(TokenType.Articulation))
        {
            var token = Peek();
            throw Invalid(token, "Expected articulation after 'articulation'.");
        }

        var articulationToken = Advance();
        return new PhraseArticulationNode
        {
            Articulation = articulationToken.Value.ToLowerInvariant() switch
            {
                "staccato" => ArticulationType.Staccato,
                "legato" => ArticulationType.Legato,
                "accent" => ArticulationType.Accent,
                "detached" => ArticulationType.Staccato,
                _ => throw Invalid(articulationToken, $"Unknown phrase articulation '{articulationToken.Value}'.")
            }
        };
    }

    private PhraseSwingNode ParsePhraseSwingStatement()
    {
        var token = Expect(TokenType.Number, "swing ratio");
        if (!double.TryParse(token.Value, out var swingValue) || swingValue < 0 || swingValue > 1)
            throw Invalid(token, "Swing ratio must be between 0 and 1.");

        return new PhraseSwingNode { Ratio = swingValue };
    }

    private PhrasePushNode ParsePhrasePushStatement()
    {
        var token = Expect(TokenType.Number, "push value");
        if (!double.TryParse(token.Value, out var pushValue) || pushValue < 0)
            throw Invalid(token, "Push value must be non-negative.");

        return new PhrasePushNode { Beats = pushValue };
    }

    private PhrasePullNode ParsePhrasePullStatement()
    {
        var token = Expect(TokenType.Number, "pull value");
        if (!double.TryParse(token.Value, out var pullValue) || pullValue < 0)
            throw Invalid(token, "Pull value must be non-negative.");

        return new PhrasePullNode { Beats = pullValue };
    }

    private OrchestrationNode ParseOrchestrationStatement()
    {
        var keyword = Previous().Value.ToLowerInvariant();
        var modifier = Expect(TokenType.Identifier, "orchestration modifier");

        return keyword switch
        {
            "double" when modifier.Value.Equals("octave", StringComparison.OrdinalIgnoreCase)
                => new OrchestrationNode { Type = OrchestrationType.DoubleOctave },
            "reinforce" when modifier.Value.Equals("bass", StringComparison.OrdinalIgnoreCase)
                => new OrchestrationNode { Type = OrchestrationType.ReinforceBass },
            "brighten" when modifier.Value.Equals("top", StringComparison.OrdinalIgnoreCase)
                => new OrchestrationNode { Type = OrchestrationType.BrightenTop },
            _ => throw Invalid(modifier, $"Unknown orchestration directive '{keyword} {modifier.Value}'.")
        };
    }

    private AstNode ParseBodyStatement(bool allowLoop)
    {
        if (allowLoop && Match(TokenType.Loop))
            return ParseLoopBlock();

        if (Match(TokenType.Phrase))
            return ParsePhraseBlock();

        if (Match(TokenType.Bpm))
            return ParseBpmStatement();

        if (Match(TokenType.Tempo))
            return ParseTempoStatement();

        if (Match(TokenType.Time))
            return ParseTimeSignatureStatement();

        if (Match(TokenType.Instrument))
            return ParseInstrumentStatement();

        if (Match(TokenType.Layer))
            return ParseLayerStatement();

        if (Match(TokenType.Gain))
            return ParseGainStatement();

        if (Match(TokenType.Humanize))
            return ParseHumanizeStatement();

        if (Match(TokenType.Velocity))
            return ParseVelocityStatement();

        if (Match(TokenType.Play))
            return ParsePlayStatement();

        if (Match(TokenType.Bar))
            return new BarNode(Previous().Line);

        if (Match(TokenType.Rest))
            return ParseRestStatement();

        if (Match(TokenType.Dynamic))
            return ParseDynamicStatement();

        if (Match(TokenType.Orchestration))
            return ParseOrchestrationStatement();

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

    private AstNode ParseTempoStatement()
    {
        var startToken = Expect(TokenType.Number, "tempo value");
        var startBpm = ParsePositiveInt(startToken, "Tempo");

        if (!Match(TokenType.Arrow))
            return new TempoNode { Bpm = startBpm };

        var endToken = Expect(TokenType.Number, "target tempo");
        var endBpm = ParsePositiveInt(endToken, "Target tempo");
        Expect(TokenType.Over, "over");
        var barsToken = Expect(TokenType.Number, "bar count");
        var bars = ParsePositiveInt(barsToken, "Bar count");
        Expect(TokenType.Bars, "bars");

        return new TempoRampNode
        {
            StartBpm = startBpm,
            EndBpm = endBpm,
            Bars = bars
        };
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

    private LayerNode ParseLayerStatement()
    {
        var nameToken = Expect(TokenType.Identifier, "layer instrument name");
        return new LayerNode
        {
            Name = nameToken.Value,
            ProgramNumber = InstrumentMap.Resolve(nameToken.Value)
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
        var voicing = ParseOptionalChordVoicing();
        var (durationBeats, _) = ParseOptionalDuration();
        chord = chord with
        {
            Voicing = voicing,
            DurationBeats = durationBeats,
            Velocity = ParseOptionalVelocity()
        };
        return chord;
    }

    private ChordVoicingStyle? ParseOptionalChordVoicing()
    {
        if (!Match(TokenType.ChordVoicing))
            return null;

        var token = Previous();
        return token.Value.ToLowerInvariant() switch
        {
            "drop2" => ChordVoicingStyle.Drop2,
            "drop3" => ChordVoicingStyle.Drop3,
            "inv1" => ChordVoicingStyle.Inversion1,
            "inv2" => ChordVoicingStyle.Inversion2,
            "spread" => ChordVoicingStyle.Spread,
            _ => throw Invalid(token, $"Unknown chord voicing '{token.Value}'.")
        };
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
            or TokenType.Gain or TokenType.Humanize or TokenType.Over or TokenType.Bars or TokenType.Layer
            or TokenType.Sequence or TokenType.Block or TokenType.Loop or TokenType.Velocity or TokenType.Track
            or TokenType.Rest or TokenType.Articulation or TokenType.Dynamic or TokenType.Phrase
            or TokenType.Curve or TokenType.Transition or TokenType.Pattern
            or TokenType.PatternRhythm or TokenType.Orchestration)
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

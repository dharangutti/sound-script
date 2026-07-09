// UNDER DEVELOPMENT — v3: adds generic key=value parameter parsing and the
// wave-only 'effect'/'speak' directives, and extends 'humanize' with a named
// parameter form (timing=/velocity=/seed=). The v1 bare-number humanize path
// and all pre-existing grammar are byte-for-byte unchanged.
using System.Globalization;
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

        if (Match(TokenType.Voice))
            return ParseVoiceBlock();

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

        // v3 wave-only 'effect': master-level (applies to the final mix-down),
        // so top-level only. The MIDI interpreter rejects the resulting node
        // with a clear error. 'speak' is per-track, not master-level — see
        // ParseBodyStatement for the track/sequence/block/loop-body form.
        if (Match(TokenType.Effect))
            return ParseEffectStatement();

        if (Match(TokenType.Speak))
            return ParseSpeakStatement();

        if (Match(TokenType.Sample))
            return ParseSampleStatement();

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

    private VoiceNode ParseVoiceBlock()
    {
        var name = ParseName("voice name");
        Expect(TokenType.LeftBrace, "{");

        var voice = new VoiceNode { Name = name };

        while (!Check(TokenType.RightBrace) && !Check(TokenType.EndOfFile))
        {
            voice.Body.Add(ParseVoiceBodyStatement());
        }

        Expect(TokenType.RightBrace, "}");
        return voice;
    }

    private AstNode ParseVoiceBodyStatement()
    {
        if (Match(TokenType.Vocal))
            return ParseVocalTimbreStatement();

        if (Match(TokenType.Sing))
            return ParseSingStatement();

        if (Match(TokenType.Rest))
            return ParseRestStatement();

        if (Match(TokenType.Dynamic))
            return ParseDynamicStatement();

        if (Match(TokenType.Velocity))
            return ParseVelocityStatement();

        var unexpected = Peek();
        throw Invalid(unexpected,
            $"Unexpected token '{unexpected.Value}' in voice body. Voice blocks support: vocal, sing, rest, dynamics, velocity.");
    }

    private VocalTimbreNode ParseVocalTimbreStatement()
    {
        var nameToken = Expect(TokenType.Identifier, "vocal timbre name");
        return new VocalTimbreNode
        {
            Name = nameToken.Value,
            ProgramNumber = VocalTimbreMap.Resolve(nameToken.Value)
        };
    }

    private SingNode ParseSingStatement()
    {
        var lyricToken = Expect(TokenType.StringLiteral, "lyric string after 'sing'");
        var sing = new SingNode { Lyric = lyricToken.Value };

        while (Check(TokenType.Note))
        {
            var noteToken = Advance();
            sing.Notes.Add(BuildNoteNode(noteToken));
        }

        if (sing.Notes.Count == 0)
            throw Invalid(lyricToken, "sing requires at least one note after the lyric.");

        return sing;
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

        // v3 wave-only 'speak': per-track (unlike 'effect', which is
        // master-level and top-level only — see ParseTopLevelStatement).
        if (Match(TokenType.Speak))
            return ParseSpeakStatement();

        if (Match(TokenType.Sample))
            return ParseSampleStatement();

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
        // v1 bare-number form — unchanged, no migration required:
        //   humanize 0.02
        if (Check(TokenType.Number))
        {
            var token = Advance();
            if (!double.TryParse(token.Value, out var value) || value < 0)
                throw Invalid(token, "Humanize must be a non-negative number.");

            return new HumanizeNode { Value = value };
        }

        // v3 named-parameter form:
        //   humanize timing=0.02 velocity=0.1 seed=42
        var humanizeToken = Previous();
        var parameters = ParseKeyValueParameters("humanize");
        if (parameters.Count == 0)
        {
            throw Invalid(Peek(),
                "Expected humanize value (e.g. 'humanize 0.02') or named parameters " +
                "(e.g. 'humanize timing=0.02 velocity=0.1 seed=42').");
        }

        RejectUnknownParameters(parameters, "humanize", "timing", "velocity", "seed");

        double? timing = null;
        if (parameters.TryGetValue("timing", out var timingToken))
        {
            timing = ParseDoubleParameter(timingToken, "humanize timing");
            if (timing < 0)
                throw Invalid(timingToken, "humanize timing must be a non-negative number of seconds.");
        }

        double? velocityAmount = null;
        if (parameters.TryGetValue("velocity", out var velocityToken))
        {
            velocityAmount = ParseDoubleParameter(velocityToken, "humanize velocity");
            if (velocityAmount < 0 || velocityAmount > 1)
                throw Invalid(velocityToken, "humanize velocity must be between 0.0 and 1.0.");
        }

        int? seed = null;
        if (parameters.TryGetValue("seed", out var seedToken))
            seed = ParseNonNegativeIntParameter(seedToken, "humanize seed");

        if (timing is null && velocityAmount is null)
        {
            throw Invalid(humanizeToken,
                "humanize named form requires at least one of timing= or velocity= " +
                "(seed= alone has nothing to vary).");
        }

        return new HumanizeNode
        {
            // Value is unused by the named form (consumers call Resolve()
            // instead); kept populated only so the required member always
            // has a defined value.
            Value = timing ?? 0.0,
            Timing = timing,
            VelocityAmount = velocityAmount,
            Seed = seed
        };
    }

    private EffectNode ParseEffectStatement()
    {
        var kindToken = Peek();
        var kind = ParseName("effect kind ('delay' or 'filter')").ToLowerInvariant();

        if (kind == "reverb")
        {
            throw Invalid(kindToken,
                $"Effect 'reverb' is deferred (v3 parking lot) — supported effects: {EffectKinds.SupportedListText}.");
        }

        var parameters = ParseKeyValueParameters($"effect {kind}");

        switch (kind)
        {
            case EffectKinds.Delay:
            {
                RequireParameter(parameters, kindToken, "effect delay", "time");
                RejectUnknownParameters(parameters, "effect delay", "time", "feedback", "mix");

                var time = ParseDoubleParameter(parameters["time"], "effect delay time");
                if (time <= 0)
                    throw Invalid(parameters["time"], "effect delay time must be a positive number of seconds.");

                if (parameters.TryGetValue("feedback", out var feedbackToken))
                {
                    var feedback = ParseDoubleParameter(feedbackToken, "effect delay feedback");
                    if (feedback < 0 || feedback >= 1)
                        throw Invalid(feedbackToken, "effect delay feedback must be at least 0.0 and below 1.0.");
                }

                if (parameters.TryGetValue("mix", out var mixToken))
                {
                    var mix = ParseDoubleParameter(mixToken, "effect delay mix");
                    if (mix < 0 || mix > 1)
                        throw Invalid(mixToken, "effect delay mix must be between 0.0 and 1.0.");
                }

                break;
            }

            case EffectKinds.Filter:
            {
                RequireParameter(parameters, kindToken, "effect filter", "type");
                RequireParameter(parameters, kindToken, "effect filter", "cutoff");
                RejectUnknownParameters(parameters, "effect filter", "type", "cutoff");

                var typeToken = parameters["type"];
                var type = typeToken.Value.ToLowerInvariant();
                if (type is not ("lowpass" or "highpass"))
                    throw Invalid(typeToken, $"Unknown filter type '{typeToken.Value}'. Supported: lowpass, highpass.");

                var cutoff = ParseDoubleParameter(parameters["cutoff"], "effect filter cutoff");
                if (cutoff <= 0)
                    throw Invalid(parameters["cutoff"], "effect filter cutoff must be a positive frequency in Hz.");

                break;
            }

            default:
                throw Invalid(kindToken, $"Unknown effect '{kind}'. Supported effects: {EffectKinds.SupportedListText}.");
        }

        var rawParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, valueToken) in parameters)
            rawParameters[key] = valueToken.Value;

        return new EffectNode { Kind = kind, Parameters = rawParameters };
    }

    private SpeakNode ParseSpeakStatement()
    {
        var textToken = Expect(TokenType.StringLiteral, "text to speak (a quoted string)");

        var hasLetter = false;
        foreach (var ch in textToken.Value)
        {
            if (char.IsLetter(ch))
            {
                hasLetter = true;
                break;
            }
        }

        if (!hasLetter)
            throw Invalid(textToken, "speak text must contain at least one letter.");

        var parameters = ParseKeyValueParameters("speak");
        RejectUnknownParameters(parameters, "speak", "voice", "seed", "sample", "gain");

        var voice = parameters.TryGetValue("voice", out var voiceToken)
            ? voiceToken.Value
            : "default";

        int? seed = null;
        if (parameters.TryGetValue("seed", out var seedToken))
            seed = ParseNonNegativeIntParameter(seedToken, "speak seed");

        string? samplePath = null;
        if (parameters.TryGetValue("sample", out var sampleToken))
            samplePath = sampleToken.Value;

        var sampleGain = 1.0;
        if (parameters.TryGetValue("gain", out var sampleGainToken))
            sampleGain = ParsePositiveDoubleParameter(sampleGainToken, "speak gain (sample level)");

        return new SpeakNode
        {
            Text = textToken.Value,
            Voice = voice,
            Seed = seed,
            SamplePath = samplePath,
            SampleGain = sampleGain,
        };
    }

    private SampleNode ParseSampleStatement()
    {
        var pathToken = Expect(TokenType.StringLiteral, "path to a WAV file (a quoted string)");
        var parameters = ParseKeyValueParameters("sample");
        RejectUnknownParameters(parameters, "sample", "gain", "at");

        var gain = 1.0;
        if (parameters.TryGetValue("gain", out var gainToken))
            gain = ParsePositiveDoubleParameter(gainToken, "sample gain");

        double? atBeats = null;
        if (parameters.TryGetValue("at", out var atToken))
            atBeats = ParsePositiveDoubleParameter(atToken, "sample at");

        return new SampleNode { Path = pathToken.Value, Gain = gain, AtBeats = atBeats };
    }

    private static double ParsePositiveDoubleParameter(Token token, string label)
    {
        if (!double.TryParse(token.Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var value) || value < 0)
        {
            throw Invalid(token, $"{label} must be a non-negative number.");
        }

        return value;
    }

    /// <summary>
    /// Parses a run of <c>key=value</c> pairs (e.g. <c>time=0.25 feedback=0.4</c>)
    /// into a key → value-token map. Keys are keyword-agnostic: any name-like
    /// token's raw text is accepted (same trick as <see cref="ParseName"/>), so
    /// e.g. <c>voice=</c> works even though 'voice' is a registered keyword.
    /// Two-token lookahead (name followed by '=') means the run never swallows
    /// the following statement. Keys are lower-cased; values keep raw text.
    /// </summary>
    private Dictionary<string, Token> ParseKeyValueParameters(string directive)
    {
        var parameters = new Dictionary<string, Token>(StringComparer.OrdinalIgnoreCase);

        while (IsNameLikeToken(Peek().Type) && PeekNext().Type == TokenType.Assign)
        {
            var keyToken = Advance();
            Advance(); // '='

            var valueToken = Peek();
            if (!IsParameterValueToken(valueToken.Type))
                throw Invalid(valueToken, $"Expected a value after '{keyToken.Value}=' in {directive}.");

            Advance();

            if (!parameters.TryAdd(keyToken.Value.ToLowerInvariant(), valueToken))
                throw Invalid(keyToken, $"Duplicate parameter '{keyToken.Value.ToLowerInvariant()}' in {directive}.");
        }

        return parameters;
    }

    private static bool IsParameterValueToken(TokenType type) =>
        type is TokenType.Number or TokenType.StringLiteral or TokenType.Note or TokenType.Chord
            or TokenType.Duration or TokenType.Dynamic
        || IsNameLikeToken(type);

    private static void RequireParameter(
        Dictionary<string, Token> parameters, Token directiveToken, string directive, string key)
    {
        if (!parameters.ContainsKey(key))
            throw Invalid(directiveToken, $"Missing required parameter '{key}=' in {directive}.");
    }

    private static void RejectUnknownParameters(
        Dictionary<string, Token> parameters, string directive, params string[] allowedKeys)
    {
        foreach (var (key, valueToken) in parameters)
        {
            var allowed = false;
            foreach (var candidate in allowedKeys)
            {
                if (string.Equals(key, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    allowed = true;
                    break;
                }
            }

            if (!allowed)
            {
                throw Invalid(valueToken,
                    $"Unknown parameter '{key}' in {directive}. Supported: {string.Join(", ", allowedKeys)}.");
            }
        }
    }

    // InvariantCulture on purpose: number tokens are always digits and '.',
    // and parameter values must parse identically on every machine/locale
    // (determinism safeguard).
    private static double ParseDoubleParameter(Token token, string label)
    {
        if (!double.TryParse(token.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            throw Invalid(token, $"{label} must be a number.");

        return value;
    }

    private static int ParseNonNegativeIntParameter(Token token, string label)
    {
        if (!int.TryParse(token.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0)
            throw Invalid(token, $"{label} must be a non-negative integer.");

        return value;
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
        if (IsNameLikeToken(token.Type))
        {
            Advance();
            return token.Value;
        }

        throw Invalid(token, $"Expected {description}.");
    }

    /// <summary>
    /// True for tokens whose raw text can serve as a plain name: identifiers
    /// plus every word-shaped keyword. Extracted from <see cref="ParseName"/>
    /// (identical set, plus the v3 Effect/Speak keywords so files that used
    /// 'effect' or 'speak' as names keep parsing) and shared with the v3
    /// key=value parameter scanner.
    /// </summary>
    private static bool IsNameLikeToken(TokenType type) =>
        type is TokenType.Identifier or TokenType.Melody or TokenType.Bpm or TokenType.Tempo
            or TokenType.Time or TokenType.Play or TokenType.For or TokenType.Instrument
            or TokenType.Gain or TokenType.Humanize or TokenType.Over or TokenType.Bars or TokenType.Layer
            or TokenType.Sequence or TokenType.Block or TokenType.Loop or TokenType.Velocity or TokenType.Track
            or TokenType.Rest or TokenType.Articulation or TokenType.Dynamic or TokenType.Phrase
            or TokenType.Curve or TokenType.Transition or TokenType.Pattern
            or TokenType.PatternRhythm or TokenType.Orchestration
            or TokenType.Voice or TokenType.Sing or TokenType.Vocal
            or TokenType.Effect or TokenType.Speak or TokenType.Sample;

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

    // Safe past-the-end lookahead: the token list always ends with EndOfFile,
    // so clamping to the last token degrades to "next is EOF".
    private Token PeekNext() =>
        _position + 1 < _tokens.Count ? _tokens[_position + 1] : _tokens[^1];

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

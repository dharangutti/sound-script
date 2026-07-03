using SoundScript.Core;

namespace SoundScript.Parser;

public sealed class Tokenizer
{
    private static readonly Dictionary<string, TokenType> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["melody"] = TokenType.Melody,
        ["bpm"] = TokenType.Bpm,
        ["tempo"] = TokenType.Tempo,
        ["time"] = TokenType.Time,
        ["play"] = TokenType.Play,
        ["for"] = TokenType.For,
        ["instrument"] = TokenType.Instrument,
        ["sequence"] = TokenType.Sequence,
        ["loop"] = TokenType.Loop,
        ["velocity"] = TokenType.Velocity,
        ["track"] = TokenType.Track,
        ["q"] = TokenType.Duration,
        ["h"] = TokenType.Duration,
        ["e"] = TokenType.Duration,
        ["w"] = TokenType.Duration,
        ["quarter"] = TokenType.Duration,
        ["half"] = TokenType.Duration,
        ["eighth"] = TokenType.Duration,
        ["whole"] = TokenType.Duration
    };

    private static readonly string[] ChordSuffixes =
    [
        "maj7", "maj", "min", "dim", "aug", "m"
    ];

    private readonly string _source;
    private int _index;
    private int _line = 1;
    private int _column = 1;

    public Tokenizer(string source)
    {
        _source = source;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (!IsAtEnd())
        {
            SkipWhitespace();
            if (IsAtEnd())
                break;

            var startLine = _line;
            var startColumn = _column;
            var current = Peek();

            if (current is '{' or '}' or '|' or ':' or '/')
            {
                var type = current switch
                {
                    '{' => TokenType.LeftBrace,
                    '}' => TokenType.RightBrace,
                    '|' => TokenType.Bar,
                    ':' => TokenType.Colon,
                    '/' => TokenType.Slash,
                    _ => throw new InvalidOperationException()
                };

                Advance();
                tokens.Add(new Token(type, current.ToString(), startLine, startColumn));
                continue;
            }

            if (current == 'v' && _index + 1 < _source.Length && char.IsDigit(_source[_index + 1]))
            {
                Advance();
                var numberStart = _index;
                while (!IsAtEnd() && char.IsDigit(Peek()))
                    Advance();

                var value = _source[numberStart.._index];
                tokens.Add(new Token(TokenType.VelocityPrefix, value, startLine, startColumn));
                continue;
            }

            if (char.IsDigit(current))
            {
                tokens.Add(ReadNumber(startLine, startColumn));
                continue;
            }

            if (char.IsLetter(current))
            {
                tokens.Add(ReadWordOrNoteOrChord(startLine, startColumn));
                continue;
            }

            throw new InvalidOperationException($"Unexpected character '{current}' at line {startLine}, column {startColumn}.");
        }

        tokens.Add(new Token(TokenType.EndOfFile, string.Empty, _line, _column));
        return tokens;
    }

    private Token ReadNumber(int line, int column)
    {
        var start = _index;
        while (!IsAtEnd() && char.IsDigit(Peek()))
            Advance();

        return new Token(TokenType.Number, _source[start.._index], line, column);
    }

    private Token ReadWordOrNoteOrChord(int line, int column)
    {
        var start = _index;
        var pitch = Advance();

        if (IsNotePitch(pitch))
        {
            var savedIndex = _index;
            var savedColumn = _column;

            ConsumeOptionalChordAccidental();

            var chordSuffix = TryReadChordSuffix();
            if (chordSuffix is not null)
            {
                ReadOctaveDigits();
                var text = _source[start.._index];
                return new Token(TokenType.Chord, text, line, column);
            }

            _index = savedIndex;
            _column = savedColumn;

            if (TryReadNoteOctaveSuffix())
            {
                var text = _source[start.._index];
                return new Token(TokenType.Note, text, line, column);
            }

            _index = start;
            _column = column;
        }
        else if (char.IsLetter(pitch) && TryReadInvalidNoteLikeSuffix())
        {
            var text = _source[start.._index];
            return new Token(TokenType.Identifier, text, line, column);
        }

        while (!IsAtEnd() && char.IsLetterOrDigit(Peek()))
            Advance();

        var word = _source[start.._index];
        if (Keywords.TryGetValue(word, out var keyword))
            return new Token(keyword, word, line, column);

        return new Token(TokenType.Identifier, word, line, column);
    }

    private void ConsumeOptionalChordAccidental()
    {
        if (!IsAtEnd() && Peek() == '#')
            Advance();
        else if (!IsAtEnd() && IsFlatSymbol(Peek()) && (_index + 1 >= _source.Length || !char.IsDigit(_source[_index + 1])))
            Advance();
        else if (!IsAtEnd() && IsNaturalSymbol(Peek()))
            Advance();
    }

    private void ConsumeNoteAccidentals()
    {
        while (!IsAtEnd())
        {
            if (Peek() == '#')
            {
                Advance();
                continue;
            }

            if (IsFlatSymbol(Peek()) && _index + 1 < _source.Length && char.IsDigit(_source[_index + 1]))
            {
                Advance();
                continue;
            }

            if (IsFlatSymbol(Peek()) && (_index + 1 >= _source.Length || !char.IsLetter(_source[_index + 1])))
            {
                Advance();
                continue;
            }

            if (IsNaturalSymbol(Peek()))
            {
                Advance();
                continue;
            }

            break;
        }
    }

    private bool TryReadNoteOctaveSuffix()
    {
        ConsumeNoteAccidentals();

        if (!IsAtEnd() && Peek() == '-')
            Advance();

        if (IsAtEnd() || !char.IsDigit(Peek()))
            return false;

        while (!IsAtEnd() && char.IsDigit(Peek()))
            Advance();

        return true;
    }

    private bool TryReadInvalidNoteLikeSuffix()
    {
        while (!IsAtEnd())
        {
            if (IsAccidentalSymbol(Peek()))
            {
                Advance();
                continue;
            }

            if (Peek() == '-')
            {
                Advance();
                continue;
            }

            if (char.IsDigit(Peek()))
            {
                while (!IsAtEnd() && char.IsDigit(Peek()))
                    Advance();
                return true;
            }

            break;
        }

        return false;
    }

    private string? TryReadChordSuffix()
    {
        foreach (var suffix in ChordSuffixes)
        {
            if (MatchesAt(suffix))
            {
                for (var i = 0; i < suffix.Length; i++)
                    Advance();
                return suffix;
            }
        }

        return null;
    }

    private string ReadOctaveDigits()
    {
        var start = _index;
        while (!IsAtEnd() && char.IsDigit(Peek()))
            Advance();
        return _source[start.._index];
    }

    private bool MatchesAt(string text)
    {
        if (_index + text.Length > _source.Length)
            return false;

        for (var i = 0; i < text.Length; i++)
        {
            if (char.ToLowerInvariant(_source[_index + i]) != char.ToLowerInvariant(text[i]))
                return false;
        }

        if (_index + text.Length < _source.Length && char.IsLetter(_source[_index + text.Length]))
            return false;

        return true;
    }

    private static bool IsFlatSymbol(char value) => value is 'b' or 'B' or '\u266D';

    private static bool IsNaturalSymbol(char value) => value is '\u266E';

    private static bool IsAccidentalSymbol(char value) =>
        value is '#' or '\u266F' or 'b' or 'B' or '\u266D' or '\u266E';

    private static bool IsNotePitch(char c) =>
        c is 'A' or 'B' or 'C' or 'D' or 'E' or 'F' or 'G'
            or 'a' or 'b' or 'c' or 'd' or 'e' or 'f' or 'g';

    private void SkipWhitespace()
    {
        while (!IsAtEnd())
        {
            var current = Peek();
            if (current is ' ' or '\t' or '\r')
            {
                Advance();
                continue;
            }

            if (current == '\n')
            {
                _line++;
                _column = 1;
                _index++;
                continue;
            }

            break;
        }
    }

    private char Peek() => _source[_index];

    private char Advance()
    {
        var current = _source[_index++];
        _column++;
        return current;
    }

    private bool IsAtEnd() => _index >= _source.Length;
}

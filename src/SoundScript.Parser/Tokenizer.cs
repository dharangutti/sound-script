using SoundScript.Core;

namespace SoundScript.Parser;

public sealed class Tokenizer
{
    private static readonly Dictionary<string, TokenType> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["melody"] = TokenType.Melody,
        ["bpm"] = TokenType.Bpm,
        ["play"] = TokenType.Play,
        ["for"] = TokenType.For
    };

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

            if (current is '{' or '}' or '|' or ':')
            {
                var type = current switch
                {
                    '{' => TokenType.LeftBrace,
                    '}' => TokenType.RightBrace,
                    '|' => TokenType.Bar,
                    ':' => TokenType.Colon,
                    _ => throw new InvalidOperationException()
                };

                Advance();
                tokens.Add(new Token(type, current.ToString(), startLine, startColumn));
                continue;
            }

            if (char.IsDigit(current))
            {
                tokens.Add(ReadNumber(startLine, startColumn));
                continue;
            }

            if (char.IsLetter(current))
            {
                tokens.Add(ReadWordOrNote(startLine, startColumn));
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

    private Token ReadWordOrNote(int line, int column)
    {
        var start = _index;
        var pitch = Advance();

        if (IsNotePitch(pitch))
        {
            if (!IsAtEnd() && Peek() == '#')
                Advance();
            else if (!IsAtEnd() && (Peek() == 'b' || Peek() == 'B') && (_index + 1 >= _source.Length || !char.IsDigit(_source[_index + 1])))
                Advance();

            if (!IsAtEnd() && char.IsDigit(Peek()))
            {
                while (!IsAtEnd() && char.IsDigit(Peek()))
                    Advance();

                var text = _source[start.._index];
                return new Token(TokenType.Note, text, line, column);
            }

            _index = start;
            _column = column;
        }

        while (!IsAtEnd() && char.IsLetterOrDigit(Peek()))
            Advance();

        var word = _source[start.._index];
        if (Keywords.TryGetValue(word, out var keyword))
            return new Token(keyword, word, line, column);

        throw new InvalidOperationException($"Unknown token '{word}' at line {line}, column {column}.");
    }

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

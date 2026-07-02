using SoundScript.Core;

namespace SoundScript.Parser;

public sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _position;

    public Parser(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens;
    }

    public MelodyProgram Parse()
    {
        Expect(TokenType.Melody, "melody");
        Expect(TokenType.LeftBrace, "{");

        var program = new MelodyProgram();

        while (!Check(TokenType.RightBrace) && !Check(TokenType.EndOfFile))
        {
            if (Match(TokenType.Bpm))
            {
                var bpmToken = Expect(TokenType.Number, "BPM value");
                if (!int.TryParse(bpmToken.Value, out var bpm) || bpm <= 0)
                    throw Invalid(bpmToken, "BPM must be a positive integer.");

                program.Bpm = bpm;
                continue;
            }

            if (Match(TokenType.Bar))
                continue;

            if (Check(TokenType.Note))
            {
                program.Notes.Add(ParseNote(Advance()));
                continue;
            }

            var unexpected = Peek();
            throw Invalid(unexpected, $"Unexpected token '{unexpected.Value}'.");
        }

        Expect(TokenType.RightBrace, "}");
        Expect(TokenType.EndOfFile, "end of file");
        return program;
    }

    private static ParsedNote ParseNote(Token token)
    {
        var text = token.Value;
        var pitch = text[0];
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

        if (!int.TryParse(text[index..], out var octave))
            throw Invalid(token, $"Invalid note '{text}'.");

        return new ParsedNote(pitch, isSharp, isFlat, octave);
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

    private static InvalidOperationException Invalid(Token token, string message) =>
        new($"{message} (line {token.Line}, column {token.Column}).");
}

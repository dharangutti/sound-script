namespace SoundScript.Vocal;

internal static class VocalTextSplitter
{
    internal static List<string> SplitWords(string text)
    {
        var words = new List<string>();
        var start = -1;

        for (var i = 0; i <= text.Length; i++)
        {
            var isLetter = i < text.Length && char.IsLetter(text[i]);
            if (isLetter && start < 0)
            {
                start = i;
            }
            else if (!isLetter && start >= 0)
            {
                words.Add(text[start..i]);
                start = -1;
            }
        }

        return words;
    }
}

using System.Text.RegularExpressions;
using SoundScript.Core.Ast;
using SoundScript.Wave.Adapter;

namespace SoundScript.Wave.Tts;

/// <summary>
/// Maps <c>speak</c> phrases to pre-rendered WAV files in a directory (V8 CLI
/// <c>--tts-dir</c>). Filenames: slugified speak text (<c>hello-world.wav</c>).
/// </summary>
public static class TtsDirectoryMapper
{
    private static readonly Regex SlugPattern = new(@"[^a-z0-9]+", RegexOptions.Compiled);

    public static IReadOnlyList<SampleOverlayRequest> BuildOverlays(
        IReadOnlyList<(SpeakNode Speak, double StartTimeSeconds)> speakTimings,
        string ttsDirectory)
    {
        var overlays = new List<SampleOverlayRequest>();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var index = 0;

        foreach (var (speak, startSeconds) in speakTimings)
        {
            index++;
            if (!string.IsNullOrWhiteSpace(speak.SamplePath))
                continue;

            var slug = Slugify(speak.Text);
            counts.TryGetValue(slug, out var seen);
            counts[slug] = seen + 1;

            var fileName = seen == 0 ? $"{slug}.wav" : $"{index:D3}-{slug}.wav";
            var path = Path.Combine(ttsDirectory, fileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"TTS directory is missing a vocal file for speak \"{speak.Text}\" (expected {path}).");
            }

            overlays.Add(new SampleOverlayRequest(path, startSeconds, 1.0));
        }

        return overlays;
    }

    public static string Slugify(string text)
    {
        var lower = text.Trim().ToLowerInvariant();
        var slug = SlugPattern.Replace(lower, "-").Trim('-');
        if (string.IsNullOrEmpty(slug))
            slug = "speech";
        return slug.Length > 48 ? slug[..48].Trim('-') : slug;
    }
}

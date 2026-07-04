using SoundScript.Core;

namespace SoundScript.Voice;

/// <summary>
/// One spoken word for playback preview: absolute onset and duration in
/// milliseconds (tempo-map accurate) plus the MIDI pitch of its first note.
/// </summary>
public sealed record VocalSpeechWord(string Text, double StartMs, double DurationMs, int Midi);

/// <summary>
/// Reassembles the syllable stream of an <see cref="InterpretedVocalTrack"/>
/// into whole words with millisecond timing, for speech-based playback layers
/// (e.g. the browser playground's Web Speech preview). Melisma continuations
/// extend the duration of the word they prolong.
/// </summary>
public static class VocalSpeechTimeline
{
    public static IReadOnlyList<VocalSpeechWord> Build(InterpretedProgram interpreted)
    {
        var words = new List<VocalSpeechWord>();

        foreach (var track in interpreted.VocalTracks)
            AppendTrackWords(words, track, interpreted);

        words.Sort((a, b) => a.StartMs.CompareTo(b.StartMs));
        return words;
    }

    private static void AppendTrackWords(
        List<VocalSpeechWord> words,
        InterpretedVocalTrack track,
        InterpretedProgram interpreted)
    {
        var text = string.Empty;
        var startMs = 0.0;
        var durationMs = 0.0;
        var midi = 0;
        var wordComplete = false;

        foreach (var syllable in track.Syllables)
        {
            if (syllable.IsMelisma)
            {
                // held vowel — prolong the current word
                durationMs += syllable.DurationMs;
                continue;
            }

            if (wordComplete || text.Length == 0)
            {
                Flush(words, ref text, startMs, durationMs, midi);
                startMs = interpreted.TempoMap.BeatsToMilliseconds(0, syllable.StartBeat);
                durationMs = 0.0;
                midi = syllable.MidiNumber;
                wordComplete = false;
            }

            text += syllable.Text;
            durationMs += syllable.DurationMs;

            if (syllable.IsWordEnd)
                wordComplete = true;
        }

        Flush(words, ref text, startMs, durationMs, midi);
    }

    private static void Flush(List<VocalSpeechWord> words, ref string text, double startMs, double durationMs, int midi)
    {
        if (text.Length > 0)
            words.Add(new VocalSpeechWord(text, startMs, durationMs, midi));

        text = string.Empty;
    }
}

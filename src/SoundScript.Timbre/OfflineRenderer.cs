using System.Text;

namespace SoundScript.Timbre;

/// <summary>
/// Offline timbre rendering facade:
///
///     MIDI → MidiToTimbreTimeline → SpectralEngine → AudioWriter
///
/// MIDI is the single source of truth for pitch, duration, timing, and
/// articulation. SoundCSS supplies spectral styling; phoneme alignment uses
/// optional text, an <c>@phonemes</c> sequence, or signature guessing.
/// </summary>
public static class OfflineRenderer
{
  /// <summary>Default SoundCSS bundled with the timbre engine.</summary>
  public const string DefaultStylesheet = """
    // SoundScript default timbre stylesheet (V4)
    p {
        burst: 12ms;
        noise: 0.3;
        brightness: 0.2;
    }

    aa {
        formant1: 700Hz;
        formant2: 1100Hz;
        smoothness: 0.9;
    }
    """;

  /// <summary>Render options for offline synthesis.</summary>
  public sealed class RenderOptions
  {
    public int SampleRate { get; init; } = MidiToTimbreTimeline.DefaultSampleRate;
    public double FrameMs { get; init; } = MidiToTimbreTimeline.DefaultFrameMs;
    public string? SourceText { get; init; }
    public IReadOnlyList<string>? PhonemeSequence { get; init; }
    public string? PreferredTrackName { get; init; } = SoundScript.Compose.PhraseAssembler.TrackName;
  }

  /// <summary>Renders a MIDI file to WAV/OGG using a SoundCSS stylesheet file.</summary>
  public static void RenderFile(string midiPath, string cssPath, string outputPath, RenderOptions? options = null)
  {
    options ??= new RenderOptions();
    var cssSource = File.ReadAllText(cssPath, Encoding.UTF8);
    Render(midiPath, cssSource, outputPath, options);
  }

  /// <summary>Renders a MIDI file using SoundCSS source text.</summary>
  public static void Render(
    string midiPath,
    string cssSource,
    string outputPath,
    RenderOptions? options = null)
  {
    options ??= new RenderOptions();
        var cssOverrides = SoundCSSParser.ParseOverrides(cssSource);
    var phonemes = ResolvePhonemes(cssSource, options);
    var timeline = MidiToTimbreTimeline.Build(
      midiPath,
      cssOverrides,
      phonemes,
      options.SampleRate,
      options.FrameMs,
      options.PreferredTrackName);

    var samples = SpectralEngine.Synthesize(timeline);
    AudioWriter.Write(outputPath, samples, options.SampleRate);
  }

  /// <summary>Renders from in-memory MIDI bytes (playground / tests).</summary>
  public static byte[] RenderToWavBytes(
    byte[] midiBytes,
    string cssSource = DefaultStylesheet,
    RenderOptions? options = null)
  {
    options ??= new RenderOptions();
    var tempMidi = Path.Combine(Path.GetTempPath(), $"soundscript-{Guid.NewGuid():N}.mid");
  var tempWav = Path.Combine(Path.GetTempPath(), $"soundscript-{Guid.NewGuid():N}.wav");

    try
    {
      File.WriteAllBytes(tempMidi, midiBytes);
      Render(tempMidi, cssSource, tempWav, options);
      return File.ReadAllBytes(tempWav);
    }
    finally
    {
      if (File.Exists(tempMidi))
        File.Delete(tempMidi);
      if (File.Exists(tempWav))
        File.Delete(tempWav);
    }
  }

  /// <summary>Computes SHA-256 of rendered WAV bytes for determinism tests.</summary>
  public static string RenderSha256(
    byte[] midiBytes,
    string cssSource = DefaultStylesheet,
    RenderOptions? options = null)
  {
    var wav = RenderToWavBytes(midiBytes, cssSource, options);
    return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(wav));
  }

  private static IReadOnlyList<string>? ResolvePhonemes(string cssSource, RenderOptions options)
  {
    if (options.PhonemeSequence is not null)
      return options.PhonemeSequence;

    var fromCss = SoundCSSParser.ParsePhonemeSequence(cssSource);
    if (fromCss is not null)
      return fromCss;

    if (!string.IsNullOrWhiteSpace(options.SourceText))
      return PhonemeTimbreMapper.PhonemesFromText(options.SourceText);

    return null;
  }
}

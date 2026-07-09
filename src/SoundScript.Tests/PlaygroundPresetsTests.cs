using SoundScript.Parser;
using SoundScript.Wave;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class PlaygroundPresetsTests
{
  private static readonly string PlaygroundCodePath = Path.GetFullPath(Path.Combine(
      AppContext.BaseDirectory,
      "../../../../../src/SoundScript.Playground/Pages/Playground.razor.cs"));

  [Fact]
  public void BrowserPresets_DoNotUseImport()
  {
    var source = File.ReadAllText(PlaygroundCodePath);
    var presetScripts = ExtractPresetScripts(source);

    Assert.NotEmpty(presetScripts);
    foreach (var script in presetScripts)
    {
      Assert.DoesNotContain("import ", script, StringComparison.OrdinalIgnoreCase);
      Assert.DoesNotContain("import\"", script, StringComparison.OrdinalIgnoreCase);
    }
  }

  [Theory]
  [InlineData("track melody { C4 q }", false)]
  [InlineData("import \"lib.ss\"", true)]
  [InlineData("track melody {\n  import \"lib.ss\"\n}", true)]
  public void SourceDiagnostics_IdentifiesImportUsage(string script, bool expected)
  {
    Assert.Equal(expected, SourceDiagnostics.ContainsImport(script));
  }

  // Regression for the wave-rail wiring: every WaveScriptText preset the
  // Playground offers must actually render through SoundScript.Wave. Catches a
  // preset that was hand-authored against grammar the wave adapter can't take
  // (e.g. the pre-fix jingle-bells example, which threw on `play <pattern>`).
  [Fact]
  public void WavePresets_RenderThroughTheWaveRail()
  {
    var source = File.ReadAllText(PlaygroundCodePath);
    var wavePresets = ExtractAssignedScripts(source, "WaveScriptText =").ToList();

    Assert.NotEmpty(wavePresets);
    foreach (var script in wavePresets)
    {
      var program = new SoundScriptParser(new Tokenizer(script).Tokenize()).Parse();
      var bytes = WaveRenderer.RenderToBytes(program);
      Assert.NotEmpty(bytes);
    }
  }

  // "ScriptText =" is a substring of "WaveScriptText =", so this returns the
  // MIDI-rail presets AND the wave presets — the callers that need only one
  // rail pass the more specific marker.
  internal static IEnumerable<string> ExtractPresetScripts(string playgroundSource) =>
      ExtractAssignedScripts(playgroundSource, "ScriptText =");

  internal static IEnumerable<string> ExtractAssignedScripts(string playgroundSource, string marker)
  {
    var scripts = new List<string>();
    var search = 0;

    while (true)
    {
      var assign = playgroundSource.IndexOf(marker, search, StringComparison.Ordinal);
      if (assign < 0)
        break;

      var open = playgroundSource.IndexOf("\"\"\"", assign, StringComparison.Ordinal);
      if (open < 0)
        break;

      var contentStart = playgroundSource.IndexOf('\n', open) + 1;
      var close = playgroundSource.IndexOf("\"\"\";", contentStart, StringComparison.Ordinal);
      if (close < 0)
        break;

      scripts.Add(playgroundSource[contentStart..close].TrimEnd());
      search = close + 4;
    }

    return scripts;
  }
}

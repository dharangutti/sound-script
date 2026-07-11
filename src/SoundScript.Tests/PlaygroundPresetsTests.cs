using System.Text.RegularExpressions;
using SoundScript.Parser;
using SoundScript.Playground;
using SoundScript.Wave;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public partial class PlaygroundPresetsTests
{
  private static readonly string PlaygroundCodePath = Path.GetFullPath(Path.Combine(
      AppContext.BaseDirectory,
      "../../../../../src/SoundScript.Playground/Pages/Playground.razor.cs"));

  private static readonly string PlaygroundMarkupPath = Path.GetFullPath(Path.Combine(
      AppContext.BaseDirectory,
      "../../../../../src/SoundScript.Playground/Pages/Playground.razor"));

  private static readonly string ExamplesRoot = Path.GetFullPath(Path.Combine(
      AppContext.BaseDirectory,
      "../../../../../examples"));

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
  public void ExampleDropdownKeys_HaveCatalogMetadata()
  {
    var markup = File.ReadAllText(PlaygroundMarkupPath);
    var keys = ExtractOptionValues(markup).Distinct(StringComparer.Ordinal).OrderBy(k => k).ToList();

    Assert.NotEmpty(keys);
    foreach (var key in keys)
    {
      var info = PlaygroundPresetCatalog.TryGet(key);
      Assert.NotNull(info);
      Assert.Equal(key, info!.Key);
      Assert.False(string.IsNullOrWhiteSpace(info.Title));
      Assert.False(string.IsNullOrWhiteSpace(info.PlaygroundAction));
      Assert.NotEmpty(info.CliSteps);
      Assert.All(info.CliSteps, step => Assert.StartsWith(PlaygroundPresetCatalog.CliPrefix, step));
    }
  }

  [Fact]
  public void TextWorkflows_ExposeMultiStepCliWhereNeeded()
  {
    var workflows = PlaygroundPresetCatalog.AllTextWorkflows;

    Assert.Equal(7, workflows.Count);
    Assert.Contains(workflows, w => w.Key == "compose-soundcss" && w.CliSteps.Count == 2);
    Assert.Contains(workflows, w => w.Key == "compose-emit-ss" && w.CliSteps.Count == 2);
    Assert.All(workflows, w => Assert.NotEmpty(w.CliSteps));
  }

  [Fact]
  public void CatalogLinkedExampleFiles_ExistOnDisk()
  {
    foreach (var info in PlaygroundPresetCatalog.AllPresets.Where(p => p.ExampleFile is not null))
    {
      var path = Path.Combine(ExamplesRoot, info.ExampleFile!);
      Assert.True(File.Exists(path), $"Preset '{info.Key}' links to missing examples/{info.ExampleFile}");
    }
  }

  [Fact]
  public void IndexHtml_DoesNotReferenceMissingScopedStylesheet()
  {
    var indexPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "../../../../../src/SoundScript.Playground/wwwroot/index.html"));
    var html = File.ReadAllText(indexPath);
    Assert.DoesNotContain("SoundScript.Playground.styles.css", html);
  }

  [Theory]
  [InlineData("wave-effects", true)]
  [InlineData("speech-only-wave", true)]
  [InlineData("showcase-jingle-bells-wave", true)]
  [InlineData("v2-showcase", false)]
  [InlineData("core-melody", false)]
  public void IsWaveExampleKey_ClassifiesPresetKeys(string key, bool expected) =>
      Assert.Equal(expected, PlaygroundPresetCatalog.IsWaveExampleKey(key));

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

  // Only the example-preset dropdowns (class="example-select") map to catalog
  // entries. Other <select> controls in the page — e.g. the V10 Studio's
  // "Render option" CLI-pattern picker — are not presets and are excluded.
  private static IEnumerable<string> ExtractOptionValues(string razorMarkup)
  {
    foreach (Match select in ExampleSelectRegex().Matches(razorMarkup))
      foreach (Match option in OptionValueRegex().Matches(select.Value))
        yield return option.Groups[1].Value;
  }

  [GeneratedRegex("<select[^>]*example-select[^>]*>.*?</select>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
  private static partial Regex ExampleSelectRegex();

  [GeneratedRegex("option value=\"([^\"]+)\"", RegexOptions.CultureInvariant)]
  private static partial Regex OptionValueRegex();
}

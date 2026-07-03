using SoundScript.Parser;
using Xunit;

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

  internal static IEnumerable<string> ExtractPresetScripts(string playgroundSource)
  {
    var scripts = new List<string>();
    var search = 0;

    while (true)
    {
      var assign = playgroundSource.IndexOf("ScriptText =", search, StringComparison.Ordinal);
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

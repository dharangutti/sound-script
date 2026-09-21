using SoundScript.Core;
using SoundScript.Parser;
using SoundScript.Wave;
using Xunit;

namespace SoundScript.Tests;

public sealed class ImportSandboxTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "soundscript-sandbox-" + Guid.NewGuid().ToString("N"));
    private string Root => Path.Combine(directory, "job");
    private AllowedPathRoot Boundary => new(Root);
    public ImportSandboxTests() => Directory.CreateDirectory(Root);
    private string Write(string relative, string source)
    {
        var path = Path.Combine(directory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, source);
        return path;
    }
    public void Dispose() => Directory.Delete(directory, true);

    [Theory]
    [InlineData("common.ss", "job/common.ss")]
    [InlineData("child/common.ss", "job/child/common.ss")]
    [InlineData("child/../common.ss", "job/common.ss")]
    [InlineData("child\\../common.ss", "job/common.ss")]
    [InlineData("./common", "job/common.ss")]
    public void ImportsWithinRootWork(string import, string target)
    {
        Directory.CreateDirectory(Path.Combine(Root, "child"));
        Write(target, "track tune { C4 q }");
        var entry = Write("job/main.ss", $"import \"{import.Replace("\\", "\\\\")}\"");
        Assert.Single(ProgramLoader.Load(entry, Boundary).Program.Statements);
    }

    [Fact]
    public void NestedParentTraversalWithinRootWorks()
    {
        Write("job/common.ss", "track tune { C4 q }");
        Write("job/child/import.ss", "import \"../common.ss\"");
        var entry = Write("job/main.ss", "import \"child/import.ss\"");
        Assert.Single(ProgramLoader.Load(entry, Boundary).Program.Statements);
    }

    [Theory]
    [InlineData("../outside.ss")]
    [InlineData("child/../../outside.ss")]
    [InlineData("../job2/outside.ss")]
    [InlineData("..\\outside.ss")]
    [InlineData("/outside.ss")]
    [InlineData("C:/outside.ss")]
    [InlineData("C:outside.ss")]
    [InlineData("\\\\server\\share\\outside.ss")]
    public void EscapingAndAbsoluteImportsFail(string import)
    {
        Write("outside.ss", "track bad { C4 q }");
        Write("job2/outside.ss", "track bad { C4 q }");
        var entry = Write("job/main.ss", $"import \"{import.Replace("\\", "\\\\")}\"");
        Assert.Throws<InvalidOperationException>(() => ProgramLoader.Load(entry, Boundary));
    }

    [Fact]
    public void NestedImportCannotEscape()
    {
        Write("outside.ss", "track bad { C4 q }");
        Write("job/child/import.ss", "import \"../../outside.ss\"");
        var entry = Write("job/main.ss", "import \"child/import.ss\"");
        Assert.Throws<InvalidOperationException>(() => ProgramLoader.Load(entry, Boundary));
    }

    [Fact]
    public void EntryMustAlsoBeWithinRoot()
    {
        var entry = Write("job2/entry.ss", "track bad { C4 q }");
        Assert.Throws<InvalidOperationException>(() => ProgramLoader.Load(entry, Boundary));
    }

    [Fact]
    public void TrustedLoaderKeepsParentImports()
    {
        Write("outside.ss", "track tune { C4 q }");
        var entry = Write("job/main.ss", "import \"../outside.ss\"");
        Assert.Single(ProgramLoader.Load(entry).Program.Statements);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RenderOptionsCannotRemoveSampleBoundary(bool stereo)
    {
        var entry = Write("job/main.ss", "sample \"../outside.wav\"");
        var compilation = SoundScriptEngine.CompileFile(entry, Boundary);
        var options = new WaveRenderOptions { SkipMissingSamples = true };
        Assert.Throws<InvalidOperationException>(() => stereo ? compilation.RenderStereoWave(options) : compilation.RenderWave(options));
    }

    [Fact]
    public void SampleAndOverlayPathsUseSameBoundary()
    {
        var entry = Write("job/main.ss", "track tune { C4 q }");
        var compilation = SoundScriptEngine.CompileFile(entry, Boundary);
        Assert.NotEmpty(compilation.RenderWave());
        Assert.Throws<InvalidOperationException>(() => Boundary.Resolve(Root, "../outside.wav"));
        Assert.Throws<InvalidOperationException>(() => Boundary.Resolve(Root, Path.Combine(Root, "sample.wav")));
        Assert.Equal(Boundary.Validate(Path.Combine(Root, "sample.wav")), Boundary.Resolve(Root, "sample.wav"));
    }

    [Theory]
    [InlineData("sample.wav:stream")]
    [InlineData("child./sample.wav")]
    [InlineData("child /sample.wav")]
    public void WindowsAliasesAreRejected(string path)
    {
        if (OperatingSystem.IsWindows())
            Assert.Throws<InvalidOperationException>(() => Boundary.Resolve(Root, path));
        else
            Assert.StartsWith(Boundary.Validate(Root) + Path.DirectorySeparatorChar, Boundary.Resolve(Root, path), StringComparison.Ordinal);
    }

    [Fact]
    public void LinkedImportCannotEscape()
    {
        // Junctions need no symlink privilege on Windows; Unix tests use symlinks.
        var outside = Path.Combine(directory, "outside");
        Directory.CreateDirectory(outside);
        Write("outside/import.ss", "track bad { C4 q }");
        var link = Path.Combine(Root, "link");
        if (OperatingSystem.IsWindows())
        {
            var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false };
            start.ArgumentList.Add("/c"); start.ArgumentList.Add("mklink"); start.ArgumentList.Add("/J");
            start.ArgumentList.Add(link); start.ArgumentList.Add(outside);
            using var process = System.Diagnostics.Process.Start(start)!;
            Assert.True(process.WaitForExit(10_000)); Assert.Equal(0, process.ExitCode);
        }
        else Directory.CreateSymbolicLink(link, outside);
        try
        {
            var entry = Write("job/main.ss", "import \"link/import.ss\"");
            Assert.Throws<InvalidOperationException>(() => ProgramLoader.Load(entry, Boundary));
        }
        finally { Directory.Delete(link); }
    }
}

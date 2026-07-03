using System.Diagnostics;
using SoundScript.Core.Ast;
using SoundScript.Midi;
using SoundScript.Parser;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class ImportTests
{
    [Fact]
    public void Load_MergesImportedTracksAndSequences()
    {
        using var dir = new TempDirectory();
        dir.Write("shared.ss", """
            sequence intro {
                C4 q
            }
            track bass {
                E2 q
            }
            """);
        dir.Write("main.ss", """
            import "shared.ss"
            track melody {
                play intro
                G4 q
            }
            """);

        var loaded = ProgramLoader.Load(dir.FilePath("main.ss"));
        var interpreted = Interpreter.Interpret(loaded.Program);

        Assert.Equal(2, interpreted.Tracks.Count);
        Assert.Contains(interpreted.Tracks, t => t.Name == "melody");
        Assert.Contains(interpreted.Tracks, t => t.Name == "bass");

        var melody = interpreted.Tracks.Single(t => t.Name == "melody");
        Assert.Equal(2, melody.Notes.Count);
    }

    [Fact]
    public void Load_LaterImportOverridesEarlierBlock()
    {
        using var dir = new TempDirectory();
        dir.Write("first.ss", """
            track melody {
                C4 q
            }
            """);
        dir.Write("second.ss", """
            track melody {
                D4 q
            }
            """);
        dir.Write("main.ss", """
            import "first.ss"
            import "second.ss"
            """);

        var loaded = ProgramLoader.Load(dir.FilePath("main.ss"));
        var interpreted = Interpreter.Interpret(loaded.Program);
        var melody = Assert.Single(interpreted.Tracks, t => t.Name == "melody");

        Assert.Single(melody.Notes);
        Assert.Equal(62, melody.Notes[0].MidiNumber);
        Assert.Contains(loaded.Warnings, w => w.Contains("Duplicate block name 'melody'"));
    }

    [Fact]
    public void Load_CurrentFileOverridesImports()
    {
        using var dir = new TempDirectory();
        dir.Write("shared.ss", """
            track melody {
                C4 q
            }
            """);
        dir.Write("main.ss", """
            import "shared.ss"
            track melody {
                E4 q
            }
            """);

        var loaded = ProgramLoader.Load(dir.FilePath("main.ss"));
        var interpreted = Interpreter.Interpret(loaded.Program);
        var melody = Assert.Single(interpreted.Tracks, t => t.Name == "melody");

        Assert.Single(melody.Notes);
        Assert.Equal(64, melody.Notes[0].MidiNumber);
    }

    [Fact]
    public void Load_ResolvesImportChain()
    {
        using var dir = new TempDirectory();
        dir.Write("base.ss", """
            track bass {
                C2 q
            }
            """);
        dir.Write("middle.ss", """
            import "base.ss"
            sequence fill {
                E3 q
            }
            """);
        dir.Write("main.ss", """
            import "middle.ss"
            track melody {
                play fill
            }
            """);

        var loaded = ProgramLoader.Load(dir.FilePath("main.ss"));
        var interpreted = Interpreter.Interpret(loaded.Program);

        Assert.Equal(2, interpreted.Tracks.Count);
        Assert.Contains(interpreted.Tracks, t => t.Name == "bass");
        Assert.Contains(interpreted.Tracks, t => t.Name == "melody");
    }

    [Fact]
    public void Load_PreservesTrackOrder()
    {
        using var dir = new TempDirectory();
        dir.Write("parts.ss", """
            track drums {
                C3 q
            }
            track bass {
                E2 q
            }
            """);
        dir.Write("main.ss", """
            import "parts.ss"
            track melody {
                G4 q
            }
            """);

        var loaded = ProgramLoader.Load(dir.FilePath("main.ss"));
        var trackNames = loaded.Program.Statements
            .OfType<TrackNode>()
            .Select(t => t.Name)
            .ToList();

        Assert.Equal(["drums", "bass", "melody"], trackNames);
    }

    [Fact]
    public void Load_RejectsCircularImport()
    {
        using var dir = new TempDirectory();
        dir.Write("a.ss", """
            import "b.ss"
            track melody { C4 q }
            """);
        dir.Write("b.ss", """
            import "a.ss"
            track bass { E2 q }
            """);

        var ex = Assert.Throws<InvalidOperationException>(() => ProgramLoader.Load(dir.FilePath("a.ss")));
        Assert.Contains("Circular import", ex.Message);
    }

    [Fact]
    public void Load_RejectsAbsoluteImportPath()
    {
        using var dir = new TempDirectory();
        dir.Write("main.ss", """
            import "/etc/passwd.ss"
            """);

        var ex = Assert.Throws<InvalidOperationException>(() => ProgramLoader.Load(dir.FilePath("main.ss")));
        Assert.Contains("relative", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_ImportStatement()
    {
        const string source = """
            import "lib/common.ss"
            track melody { C4 q }
            """;

        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        var import = Assert.IsType<ImportNode>(program.Statements[0]);
        Assert.Equal("lib/common.ss", import.Path);
    }

    [Fact]
    public void Cli_RunsMultiFileProject()
    {
        using var dir = new TempDirectory();
        dir.Write("lib.ss", """
            track melody {
                C4 q
                D4 q
            }
            """);
        dir.Write("main.ss", """
            import "lib.ss"
            """);
        var outputPath = dir.FilePath("output.mid");

        var exitCode = RunCli(dir.FilePath("main.ss"), outputPath);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputPath));
        Assert.True(new FileInfo(outputPath).Length > 0);
    }

    private static int RunCli(string scriptPath, string outputPath)
    {
        var cliProject = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../src/SoundScript.Cli/SoundScript.Cli.csproj"));

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{cliProject}\" --no-build -- run \"{scriptPath}\" \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi)!;
        process.WaitForExit();
        return process.ExitCode;
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "soundscript-test-" + Guid.NewGuid().ToString("N"));

        public TempDirectory()
        {
            Directory.CreateDirectory(Root);
        }

        public void Write(string fileName, string content) =>
            File.WriteAllText(FilePath(fileName), content);

        public string FilePath(string fileName) => System.IO.Path.Combine(Root, fileName);

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}

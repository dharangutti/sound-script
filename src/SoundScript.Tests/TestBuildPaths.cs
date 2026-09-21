using System.Reflection;

namespace SoundScript.Tests;

internal static class TestBuildPaths
{
    private static string Metadata(string key) => typeof(TestBuildPaths).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == key).Value!;

    public static string RepositoryRoot => Metadata("TestRepositoryRoot");
    public static string Configuration => Metadata("TestBuildConfiguration");
    public static string ProjectOutput(string project, string file) => Path.Combine(
        RepositoryRoot, "src", project, "bin", Configuration, Metadata("TestTargetFramework"), file);
    public static string CliDll => ProjectOutput("SoundScript.Cli", "soundscript.dll");
    public static string ProcessFixture => ProjectOutput("SoundScript.Cli.TestFfmpeg",
        "SoundScript.Cli.TestFfmpeg" + (OperatingSystem.IsWindows() ? ".exe" : ""));
}

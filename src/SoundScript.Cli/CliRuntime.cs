namespace SoundScript.Cli;

public static class CliRuntime
{
    public static CancellationToken CancellationToken { get; set; }
    public static CliProgress? Progress { get; set; }
}

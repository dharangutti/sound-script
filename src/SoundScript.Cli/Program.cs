using SoundScript.Cli;
using SoundScript.Core;

CliArguments? invocation = null;
try
{
    if (args.Length == 1 && args[0] is "--version" or "-v")
    {
        Console.WriteLine(VersionInfo.Number);
        return 0;
    }
    if (args.Length > 0 && args[0] == "help")
    {
        CliArguments.PrintHelp(args.Length == 1 ? null : string.Join(" ", args.Skip(1)).ToLowerInvariant());
        return 0;
    }
    if (args.Length > 0 && args[^1] is "--help" or "-h")
    {
        CliArguments.PrintHelp(args.Length == 1 ? null : string.Join(" ", args.SkipLast(1)).ToLowerInvariant());
        return 0;
    }
    invocation = CliArguments.Parse(args);
    return CommandHandlers.Execute(invocation);
}
catch (Exception ex)
{
    var input = invocation?.Input;
    if (input is null && args.Length > 1 && !args[1].StartsWith('-')) input = args[1];
    var diagnostic = Diagnostics.FromException(ex, input);
    bool json = invocation?.Has("json") ?? (args.Length > 0 && (args[0] is "validate" or "inspect" or "video") && args.Contains("--json", StringComparer.OrdinalIgnoreCase));
    if (json) Diagnostics.WriteResult(invocation?.Command ?? args[0], input, [diagnostic]);
    else Console.Error.WriteLine(diagnostic);
    return Diagnostics.ExitCode(ex);
}

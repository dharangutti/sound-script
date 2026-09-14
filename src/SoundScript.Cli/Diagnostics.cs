using System.Text.Json;
using System.Text.RegularExpressions;
using SoundScript.Core;
using SoundScript.Media;

namespace SoundScript.Cli;

public sealed record Diagnostic(string Severity, string Code, string? File, int? Line, int? Column, string Message)
{
    public override string ToString() => $"{File ?? "soundscript"}{(Line is null ? "" : $":{Line}:{Column}")} {Severity} {Code}: {Message}";
}

public sealed record CommandResult(int SchemaVersion, string SoundScriptVersion, bool Success, string Command,
    string? Input, IReadOnlyList<Diagnostic> Diagnostics, object? Metadata, object? Results);

public static class Diagnostics
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public static int ExitCode(Exception ex) => ex switch
    {
        CliUsageException => 2,
        DependencyException or FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException => 3,
        ExportException => 4,
        IOException => 3,
        _ => 1
    };

    public static Diagnostic FromException(Exception ex, string? input)
    {
        var status = ExitCode(ex);
        var location = ex.Data["SoundScript.Location"] as SourceLocation;
        var file = ex.Data["SoundScript.File"] as string ?? location?.File ?? input;
        var match = Regex.Match(ex.Message, @"\bline (\d+), column (\d+)");
        int? line = match.Success ? int.Parse(match.Groups[1].Value) : location?.Line;
        int? column = match.Success ? int.Parse(match.Groups[2].Value) : location?.Column;
        var message = Regex.Replace(ex.Message, @"\s*\(line \d+, column \d+\)\.", "");
        // Environment and usage failures have no invented source position.
        if (status is 2 or 3 or 4) { line = null; column = null; }
        return new Diagnostic("error", status switch { 2 => "SS0002", 3 => "SS0003", 4 => "SS0004", _ => match.Success ? "SS1001" : "SS2001" }, file, line, column, message);
    }

    public static Diagnostic Warning(string message, string? input, SourceLocation? location = null, string code = "SS2002")
    {
        var match = Regex.Match(message, @"^(.*):(\d+): (.*)$");
        if (match.Success)
        {
            input = location?.File ?? match.Groups[1].Value;
            if (location is null && int.Parse(match.Groups[2].Value) > 0)
                location = new SourceLocation(input, int.Parse(match.Groups[2].Value), 1);
            message = match.Groups[3].Value;
        }
        return new Diagnostic("warning", code, location?.File ?? input, location?.Line, location?.Column, message);
    }

    public static void WriteResult(string command, string? input, IReadOnlyList<Diagnostic> diagnostics, object? metadata = null, object? results = null)
        => Console.WriteLine(JsonSerializer.Serialize(new CommandResult(1, VersionInfo.Number, !diagnostics.Any(d => d.Severity == "error"), command, input, diagnostics, metadata, results), Json));
}

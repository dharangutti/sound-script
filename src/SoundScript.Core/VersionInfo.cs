using System.Reflection;

namespace SoundScript.Core;

/// <summary>
/// Reads the release version stamped into this assembly by the root
/// Directory.Build.props (Version, SoundScriptVersionLabel, SoundScriptCodename).
/// That file is the single place to edit for a release; every consumer reads
/// it back through here instead of hardcoding a version string.
/// </summary>
public static class VersionInfo
{
    private static readonly Assembly Assembly = typeof(VersionInfo).Assembly;

    public static string Number { get; } =
        Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    public static string Label { get; } = GetMetadata("SoundScriptVersionLabel") ?? "Unversioned";

    public static string Codename { get; } = GetMetadata("SoundScriptCodename") ?? string.Empty;

    public static string Display => string.IsNullOrEmpty(Codename) ? Label : $"{Label} — {Codename}";

    private static string? GetMetadata(string key) =>
        Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key)?.Value;
}

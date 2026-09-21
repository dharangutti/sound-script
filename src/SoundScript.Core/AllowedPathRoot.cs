namespace SoundScript.Core;

/// <summary>Filesystem boundary for an explicitly sandboxed script and its resources.</summary>
/// <remarks>The host must keep the root and its ancestors stable while a job runs.
/// Links/reparse points below the root are rejected, including links whose targets are inside it.
/// This is a path boundary, not an operating-system sandbox.</remarks>
public sealed class AllowedPathRoot
{
    private readonly string declaredRoot;
    private readonly string physicalRoot;
    private static StringComparison Comparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>Establishes an existing directory as the allowed root.</summary>
    public AllowedPathRoot(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        declaredRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!Directory.Exists(declaredRoot)) throw new DirectoryNotFoundException($"Allowed root does not exist: '{root}'.");
        // Resolve trusted root ancestors (e.g. /var -> /private/var on macOS).
        physicalRoot = CanonicalizeRoot(declaredRoot);
    }

    /// <summary>Checks an entry path or a previously resolved path before opening it.</summary>
    public string Validate(string path)
    {
        var normalized = NormalizeSeparators(path);
        // GetFullPath can erase Windows trailing-dot aliases. Reject ambiguous
        // components before canonicalization as well as checking the final path.
        if (OperatingSystem.IsWindows())
        {
            var volume = Path.GetPathRoot(normalized) ?? "";
            foreach (var segment in normalized[volume.Length..].Split(Path.DirectorySeparatorChar))
                if (segment is not "." and not ".." &&
                    (segment.Contains(':') || segment.EndsWith('.') || segment.EndsWith(' ')))
                    throw new InvalidOperationException($"Ambiguous sandbox path: '{path}'.");
        }
        var full = Path.GetFullPath(normalized);
        var root = IsWithin(declaredRoot, full) ? declaredRoot : physicalRoot;
        if (!IsWithin(root, full)) throw Escape(path);
        var relative = Path.GetRelativePath(root, full);
        var current = physicalRoot;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar))
        {
            if (segment == ".") continue;
            // Windows aliases and alternate data streams must not change path identity.
            if (OperatingSystem.IsWindows() && (segment.Contains(':') || segment.EndsWith('.') || segment.EndsWith(' ')))
                throw new InvalidOperationException($"Ambiguous sandbox path: '{path}'.");
            current = Path.Combine(current, segment);
            var info = new FileInfo(current);
            if (info.LinkTarget is not null || (info.Exists || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"Links/reparse points are not allowed below the sandbox root: '{path}'.");
        }
        return current;
    }

    /// <summary>Resolves a relative import/resource and enforces the established root.</summary>
    public string Resolve(string baseDirectory, string relativePath)
    {
        RequireRelative(relativePath);
        return Validate(Path.Combine(baseDirectory, NormalizeSeparators(relativePath)));
    }

    /// <summary>Rejects absolute, drive-relative, UNC and device paths on every host.</summary>
    public static void RequireRelative(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) ||
            path[0] is '/' or '\\' || (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':'))
            throw new InvalidOperationException($"Path must be relative: '{path}'.");
    }

    private static string NormalizeSeparators(string path) => path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    private static bool IsWithin(string root, string path) => string.Equals(root, path, Comparison) ||
        path.StartsWith(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar, Comparison);
    private static InvalidOperationException Escape(string path) => new($"Path escapes the allowed root: '{path}'.");

    private static string CanonicalizeRoot(string path)
    {
        var volume = Path.GetPathRoot(path)!;
        var current = volume;
        foreach (var segment in path[volume.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            var info = new DirectoryInfo(current);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                current = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName
                    ?? throw new InvalidOperationException($"Cannot resolve allowed root: '{path}'.");
        }
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(current));
    }
}

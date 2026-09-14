namespace SoundScript.Media;

public sealed class DependencyException(string message, Exception? inner = null) : Exception(message, inner);
public sealed class ExportException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Stage next to the destination so the commit is a same-filesystem rename.</summary>
public static class AtomicOutput
{
    public static void ValidatePath(string path)
    {
        var full = Path.GetFullPath(path);
        if (Directory.Exists(full)) throw new IOException($"Output path is a directory: {full}");
        if (File.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReadOnly) != 0)
            throw new UnauthorizedAccessException($"Output is read-only: {full}");
        var parent = Path.GetDirectoryName(full)!;
        while (!Directory.Exists(parent))
        {
            if (File.Exists(parent)) throw new IOException($"Output parent is a file: {parent}");
            parent = Path.GetDirectoryName(parent) ?? throw new DirectoryNotFoundException($"No output parent exists for {full}.");
        }
        // Probe only an existing ancestor; preflight never creates output directories.
        var probe = Path.Combine(parent, $".soundscript-probe-{Guid.NewGuid():N}");
        using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
    }

    public static void Write(string destination, Action<string> generate, Action<string> verify)
    {
        ValidatePath(destination);
        var full = Path.GetFullPath(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temporary = Path.Combine(Path.GetDirectoryName(full)!, $".soundscript-{Guid.NewGuid():N}{Path.GetExtension(full)}");
        try
        {
            generate(temporary);
            if (!File.Exists(temporary) || new FileInfo(temporary).Length == 0)
                throw new ExportException("Export produced no output.");
            verify(temporary);
            File.Move(temporary, full, overwrite: true);
        }
        catch (DependencyException) { throw; }
        catch (Exception ex) { throw new ExportException($"Export failed; destination was not replaced: {ex.Message}", ex); }
        finally { TryDeleteFile(temporary); }
    }

    public static void TryDeleteFile(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    public static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

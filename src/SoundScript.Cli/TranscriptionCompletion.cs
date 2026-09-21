using System.Security.Cryptography;
using System.Text.Json;
using SoundScript.Core;
using SoundScript.Media;

namespace SoundScript.Cli;

/// <summary>A commit marker for a CLI transcription output set, written after every requested output.</summary>
public static class TranscriptionCompletion
{
    public static string ManifestPath(string sourcePath) => Path.GetFullPath(sourcePath) + ".completion.json";

    /// <summary>Removes an earlier marker before this attempt can modify outputs. Failure is fatal.</summary>
    public static void Invalidate(string path)
    {
        AtomicOutput.ValidatePath(path);
        File.Delete(path);
    }

    public static void Write(string path, string mode, IEnumerable<string> outputs, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var files = outputs.Select(output =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = File.OpenRead(output);
            return new { Path = Path.GetRelativePath(directory, Path.GetFullPath(output)), Bytes = stream.Length,
                Sha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant() };
        }).ToArray();
        if (files.Length == 0) throw new InvalidOperationException("Completion requires an output set.");
        cancellationToken.ThrowIfCancellationRequested();
        AtomicOutput.Write(path,
            temporary => File.WriteAllText(temporary, JsonSerializer.Serialize(new
            {
                SchemaVersion = 1, Operation = "transcribe", Mode = mode,
                State = "complete", SoundScriptVersion = VersionInfo.Number, Files = files
            }, Diagnostics.Json)),
            temporary => { using var json = JsonDocument.Parse(File.ReadAllText(temporary)); cancellationToken.ThrowIfCancellationRequested(); });
    }
}

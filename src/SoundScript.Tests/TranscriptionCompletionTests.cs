using System.Security.Cryptography;
using System.Text.Json;
using SoundScript.Cli;
using SoundScript.Media;
using SoundScript.Wave.Io;
using Xunit;

namespace SoundScript.Tests;

public sealed class TranscriptionCompletionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "soundscript-completion-" + Guid.NewGuid().ToString("N"));
    public TranscriptionCompletionTests() => Directory.CreateDirectory(root);
    private string PathFor(string name) => Path.Combine(root, name);
    public void Dispose() => Directory.Delete(root, true);

    [Fact]
    public void SuccessfulCliOutputSetHasMatchingHashes()
    {
        var input = PathFor("input.wav"); var output = PathFor("score.ss");
        WavWriter.Write(input, TranscriptionFixture.All[0].Audio().Samples, 16000);
        Assert.Equal(0, CommandHandlers.Execute(CliArguments.Parse([
            "transcribe", input, "--out", output, "--preview", PathFor("preview.wav"), "--report", PathFor("analysis.json")])));
        using var document = JsonDocument.Parse(File.ReadAllText(TranscriptionCompletion.ManifestPath(output)));
        var manifest = document.RootElement;
        Assert.Equal("complete", manifest.GetProperty("state").GetString());
        Assert.Equal("monophonic", manifest.GetProperty("mode").GetString());
        Assert.Equal(3, manifest.GetProperty("files").GetArrayLength());
        foreach (var file in manifest.GetProperty("files").EnumerateArray())
        {
            var bytes = File.ReadAllBytes(PathFor(file.GetProperty("path").GetString()!));
            Assert.Equal(bytes.LongLength, file.GetProperty("bytes").GetInt64());
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), file.GetProperty("sha256").GetString());
        }
    }

    [Fact]
    public void RejectedAttemptInvalidatesPreviousCompletion()
    {
        var input = PathFor("input.wav"); var output = PathFor("score.ss");
        File.WriteAllText(output, "previous source");
        var manifest = TranscriptionCompletion.ManifestPath(output);
        File.WriteAllText(manifest, "previous marker");
        WavWriter.Write(input, new float[16000], 16000);
        Assert.Throws<InvalidDataException>(() => CommandHandlers.Execute(CliArguments.Parse([
            "transcribe", input, "--out", output, "--report", PathFor("rejected.json")])));
        Assert.False(File.Exists(manifest));
        Assert.Equal("previous source", File.ReadAllText(output));
    }

    [Fact]
    public void FailedLaterOutputDoesNotGetCompletionMarker()
    {
        var output = PathFor("source.ss"); var marker = TranscriptionCompletion.ManifestPath(output);
        File.WriteAllText(marker, "previous marker");
        TranscriptionCompletion.Invalidate(marker);
        AtomicOutput.Write(output, p => File.WriteAllText(p, "track tune { C4 q }"), _ => { });
        Assert.Throws<ExportException>(() => AtomicOutput.Write(PathFor("report.json"), _ => throw new IOException("test disk failure"), _ => { }));
        Assert.False(File.Exists(marker));
        Assert.Throws<FileNotFoundException>(() => TranscriptionCompletion.Write(marker, "monophonic", [output, PathFor("report.json")]));
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public void CancellationDoesNotWriteMarker()
    {
        var output = PathFor("source.ss"); File.WriteAllText(output, "source");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var marker = TranscriptionCompletion.ManifestPath(output);
        Assert.ThrowsAny<OperationCanceledException>(() => TranscriptionCompletion.Write(marker, "monophonic", [output], cancellation.Token));
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public void ManifestCannotAliasRequestedOutput()
    {
        var output = PathFor("score.ss");
        Assert.Throws<CliUsageException>(() => CommandHandlers.Execute(CliArguments.Parse([
            "transcribe", PathFor("input.wav"), "--out", output, "--report", TranscriptionCompletion.ManifestPath(output)])));
    }
}

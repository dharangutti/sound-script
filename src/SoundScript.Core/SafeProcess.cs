using System.Diagnostics;

namespace SoundScript.Core;

/// <summary>Exit status and concurrently drained output from a child process.</summary>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>Runs argument-list processes with a deadline, cancellation and tree cleanup.</summary>
public static class SafeProcess
{
    /// <summary>Runs a process without a shell. The deadline includes redirected stream draining.</summary>
    public static async Task<ProcessResult> RunAsync(string executable, IEnumerable<string> arguments,
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > uint.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        cancellationToken.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Failed to start '{executable}'.");
        using var drains = new CancellationTokenSource();
        var stdout = process.StandardOutput.ReadToEndAsync(drains.Token);
        var stderr = process.StandardError.ReadToEndAsync(drains.Token);
        var completion = Task.WhenAll(process.WaitForExitAsync(), stdout, stderr);
        try
        {
            await completion.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            return new(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { } // Already exited.
            catch (System.ComponentModel.Win32Exception) { } // OS may have reaped it first.
            // Cleanup has its own bound, including descendants that inherited the pipes.
            drains.Cancel();
            try { await completion.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
            catch (Exception cleanup) when (cleanup is TimeoutException or OperationCanceledException or IOException) { }
            if (ex is OperationCanceledException) throw;
            throw new TimeoutException($"Process '{executable}' timed out after {timeout.TotalSeconds:g} seconds.", ex);
        }
    }
}

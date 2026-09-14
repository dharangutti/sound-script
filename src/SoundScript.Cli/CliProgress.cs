using System.Diagnostics;

namespace SoundScript.Cli;

/// <summary>
/// Opt-in, stderr-only progress reporter. It deliberately emits whole lines so
/// logs stay useful in PowerShell, Bash, CI, and redirected child processes.
/// </summary>
public sealed class CliProgress : IDisposable
{
    private readonly Stopwatch _total = Stopwatch.StartNew();
    private readonly bool _enabled;
    private readonly object _gate = new();
    private Stopwatch? _stage;
    private string? _stageName;
    private int _nextPercent;
    private bool _cancelledReported;

    public CliProgress(bool enabled) => _enabled = enabled;
    public bool Enabled => _enabled;
    public TimeSpan Elapsed => _total.Elapsed;

    public void Stage(string name)
    {
        if (!_enabled) return;
        CompleteStage();
        lock (_gate)
        {
            _stageName = name;
            _stage = Stopwatch.StartNew();
            _nextPercent = 5;
            Console.Error.WriteLine($"{name}...");
        }
    }

    public void Detail(string message)
    {
        if (!_enabled) return;
        lock (_gate) Console.Error.WriteLine(message);
    }

    public void Progress(string label, int completed, int total)
    {
        if (!_enabled || total <= 0) return;
        var percent = Math.Clamp((int)Math.Floor(completed * 100d / total), 0, 100);
        lock (_gate)
        {
            if (_cancelledReported) return;
            if (completed != total && percent < _nextPercent) return;
            Console.Error.WriteLine($"{label}: {completed}/{total} {percent}%");
            while (_nextPercent <= percent) _nextPercent += 5;
        }
    }

    public void CompleteStage()
    {
        if (!_enabled) return;
        lock (_gate)
        {
            if (_stageName is null || _stage is null) return;
            if (_cancelledReported)
            {
                _stageName = null;
                _stage = null;
                return;
            }
            Console.Error.WriteLine(FormattableString.Invariant($"{_stageName} completed in {_stage.Elapsed.TotalMilliseconds:0.##} ms."));
            _stageName = null;
            _stage = null;
        }
    }

    public void Completed(string output)
    {
        if (!_enabled) return;
        CompleteStage();
        lock (_gate) if (_cancelledReported) return;
        Console.Error.WriteLine($"Completed:");
        Console.Error.WriteLine(output);
        Console.Error.WriteLine(FormattableString.Invariant($"Total elapsed: {_total.Elapsed.TotalSeconds:0.##} s."));
    }

    public void Cancelled()
    {
        if (!_enabled) return;
        lock (_gate)
        {
            if (_cancelledReported) return;
            _cancelledReported = true;
            Console.Error.WriteLine("Cancellation requested. Cleaning temporary output...");
        }
    }

    /// <summary>Creates a synchronous progress adapter so stage output remains ordered.</summary>
    public IProgress<int> CreateProgress(string label, int total) =>
        new InlineProgress(value => Progress(label, value, total));

    private sealed class InlineProgress(Action<int> report) : IProgress<int>
    {
        public void Report(int value) => report(value);
    }

    public void Dispose()
    {
        CompleteStage();
        if (ReferenceEquals(CliRuntime.Progress, this)) CliRuntime.Progress = null;
    }
}

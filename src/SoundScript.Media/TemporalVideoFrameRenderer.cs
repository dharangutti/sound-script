namespace SoundScript.Media;

/// <summary>
/// Rasterizes the canonical presentation scene for the FFmpeg adapter. It
/// never reads a timeline: callers supply state observations that were already
/// obtained from <c>VisualTimeline.StateAt(t)</c>.
/// </summary>
public static class TemporalVideoFrameRenderer
{
    /// <summary>
    /// Writes canonical numbered PPM frames. Each frame is independent, so a
    /// bounded worker count can be used without changing frame bytes or order.
    /// </summary>
    public static void WritePpmFrames(
        TemporalVideoExportPlan plan,
        string outputDirectory,
        int width = 640,
        int height = 360,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default,
        int jobs = 1)
    {
        ArgumentNullException.ThrowIfNull(plan);
        cancellationToken.ThrowIfCancellationRequested();
        WritePpmFrames(TemporalVisualSceneBuilder.Build(plan), outputDirectory, width, height, progress, cancellationToken, jobs);
    }

    public static void WritePpmFrames(
        TemporalVisualExportPlan plan,
        string outputDirectory,
        int width = 640,
        int height = 360,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default,
        int jobs = 1)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ValidateOutput(outputDirectory, width, height);

        if (jobs is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(jobs), "Frame jobs must be between 1 and 64.");

        Directory.CreateDirectory(outputDirectory);
        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = jobs,
        };
        var completed = 0;
        Parallel.For(0, plan.Samples.Count, options, index =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = RenderPpm(plan.Samples[index], width, height);
            File.WriteAllBytes(Path.Combine(outputDirectory, $"frame-{index:D6}.ppm"), bytes);
            progress?.Report(Interlocked.Increment(ref completed));
        });
    }

    /// <summary>Projects a supplied state observation through the shared scene profile.</summary>
    public static byte[] RenderPpm(TemporalVideoSample sample, int width, int height) =>
        RenderPpm(TemporalVisualSceneBuilder.Build(sample), width, height);

    /// <summary>Renders a canonical scene to portable pixmap bytes for FFmpeg image2 input.</summary>
    public static byte[] RenderPpm(TemporalVisualScene scene, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ValidateDimensions(width, height);

        return SkiaSceneRasterizer.RenderPpm(scene, width, height);
    }

    private static void ValidateOutput(string outputDirectory, int width, int height)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("An output directory is required.", nameof(outputDirectory));
        ValidateDimensions(width, height);
    }

    private static void ValidateDimensions(int width, int height)
    {
        if (width is < 2 or > 3840 || height is < 2 or > 2160 || width % 2 != 0 || height % 2 != 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Video frame dimensions must be even and within 2–3840 × 2–2160.");
    }

}

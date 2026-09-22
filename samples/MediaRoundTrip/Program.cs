using SoundScript;
using SoundScript.Transcription;
using SoundScript.Wave.Io;

if (args.Length > 2 || args is ["--help"])
{
    Console.WriteLine("Usage: MediaRoundTrip [output-directory] [input.wav]");
    return args is ["--help"] ? 0 : 2;
}
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    string root = args.ElementAtOrDefault(0) ?? "artifacts/samples/media-round-trip";
    string output = Path.Combine(root, "Output");
    string input = args.ElementAtOrDefault(1) ?? Path.Combine(root, "Input", "melody.wav");
    // An original four-note sine fixture, independent of SoundScript's synthesizer.
    if (args.Length < 2)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(input)!);
        int rate = AnalysisAudio.SampleRate;
        float[] pcm = new float[rate * 3];
        int[] pitches = [60, 64, 67, 72];
        for (int note = 0; note < pitches.Length; note++)
            for (int i = 0; i < rate / 2; i++)
            {
                double envelope = Math.Min(1, Math.Min(i, rate / 2 - 1 - i) / (rate * 0.01));
                double frequency = 440 * Math.Pow(2, (pitches[note] - 69) / 12.0);
                pcm[note * rate * 3 / 4 + i] = (float)(0.3 * envelope * Math.Sin(2 * Math.PI * frequency * i / rate));
            }
        WavWriter.Write(input, pcm, rate);
    }
    string[] outputs = ["report.json", "transcribed.ss", "modified.ss", "reconstructed.mid", "reconstructed.wav"];
    if (outputs.Any(name => string.Equals(Path.GetFullPath(input), Path.GetFullPath(Path.Combine(output, name)),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))
        throw new ArgumentException("Input must be separate from output files.");
    Directory.CreateDirectory(output);
    // Invalidate prior successful artifacts before attempting a new transcription.
    foreach (string name in outputs) File.Delete(Path.Combine(output, name));
    var audio = PcmWaveInput.Decode(File.ReadAllBytes(input));
    var result = await new TranscriptionEngine().TranscribeAsync(audio,
        new TranscriptionOptions(Tempo: 120, Instrument: 73), TranscriptionMode.Monophonic, cancellation.Token);
    File.WriteAllText(Path.Combine(output, "report.json"), new AnalysisJsonOutput().Write(result));
    if (!result.Suitability.CanGenerate)
        throw new InvalidDataException(result.Suitability.Reason + " See Output/report.json; no reconstruction generated.");

    var score = TranscriptionSuitability.ScoreForGeneration(result);
    var writer = new SoundScriptOutput();
    // Wave applies instrument timbres in expressive mode; use it for both versions.
    File.WriteAllText(Path.Combine(output, "transcribed.ss"), "perform expressive\n" + writer.Source(score));
    // Change the editable musical model: flute to piano. Preserve observations and source timing.
    var modified = score with { Tracks = score.Tracks.Select(track => track with { Instrument = 0 }).ToArray() };
    string source = "perform expressive\n" + writer.Source(modified);
    File.WriteAllText(Path.Combine(output, "modified.ss"), source);
    var compilation = SoundScriptEngine.Compile(source);
    File.WriteAllBytes(Path.Combine(output, "reconstructed.mid"), compilation.RenderMidi());
    File.WriteAllBytes(Path.Combine(output, "reconstructed.wav"), compilation.RenderWave());
    Console.WriteLine($"{result.Suitability.Status}: {score.Tracks.Sum(track => track.Notes.Count)} notes; flute → piano. {Path.GetFullPath(output)}");
    return 0;
}
catch (OperationCanceledException) { Console.Error.WriteLine("Cancelled."); return 130; }
catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }

using System.Globalization;
using System.Text.RegularExpressions;
using SoundScript.Labs.IR;
using SoundScript.Labs.Syntax;

namespace SoundScript.Labs.Compilation;

public static class LabsCompiler
{
    public static ExperimentIr Compile(ExperimentAst ast)
    {
        var waveform = ast.Waveform switch
        {
            "sine" => Waveform.Sine, "square" => Waveform.Square,
            "chirp" => Waveform.Chirp, "sweep" => Waveform.Sweep,
            _ => throw new LabsException($"Unsupported waveform '{ast.Waveform}'.")
        };
        bool swept = waveform is Waveform.Chirp or Waveform.Sweep;
        string[] allowed = swept ? ["start", "end", "duration", "sample_rate", "amplitude"]
            : ["frequency", "duration", "sample_rate", "amplitude"];
        foreach (var key in ast.Parameters.Keys)
            if (!allowed.Contains(key)) throw new LabsException($"Unsupported parameter '{key}'.");
        decimal Read(string key, string dimension, decimal? fallback = null)
        {
            if (!ast.Parameters.TryGetValue(key, out var token))
                return fallback ?? throw new LabsException($"Missing '{key}'.");
            var match = Regex.Match(token.Text, @"\A([0-9]+(?:\.[0-9]+)?)(Hz|kHz|s|ms)?\z");
            if (!match.Success || !decimal.TryParse(match.Groups[1].Value, NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var value))
                throw new LabsException($"Line {token.Line}, column {token.Column}: Invalid quantity '{token.Text}'.");
            string unit = match.Groups[2].Value;
            decimal factor = (dimension, unit) switch
            {
                ("frequency", "Hz") => 1, ("frequency", "kHz") => 1000,
                ("time", "s") => 1, ("time", "ms") => .001m, ("scalar", "") => 1,
                _ => throw new LabsException($"Wrong unit for '{key}': '{token.Text}'.")
            };
            try { return value * factor; }
            catch (OverflowException) { throw new LabsException($"Quantity '{key}' is too large."); }
        }

        decimal rate = Read("sample_rate", "frequency", 48_000);
        decimal duration = Read("duration", "time");
        if (rate is < 1 or > 384_000 || rate != decimal.Truncate(rate))
            throw new LabsException("sample_rate must be an integer Hz in [1, 384000].");
        if (duration <= 0 || duration > ExperimentIr.MaxSamples / rate)
            throw new LabsException("duration exceeds the sample budget or is not positive.");
        decimal frames = rate * duration;
        if (frames < 1 || frames != decimal.Truncate(frames))
            throw new LabsException("duration * sample_rate must be a positive whole number of samples.");
        double start = (double)Read(swept ? "start" : "frequency", "frequency");
        double end = swept ? (double)Read("end", "frequency") : start;
        AnalysisKind analysis = AnalysisKind.None;
        foreach (var token in ast.Analyses)
        {
            var flag = token.Text switch
            {
                "fft" => AnalysisKind.Fft, "peaks" => AnalysisKind.Peaks,
                "rms" => AnalysisKind.Rms, "frequency" => AnalysisKind.Frequency,
                _ => throw new LabsException($"Unsupported analysis '{token.Text}'.")
            };
            if (analysis.HasFlag(flag)) throw new LabsException($"Duplicate analysis '{token.Text}'.");
            analysis |= flag;
        }
        ExportKind exports = 0;
        foreach (var token in ast.Exports)
        {
            var flag = token.Text switch
            {
                "json" => ExportKind.Json, "wav" => ExportKind.Wav,
                "pcm" => ExportKind.Pcm, "float32" => ExportKind.Float32,
                _ => throw new LabsException($"Unsupported export '{token.Text}'.")
            };
            if (exports.HasFlag(flag)) throw new LabsException($"Duplicate export '{token.Text}'.");
            exports |= flag;
        }
        var ir = new ExperimentIr(ExperimentIr.CurrentVersion, ast.Name,
            new(waveform, start, end, (int)rate, (int)frames, (double)Read("amplitude", "scalar", 1)), analysis, exports);
        try { ir.Validate(); }
        catch (ArgumentException ex) { throw new LabsException(ex.Message); }
        return ir;
    }
}

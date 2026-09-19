using SoundScript.Labs.Analysis;
using SoundScript.Labs.Compilation;
using SoundScript.Labs.Execution;
using SoundScript.Labs.IR;
using SoundScript.Labs.Output;
using SoundScript.Labs.Syntax;

namespace SoundScript.Labs;

public sealed record ExperimentRun(string SourceSha256, string Backend,
    ExperimentIr Experiment, SampleBuffer Buffer, AnalysisResult Analysis);

public static class ExperimentRunner
{
    public const string EngineVersion = "0.1.0-labs";

    public static ExperimentRun Run(string source, IExperimentBackend? backend = null)
    {
        var ir = LabsCompiler.Compile(LabsParser.Parse(source));
        backend ??= new Simulator();
        var samples = backend.Execute(ir);
        var analysis = AnalysisEngine.Analyze(ir, samples);
        return new(ResultExporter.Hash(System.Text.Encoding.UTF8.GetBytes(source)), backend.Id, ir, samples, analysis);
    }
}

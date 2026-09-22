using SoundScript.Core.Ast;
using SoundScript.Core;
using SoundScript.Midi;
using SoundScript.Parser;
using SoundScript.Wave;
using SoundScript.Media;

namespace SoundScript;

/// <summary>Compiles SoundScript source for the existing deterministic renderers.</summary>
/// <remarks>Compilation parses source; renderer-specific validation occurs when rendering.
/// No CLI process is started. Use <see cref="CompileFile(string)"/> for filesystem imports.</remarks>
public static class SoundScriptEngine
{
    /// <summary>Parses an in-memory script without resolving filesystem imports.</summary>
    /// <param name="source">SoundScript text, including .ss, .ssw or .ssv syntax.</param>
    /// <returns>A reusable compilation supporting WAV and MIDI rendering.</returns>
    /// <exception cref="ArgumentNullException">Source is null.</exception>
    /// <exception cref="NotSupportedException">Source contains imports; use CompileFile instead.</exception>
    /// <exception cref="InvalidOperationException">The existing parser rejects invalid source.</exception>
    public static SoundScriptCompilation Compile(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var program = new Parser.Parser(new Tokenizer(source).Tokenize()).Parse();
        if (program.Statements.Any(statement => statement is ImportNode))
            throw new NotSupportedException("Use SoundScriptEngine.CompileFile to resolve imports.");
        return new(program, null, []);
    }

    /// <summary>Loads a script and its relative import graph using the existing program loader.</summary>
    /// <param name="path">Entry script path; relative paths use the current directory.</param>
    /// <returns>A compilation with loader warnings and the entry directory for audio samples.</returns>
    /// <exception cref="FileNotFoundException">The entry script or an import is missing.</exception>
    /// <exception cref="InvalidOperationException">An import cycle or invalid source is encountered.</exception>
    /// <remarks>Imports and sample paths access the local filesystem. Only load trusted scripts.</remarks>
    public static SoundScriptCompilation CompileFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var loaded = ProgramLoader.Load(fullPath);
        return new(loaded.Program, Path.GetDirectoryName(fullPath), loaded.Warnings.AsReadOnly());
    }

    /// <summary>Compiles a file with imports and filesystem samples constrained to an explicit root.</summary>
    /// <remarks>The host must keep the root stable for the lifetime of this compilation.</remarks>
    public static SoundScriptCompilation CompileFile(string path, AllowedPathRoot allowedRoot)
    {
        ArgumentNullException.ThrowIfNull(allowedRoot);
        var fullPath = allowedRoot.Validate(path);
        var loaded = ProgramLoader.Load(fullPath, allowedRoot);
        return new(loaded.Program, Path.GetDirectoryName(fullPath), loaded.Warnings.AsReadOnly(), allowedRoot);
    }
}

/// <summary>A parsed script rendered through SoundScript's existing WAV and MIDI pipelines.</summary>
/// <remarks>The AST is kept private. Output-specific errors are deferred until rendering;
/// compilation alone does not prove that every backend supports the script.</remarks>
public sealed class SoundScriptCompilation
{
    private readonly ProgramNode program;
    private readonly string? scriptDirectory;
    private readonly AllowedPathRoot? allowedRoot;

    internal SoundScriptCompilation(ProgramNode program, string? scriptDirectory, IReadOnlyList<string> warnings, AllowedPathRoot? allowedRoot = null)
    {
        this.program = program;
        this.scriptDirectory = scriptDirectory;
        this.allowedRoot = allowedRoot;
        Warnings = warnings;
    }

    /// <summary>Import-loader warnings, including duplicate blocks overridden by later definitions.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Compiles the existing visual timeline alongside this program's deterministic audio.</summary>
    /// <remarks>Filesystem assets retain this compilation's import/sample sandbox. No playback clock is created.</remarks>
    public SoundScriptMediaCompilation CompileMedia() => new(program, RenderOptions(null));

    /// <summary>Renders a complete mono PCM WAV using the existing direct AST renderer.</summary>
    /// <returns>RIFF/WAVE bytes ready to save or return from an application.</returns>
    public byte[] RenderWave() => RenderWave(options: null);

    /// <summary>Renders a complete mono PCM WAV using explicit renderer options.</summary>
    /// <param name="options">Sample resolution and overlay inputs; omitted options use the entry file's directory.</param>
    /// <returns>RIFF/WAVE bytes ready to save or return from an application.</returns>
    /// <remarks>Explicit options replace the default options, including ScriptDirectory.
    /// Determinism assumes identical source, external assets, options, and renderer version.</remarks>
    public byte[] RenderWave(WaveRenderOptions? options) =>
        WaveRenderer.RenderToBytes(program, RenderOptions(options));

    /// <summary>Renders a complete stereo PCM WAV using supported track panning.</summary>
    /// <returns>A stereo RIFF/WAVE file in memory.</returns>
    public byte[] RenderStereoWave() => RenderStereoWave(options: null);

    /// <summary>Renders a complete stereo PCM WAV using explicit renderer options.</summary>
    /// <param name="options">Sample resolution and overlay inputs, or entry-file defaults.</param>
    /// <returns>A stereo RIFF/WAVE file in memory.</returns>
    public byte[] RenderStereoWave(WaveRenderOptions? options) =>
        WaveRenderer.RenderStereoToBytes(program, RenderOptions(options));

    private WaveRenderOptions RenderOptions(WaveRenderOptions? options)
    {
        options ??= new() { ScriptDirectory = scriptDirectory };
        if (allowedRoot is null) return options;
        // Explicit render options cannot remove a compilation's sandbox boundary.
        return new()
        {
            AllowedRoot = allowedRoot,
            ScriptDirectory = options.ScriptDirectory ?? scriptDirectory,
            AdditionalSampleOverlays = options.AdditionalSampleOverlays,
            ExternalOverlays = options.ExternalOverlays,
            SkipMissingSamples = options.SkipMissingSamples,
            SuppressSyntheticSpeak = options.SuppressSyntheticSpeak
        };
    }

    /// <summary>Interprets pitched musical events and writes a standard MIDI file.</summary>
    /// <returns>Standard MIDI file bytes, starting with the MThd header.</returns>
    /// <exception cref="NotSupportedException">The script uses unsupported MIDI features, including unpitched hit events.</exception>
    /// <remarks>Uses the same interpreter and writer as the CLI. WAV-only features are not converted into invented MIDI notes.</remarks>
    public byte[] RenderMidi()
    {
        using var output = new MemoryStream();
        MidiGenerator.Write(Interpreter.Interpret(program), output);
        return output.ToArray();
    }
}

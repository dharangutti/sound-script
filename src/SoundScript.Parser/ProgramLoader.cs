using SoundScript.Core.Ast;
using SoundScript.Core;

namespace SoundScript.Parser;

/// <summary>Parsed program plus diagnostics collected while resolving imports.</summary>
public sealed class LoadResult
{
    /// <summary>The merged program after all relative imports have been loaded.</summary>
    public ProgramNode Program { get; set; } = new();
    /// <summary>Human-readable warnings, including duplicate block replacements.</summary>
    public List<string> Warnings { get; } = [];
    /// <summary>Warnings paired with their source locations when available.</summary>
    public List<(string Message, SourceLocation? Location)> SourceWarnings { get; } = [];
}

/// <summary>Loads a SoundScript entry file and recursively merges its relative imports.</summary>
public static class ProgramLoader
{
    /// <summary>Loads and parses an entry file and all of its relative imports.</summary>
    /// <param name="entryPath">Path to the root <c>.ss</c> source file.</param>
    /// <returns>The merged program and any non-fatal load warnings.</returns>
    /// <exception cref="FileNotFoundException">The entry file or an imported file does not exist.</exception>
    /// <exception cref="InvalidOperationException">An import is absolute/empty or an import cycle is detected.</exception>
    public static LoadResult Load(string entryPath)
    {
        var fullEntryPath = Path.GetFullPath(entryPath);
        var result = new LoadResult();
        var loading = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var merged = new MergedProgram();

        LoadFile(fullEntryPath, merged, result, loading);

        result.Program = merged.ToProgramNode();
        return result;
    }

    private static void LoadFile(
        string filePath,
        MergedProgram merged,
        LoadResult result,
        HashSet<string> loading)
    {
        if (!loading.Add(filePath))
            throw new InvalidOperationException($"Circular import detected: '{filePath}'.");

        try
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Import file not found: '{filePath}'.", filePath);

            var source = File.ReadAllText(filePath);
            var program = new Parser(new Tokenizer(source).Tokenize()) { SourceFile = filePath }.Parse();
            var baseDirectory = Path.GetDirectoryName(filePath) ?? ".";

            foreach (var statement in program.Statements)
            {
                if (statement is ImportNode import)
                {
                    var resolvedPath = ResolveImportPath(import.Path, baseDirectory);
                    try { LoadFile(resolvedPath, merged, result, loading); }
                    catch (Exception ex) { SourceLocation.Attach(ex, import); throw; }
                    continue;
                }

                merged.Add(statement, result);
            }
        }
        catch (Exception ex)
        {
            if (!ex.Data.Contains("SoundScript.File")) ex.Data["SoundScript.File"] = filePath;
            throw;
        }
        finally
        {
            loading.Remove(filePath);
        }
    }

    private static string ResolveImportPath(string importPath, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(importPath))
            throw new InvalidOperationException("Import path cannot be empty.");

        if (Path.IsPathRooted(importPath))
            throw new InvalidOperationException($"Import path must be relative: '{importPath}'.");

        var combined = Path.GetFullPath(Path.Combine(baseDirectory, importPath));

        if (!combined.EndsWith(".ss", StringComparison.OrdinalIgnoreCase)
            && File.Exists(combined + ".ss"))
        {
            combined += ".ss";
        }

        return combined;
    }

    private sealed class MergedProgram
    {
        private readonly List<AstNode> _statements = [];
        private readonly Dictionary<string, int> _blockIndices = new(StringComparer.OrdinalIgnoreCase);

        public void Add(AstNode statement, LoadResult result)
        {
            var blockName = GetBlockName(statement);
            if (blockName is not null)
            {
                if (_blockIndices.TryGetValue(blockName, out var index))
                {
                    var warning = $"Duplicate block name '{blockName}' — later definition overrides earlier.";
                    result.Warnings.Add(warning);
                    result.SourceWarnings.Add((warning, SourceLocation.For(statement)));
                    _statements[index] = statement;
                    return;
                }

                _blockIndices[blockName] = _statements.Count;
            }

            _statements.Add(statement);
        }

        public ProgramNode ToProgramNode()
        {
            var program = new ProgramNode();
            program.Statements.AddRange(_statements);
            return program;
        }

        private static string? GetBlockName(AstNode statement) => statement switch
        {
            TrackNode track => track.Name,
            VoiceNode voice => $"voice:{voice.Name}",
            SequenceNode sequence => sequence.Name,
            BlockNode block => block.Name,
            PatternNode pattern => pattern.Name,
            MelodyNode => "melody",
            _ => null
        };
    }
}

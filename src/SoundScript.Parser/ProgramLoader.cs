using SoundScript.Core.Ast;

namespace SoundScript.Parser;

public sealed class LoadResult
{
    public ProgramNode Program { get; set; } = new();
    public List<string> Warnings { get; } = [];
}

public static class ProgramLoader
{
    public static LoadResult Load(string entryPath)
    {
        var fullEntryPath = Path.GetFullPath(entryPath);
        var result = new LoadResult();
        var loading = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var merged = new MergedProgram();

        LoadFile(fullEntryPath, merged, result.Warnings, loading);

        result.Program = merged.ToProgramNode();
        return result;
    }

    private static void LoadFile(
        string filePath,
        MergedProgram merged,
        List<string> warnings,
        HashSet<string> loading)
    {
        if (!loading.Add(filePath))
            throw new InvalidOperationException($"Circular import detected: '{filePath}'.");

        try
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Import file not found: '{filePath}'.");

            var source = File.ReadAllText(filePath);
            var program = new Parser(new Tokenizer(source).Tokenize()).Parse();
            var baseDirectory = Path.GetDirectoryName(filePath) ?? ".";

            foreach (var statement in program.Statements)
            {
                if (statement is ImportNode import)
                {
                    var resolvedPath = ResolveImportPath(import.Path, baseDirectory);
                    LoadFile(resolvedPath, merged, warnings, loading);
                    continue;
                }

                merged.Add(statement, warnings);
            }
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

        public void Add(AstNode statement, List<string> warnings)
        {
            var blockName = GetBlockName(statement);
            if (blockName is not null)
            {
                if (_blockIndices.TryGetValue(blockName, out var index))
                {
                    warnings.Add($"Duplicate block name '{blockName}' — later definition overrides earlier.");
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
            SequenceNode sequence => sequence.Name,
            BlockNode block => block.Name,
            MelodyNode => "melody",
            _ => null
        };
    }
}

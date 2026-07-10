namespace SoundScript.Wordbank.Models;

public sealed class CorpusManifestDocument
{
    public string Id { get; init; } = "";
    public int Version { get; init; }
    public string Status { get; init; } = "";
    public string? Description { get; init; }
    public CorpusLocaleRef[] Locales { get; init; } = [];
}

public sealed class CorpusLocaleRef
{
    public string Code { get; init; } = "";
    public string Path { get; init; } = "";
    public string LemmaFile { get; init; } = "";
    public string? PilotFile { get; init; }
}

public sealed class CorpusLemmasDocument
{
    public string CorpusId { get; init; } = "";
    public string Locale { get; init; } = "";
    public int Version { get; init; }
    public string Status { get; init; } = "";
    public string? Description { get; init; }
    public CorpusLemmaEntry[] Entries { get; init; } = [];
}

public sealed class CorpusLemmaEntry
{
    public string Lemma { get; init; } = "";
    public string? License { get; init; }
    public string? Source { get; init; }
    public string? Attribution { get; init; }
    public string? Audio { get; init; }
    public double TrimStartMs { get; init; }
    public double? TrimEndMs { get; init; }
    public double Gain { get; init; } = 1.0;
    public double PitchSemitones { get; init; }
}

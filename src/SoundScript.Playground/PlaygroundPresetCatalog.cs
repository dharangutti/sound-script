namespace SoundScript.Playground;

/// <summary>Which output rail a preset or workflow uses.</summary>
public enum PlaygroundOutputRail
{
    Midi,
    Wave,
    WaveAutoDetect,
    SoundCss,
    TextCompose,
    TextProsody,
    PlaygroundOnly,
}

/// <summary>Metadata linking a Playground preset to on-disk examples and CLI commands.</summary>
public sealed record PlaygroundPresetInfo(
    string Key,
    string Title,
    PlaygroundOutputRail OutputRail,
    string? ExampleFile,
    string PlaygroundAction,
    string? Notes,
    IReadOnlyList<string> CliSteps)
{
    public string OutputRailLabel => OutputRail switch
    {
        PlaygroundOutputRail.Midi => "MIDI (.ss → .mid)",
        PlaygroundOutputRail.Wave => "Wave (.ss/.ssw → .wav)",
        PlaygroundOutputRail.WaveAutoDetect => "Wave auto (Run detects effect/speak)",
        PlaygroundOutputRail.SoundCss => "SoundCSS (MIDI → WAV)",
        PlaygroundOutputRail.TextCompose => "Text → MIDI (compose)",
        PlaygroundOutputRail.TextProsody => "Text → MIDI (prosody)",
        PlaygroundOutputRail.PlaygroundOnly => "Playground only",
        _ => OutputRail.ToString(),
    };

    public string? ExampleRepoUrl =>
        ExampleFile is null ? null : PlaygroundPresetCatalog.ExampleRepoUrl(ExampleFile);

    public string DocsUrl => PlaygroundPresetCatalog.DocsExamplesUrl;

    public string OutputRailShortLabel => PlaygroundPresetCatalog.DescribeOutputRail(OutputRail);
}

/// <summary>Text-to-Melody button workflows (multi-step CLI where applicable).</summary>
public sealed record PlaygroundTextWorkflowInfo(
    string Key,
    string Title,
    PlaygroundOutputRail OutputRail,
    string PlaygroundButton,
    IReadOnlyList<string> CliSteps,
    string? Notes)
{
    public string OutputRailShortLabel => PlaygroundPresetCatalog.DescribeOutputRail(OutputRail);
}

public static class PlaygroundPresetCatalog
{
    public const string CliPrefix = "dotnet run --project src/SoundScript.Cli -- ";
    public const string DocsExamplesPath = "examples.md";
    public const string DefaultComposeText = "Twinkle twinkle little star";

    private static readonly Dictionary<string, PlaygroundPresetInfo> Presets =
        BuildPresets().ToDictionary(p => p.Key, StringComparer.Ordinal);

    private static readonly IReadOnlyList<PlaygroundTextWorkflowInfo> TextWorkflows =
    [
        new(
            "compose-midi",
            "Compose from text → MIDI",
            PlaygroundOutputRail.TextCompose,
            "Compose from text",
            [$"{CliPrefix}compose \"{DefaultComposeText}\""],
            "Playground button writes the same MIDI bytes as this CLI command."),
        new(
            "prosody-midi",
            "Compose with Prosody → MIDI",
            PlaygroundOutputRail.TextProsody,
            "Compose with Prosody",
            [$"{CliPrefix}prosody \"{DefaultComposeText}\""],
            "Word-level pitch contour (V5 ProsodyComposer)."),
        new(
            "compose-soundcss",
            "Render Audio (SoundCSS timbre)",
            PlaygroundOutputRail.SoundCss,
            "Render Audio",
            [
                $"{CliPrefix}compose \"{DefaultComposeText}\" twinkle.mid",
                $"{CliPrefix}render twinkle.mid --css examples/default.ssc --out twinkle.wav --text \"{DefaultComposeText}\"",
            ],
            "Playground uses embedded rules equivalent to examples/default.ssc."),
        new(
            "prosody-soundcss",
            "Render Audio (Prosody + SoundCSS)",
            PlaygroundOutputRail.SoundCss,
            "Render Audio (Prosody)",
            [
                $"{CliPrefix}prosody \"{DefaultComposeText}\" twinkle.mid",
                $"{CliPrefix}render twinkle.mid --css examples/default.ssc --out twinkle.wav --text \"{DefaultComposeText}\"",
            ],
            "Same SoundCSS pass; MIDI comes from ProsodyComposer (prosody track)."),
        new(
            "compose-wave",
            "Render Wave (text → WAV, no MIDI)",
            PlaygroundOutputRail.Wave,
            "Render Wave",
            [$"{CliPrefix}compose \"{DefaultComposeText}\" twinkle.wav --wave"],
            "Skips the MIDI step entirely (V7)."),
        new(
            "prosody-wave",
            "Render Wave (Prosody, no MIDI)",
            PlaygroundOutputRail.Wave,
            "Render Wave (Prosody)",
            [$"{CliPrefix}prosody \"{DefaultComposeText}\" twinkle-prosody.wav --wave"],
            "Prosody-shaped melody rendered through SoundScript.Wave."),
        new(
            "compose-emit-ss",
            "Compose → edit .ss → MIDI",
            PlaygroundOutputRail.TextCompose,
            "View .ss source (after Compose)",
            [
                $"{CliPrefix}compose \"{DefaultComposeText}\" --emit-ss twinkle.ss",
                $"{CliPrefix}run twinkle.ss twinkle-viass.mid",
            ],
            "After composing in Playground, use View .ss source / Download .ss, then run via CLI."),
    ];

    public static IReadOnlyList<PlaygroundTextWorkflowInfo> AllTextWorkflows => TextWorkflows;

    public static PlaygroundPresetInfo? TryGet(string key) =>
        Presets.TryGetValue(key, out var info) ? info : null;

    public static IEnumerable<PlaygroundPresetInfo> AllPresets => Presets.Values;

    public static bool IsWaveExampleKey(string key) =>
        key.StartsWith("wave-", StringComparison.Ordinal) ||
        key is "speech-only-wave" or "showcase-jingle-bells-wave";

    public static string DocsExamplesUrl => $"/doc.html?p={DocsExamplesPath}";

    public static string DescribeOutputRail(PlaygroundOutputRail rail) => rail switch
    {
        PlaygroundOutputRail.Midi => "MIDI",
        PlaygroundOutputRail.Wave => "Wave",
        PlaygroundOutputRail.WaveAutoDetect => "Wave (auto)",
        PlaygroundOutputRail.SoundCss => "SoundCSS",
        PlaygroundOutputRail.TextCompose => "compose",
        PlaygroundOutputRail.TextProsody => "prosody",
        PlaygroundOutputRail.PlaygroundOnly => "Playground",
        _ => rail.ToString(),
    };

    public static string ExampleRepoUrl(string exampleFile) =>
        $"https://github.com/dharangutti/sound-script/blob/main/examples/{exampleFile}";

    private static IEnumerable<PlaygroundPresetInfo> BuildPresets()
    {
        yield return DefaultPreset();

        foreach (var p in MidiPresets())
            yield return p;

        foreach (var p in WavePresets())
            yield return p;

        foreach (var p in ShowcasePresets())
            yield return p;
    }

    private static PlaygroundPresetInfo DefaultPreset() =>
        new(
            "default",
            "Default script",
            PlaygroundOutputRail.Midi,
            null,
            "Reset",
            "Inline starter script — no matching file in examples/.",
            [$"{CliPrefix}run your-script.ss"]);

    private static IEnumerable<PlaygroundPresetInfo> MidiPresets()
    {
        yield return Midi("v2-showcase", "Showcase", "full-v2-showcase.ss",
            "Inline variant of the V2 showcase (imports omitted — browser cannot load import).");
        yield return Midi("v2-blocks", "Blocks", "blocks.ss");
        yield return Midi("v2-metadata", "Metadata", "metadata.ss");
        yield return Midi("v2-tempo", "Tempo", "tempo-automation.ss");
        yield return Midi("v2-layers", "Layers", "layers.ss");
        yield return Midi("v2-humanize", "Humanize", "humanization.ss");
        yield return Midi("v2-chords", "Chords+", "advanced-chords.ss");
        yield return Midi("v2-phrases", "Phrases", "phrases.ss");
        yield return Midi("v2-phrases-v3", "Phrases V3", "phrases-v3.ss",
            "Playground loads a subset of the on-disk example.");
        yield return Midi("v2-patterns", "Patterns", "patterns.ss");
        yield return Midi("v2-orchestration", "Orchestration", "orchestration.ss");
        yield return Midi("v2-voice", "Voice", "vocal-song.ss",
            "Browser speech overlay via Web Speech API; MIDI carries lyric events.");
        yield return Midi("core-melody", "Melody", "melody.ss");
        yield return Midi("core-articulations", "Articulations", "articulations.ss");
        yield return Midi("core-dynamics", "Dynamics", "dynamics.ss");
        yield return Midi("core-chords", "Chords", "chords.ss");
        yield return Midi("core-intelligence", "Intelligence", "phrase-smoothing.ss",
            "Phrase smoothing + sequence ideas; see also sequences.ss and melodic-contour.ss in examples/.");
        yield return Midi("core-multitrack", "Multi-track", "multitrack.ss");
        yield return Midi("core-playback", "Playback", "playback-shaping.ss");
    }

    private static IEnumerable<PlaygroundPresetInfo> WavePresets()
    {
        yield return Wave(
            "wave-effects",
            "Effects (delay + filter)",
            null,
            "Run (main) or Render (Wave pane)",
            PlaygroundOutputRail.WaveAutoDetect,
            "Playground-only subset (melody + effects). Compare with examples/wave-effects.ssw for the full combined demo.",
            [$"{CliPrefix}wave examples/wave-effects.ssw output.wav"]);

        yield return Wave(
            "wave-speak",
            "Speak (prosody tone)",
            "wave-speak.ssw",
            "Run (main) or Render (Wave pane)",
            PlaygroundOutputRail.WaveAutoDetect,
            null,
            [$"{CliPrefix}wave examples/wave-speak.ssw output.wav"]);

        yield return Wave(
            "wave-humanize",
            "Seeded humanize + speak",
            "wave-humanize.ssw",
            "Run (main) or Render (Wave pane)",
            PlaygroundOutputRail.WaveAutoDetect,
            null,
            [$"{CliPrefix}wave examples/wave-humanize.ssw output.wav"]);

        yield return Wave(
            "wave-effects-combined",
            "Combined (wave-effects.ssw)",
            "wave-effects.ssw",
            "Run (main) or Render (Wave pane)",
            PlaygroundOutputRail.WaveAutoDetect,
            null,
            [$"{CliPrefix}wave examples/wave-effects.ssw effects.wav --stereo"]);

        yield return Wave(
            "wave-full-song",
            "Full song (full-song-wave.ss)",
            "full-song-wave.ss",
            "Render (Wave pane)",
            PlaygroundOutputRail.Wave,
            "Standard .ss file rendered via the wave backend (no effect/speak grammar required).",
            [$"{CliPrefix}wave examples/full-song-wave.ss jingle.wav"]);

        yield return Wave(
            "speech-only-wave",
            "Speech-only wave song",
            "speech-only-wave.ss",
            "Render (Wave pane)",
            PlaygroundOutputRail.Wave,
            "Contains speak (wave-only) — use wave, not run.",
            [$"{CliPrefix}wave examples/speech-only-wave.ss speech.wav"]);

        yield return Wave(
            "wave-vocal-stem",
            "Vocal stem (wave-vocal-stem.ssw)",
            "wave-vocal-stem.ssw",
            "Render (Wave pane)",
            PlaygroundOutputRail.Wave,
            "V8: speak sample= mixes your recording in CLI export; Playground uses synthetic fallback when stem file is absent.",
            [$"{CliPrefix}wave examples/wave-vocal-stem.ssw vocal.wav"]);
    }

    private static IEnumerable<PlaygroundPresetInfo> ShowcasePresets()
    {
        yield return new PlaygroundPresetInfo(
            "showcase-jingle-bells",
            "Jingle Bells (full showcase)",
            PlaygroundOutputRail.Midi,
            null,
            "Run",
            "Playground-only integrated demo — no matching file in examples/.",
            [$"{CliPrefix}run your-jingle-bells.ss"]);

        yield return Wave(
            "showcase-jingle-bells-wave",
            "Jingle Bells (Wave / speak overlay)",
            null,
            "Run (main) or Render (Wave pane)",
            PlaygroundOutputRail.WaveAutoDetect,
            "Playground-only extended arrangement with effect chain + speak overlay.",
            [$"{CliPrefix}wave your-jingle-bells-wave.ss output.wav"]);
    }

    private static PlaygroundPresetInfo Midi(string key, string title, string file, string? notes = null) =>
        new(
            key,
            title,
            PlaygroundOutputRail.Midi,
            file,
            "Run",
            notes,
            [$"{CliPrefix}run examples/{file}"]);

    private static PlaygroundPresetInfo Wave(
        string key,
        string title,
        string? file,
        string playgroundAction,
        PlaygroundOutputRail rail,
        string? notes,
        IReadOnlyList<string> cliSteps) =>
        new(key, title, rail, file, playgroundAction, notes, cliSteps);
}

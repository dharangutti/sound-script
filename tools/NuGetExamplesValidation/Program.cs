using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using SoundScript;
using SoundScript.Compose;
using SoundScript.Core;
using SoundScript.Media;
using SoundScript.Midi;
using SoundScript.Parser;
using SoundScript.Prosody;
using SoundScript.Timbre;
using SoundScript.Transcription;
using SoundScript.Visual;
using SoundScript.Vocal;
using SoundScript.Vocal.Wordbank;
using SoundScript.Wave;

// A package-only integration check. Paths contain outputs of two independent sample runs.
if (args.Length != 1) throw new ArgumentException("Expected validation output root.");
string root = Path.GetFullPath(args[0]);
var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
byte[] Read(string path) => File.ReadAllBytes(path);
string Hash(string path) => Convert.ToHexString(SHA256.HashData(Read(path)));
void Wave(string path)
{
    byte[] bytes = Read(path);
    Require(bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WAVE"u8), path);
    Require(PcmWaveInput.Decode(bytes).Samples.Any(sample => Math.Abs(sample) > .001), "Silent WAV: " + path);
}
foreach (string pair in new[] { "monitoring", "roundtrip" })
{
    string first = Path.Combine(root, pair + "-a");
    string second = Path.Combine(root, pair + "-b");
    var files = Directory.GetFiles(first, "*", SearchOption.AllDirectories)
        .Where(path => Path.GetExtension(path) != ".webm").Select(path => Path.GetRelativePath(first, path)).Order().ToArray();
    var repeated = Directory.GetFiles(second, "*", SearchOption.AllDirectories)
        .Where(path => Path.GetExtension(path) != ".webm").Select(path => Path.GetRelativePath(second, path)).Order().ToArray();
    Require(files.SequenceEqual(repeated), "Repeated output file set differs.");
    foreach (string file in files)
    {
        string a = Path.Combine(first, file), b = Path.Combine(second, file);
        Require(Hash(a) == Hash(b), "Nonrepeatable output: " + file);
        hashes[pair + "/" + file.Replace('\\', '/')] = Hash(a);
        if (Path.GetExtension(file) == ".wav") Wave(a);
        if (Path.GetExtension(file) == ".mid")
        {
            Require(Read(a).AsSpan(0, 4).SequenceEqual("MThd"u8), "MIDI signature");
            Require(MidiFile.Read(a).GetNotes().Any(), "Empty MIDI");
        }
    }
}

string[] names = ["healthy", "warning", "critical"];
int[] counts = [2, 4, 8], pitches = [72, 79, 84], tempos = [90, 116, 139], instruments = [73, 0, 40];
for (int index = 0; index < names.Length; index++)
{
    string name = names[index], dir = Path.Combine(root, "monitoring-a", name);
    var midi = MidiFile.Read(Path.Combine(dir, name + ".mid"));
    var notes = midi.GetNotes().ToArray();
    Require(notes.Length == counts[index] && notes.All(note => note.NoteNumber == pitches[index]), "Unexpected cue pitches/density");
    Require(midi.GetTrackChunks().SelectMany(track => track.Events).OfType<ProgramChangeEvent>()
        .Any(change => change.ProgramNumber == instruments[index]), "Unexpected instrument");
    Require(midi.GetTrackChunks().SelectMany(track => track.Events).OfType<SetTempoEvent>()
        .Any(change => Math.Abs(60_000_000.0 / change.MicrosecondsPerQuarterNote - tempos[index]) < .01), "MIDI tempo");
    using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "scenario.json")));
    Require(metadata.RootElement.GetProperty("Tempo").GetInt32() == tempos[index], "Unexpected tempo mapping");
    using var scene = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "scenes.json")));
    var primitives = scene.RootElement.EnumerateArray().Select(state => state.GetProperty("Primitives").EnumerateArray()
        .Single(primitive => primitive.GetProperty("Name").GetString() == "status-cue")).ToArray();
    Require(primitives.Length == 3 && primitives[0].GetProperty("Width").GetDecimal() == 100 + 80 * index, "Status cue geometry");
    Require(primitives[0].GetProperty("Opacity").GetDecimal() < primitives[1].GetProperty("Opacity").GetDecimal()
        && primitives[2].GetProperty("Opacity").GetDecimal() == 1, "Temporal opacity progression");
    var program = SoundScriptOutput.Parse(File.ReadAllText(Path.Combine(dir, name + ".ssv")));
    var timeline = VisualInterpreter.Interpret(program);
    Require(timeline.StateAt(TimeSpan.FromSeconds(4)).Elements.Count == 0, "End-exclusive timeline");
    byte[] sync = Read(Path.Combine(dir, name + "-synchronized.wav"));
    Require(PcmWaveInput.Decode(sync).Samples.Length == AnalysisAudio.SampleRate * 4, "A/V duration");
    using var stereoStream = new MemoryStream(Read(Path.Combine(dir, name + "-stereo.wav")));
    using var stereoReader = new BinaryReader(stereoStream);
    stereoStream.Position = 22;
    Require(stereoReader.ReadInt16() == 2, "Stereo channel count");
    var loaded = SoundScriptEngine.CompileFile(Path.Combine(dir, name + ".ss"));
    Require(loaded.RenderMidi().SequenceEqual(Read(Path.Combine(dir, name + ".mid"))), "CompileFile parity");
}
foreach (string suffix in new[] { ".ss", ".ssw", ".ssv", ".mid", "-music.wav", "-alert.wav", "-synchronized.wav", "-stereo.wav" })
    Require(names.Select(name => Hash(Path.Combine(root, "monitoring-a", name, name + suffix))).Distinct().Count() == 3,
        "Scenarios must differ: " + suffix);
foreach (string file in new[] { "timeline.json", "scenes.json" })
    Require(names.Select(name => Hash(Path.Combine(root, "monitoring-a", name, file))).Distinct().Count() == 3, "Visual variation");

string round = Path.Combine(root, "roundtrip-a", "Output");
var report = JsonSerializer.Deserialize<TranscriptionResult>(File.ReadAllText(Path.Combine(round, "report.json")))!;
Require(report.Suitability.CanGenerate && report.Score.Tracks.SelectMany(t => t.Notes).Select(n => n.MidiPitch)
    .SequenceEqual(new[] { 60, 64, 67, 72 }), "Original fixture transcription pitches");
var before = SoundScriptEngine.Compile(File.ReadAllText(Path.Combine(round, "transcribed.ss")));
Require(!before.RenderWave().SequenceEqual(Read(Path.Combine(round, "reconstructed.wav"))), "Instrument edit must change audio");
Require(!before.RenderMidi().SequenceEqual(Read(Path.Combine(round, "reconstructed.mid"))), "Instrument edit must change MIDI");

// Exercise the remaining reusable APIs from the actual package, not project references.
string probe = Path.Combine(root, "api-probe");
Directory.CreateDirectory(probe);
var composed = PhonemeComposer.BuildAst("machine ready");
var prosody = ProsodyComposer.BuildAst("machine ready");
foreach (var ast in new[] { composed, prosody })
{
    string source = SsPrinter.Print(ast);
    Require(SoundScriptEngine.Compile(source).RenderMidi().Length > 14, "compose/prosody source round trip");
    Require(WaveRenderer.RenderStereoToBytes(ast).Length > 44, "compose/prosody stereo Wave");
}
var interpreted = Interpreter.Interpret(composed);
int initialNotes = interpreted.Tracks.Sum(track => track.Notes.Count);
PhonemeComposer.AppendTo(interpreted, "ready");
ProsodyComposer.AppendTo(interpreted, "ready");
Require(interpreted.Tracks.Sum(track => track.Notes.Count) > initialNotes, "Append semantics");
string composedMidi = Path.Combine(probe, "composed.mid");
MidiGenerator.Write(interpreted, composedMidi);
_ = SoundCSSParser.ParseOverrides(OfflineRenderer.DefaultStylesheet);
byte[] styled = OfflineRenderer.RenderToWavBytes(Read(composedMidi));
File.WriteAllBytes(Path.Combine(probe, "styled.wav"), styled);
Wave(Path.Combine(probe, "styled.wav"));
OfflineRenderer.Render(composedMidi, OfflineRenderer.DefaultStylesheet, Path.Combine(probe, "styled.ogg"));
Require(Read(Path.Combine(probe, "styled.ogg")).AsSpan(0, 4).SequenceEqual("OggS"u8), "Ogg signature");
var engine = VocalEngineFactory.Create("prosody");
engine.Synthesize("machine ready", Path.Combine(probe, "vocal.wav"), new() { Seed = 7 });
Wave(Path.Combine(probe, "vocal.wav"));
var speech = SoundScriptOutput.Parse("tempo 120 track voice { speak \"machine ready\" } ");
var stems = VocalBatchExporter.ExportFromProgram(speech, Path.Combine(probe, "stems"), engine, new() { Seed = 7 });
Require(stems.Count == 1, "Vocal batch");
Wave(stems[0].FilePath);
// No corpus mutation or eSpeak installation: confirm typed failure results are accessible.
Require(!new WordbankNormalizer().Normalize("nonexistent-audit-lemma", "en").Success, "Missing wordbank source");
_ = new WordbankAutoGenerate().EnsureLemma("nonexistent-audit-lemma", "en", autoGenerateMissing: false);

string[] assemblies = ["Api", "Compose", "Core", "Media", "Midi", "Parser", "Prosody", "Timbre", "Transcription", "Visual", "Vocal", "Voice", "Wave", "Wordbank"];
var publicApi = assemblies.Select(name => Assembly.Load("SoundScript." + name)).SelectMany(assembly =>
    assembly.GetExportedTypes().OrderBy(type => type.FullName).Select(type => new
    {
        Assembly = assembly.GetName().Name, Type = type.FullName,
        Methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(method => method.ToString()).Order().ToArray()
    })).ToArray();
Require(!publicApi.Any(type => type.Type == "SoundScript.Cli.SourceAnalysis"), "CLI must not be packaged");
File.WriteAllText(Path.Combine(root, "public-api.json"), JsonSerializer.Serialize(publicApi, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(Path.Combine(root, "hashes.json"), JsonSerializer.Serialize(hashes, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"PASS: {hashes.Count} byte-identical artifacts; three distinct scenarios; MIDI/WAV/visual/transcription semantics; advanced API probes; {assemblies.Length} packaged assemblies.");

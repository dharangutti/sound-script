using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Midi;
using SoundScript.Parser;
using SoundScript.Wave;
using SoundScript.Wave.Adapter;
using SoundScript.Wave.Synthesis;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public sealed class ExpressivePerformanceTests
{
    private static ProgramNode Parse(string source) => new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
    private static List<TimedNote> Notes(string body, bool expressive = true, string instrument = "flute") =>
        Interpreter.Interpret(Parse($"{(expressive ? "perform expressive" : "")} tempo 60 track lead {{ instrument {instrument} {body} }}")).Tracks.Single().Notes;

    [Fact]
    public void OptInIsContextualAndRoundTrips()
    {
        var ast = Parse("perform expressive tempo 60 track lead { C4 q }");
        Assert.IsType<PerformNode>(ast.Statements[0]);
        Assert.IsType<PerformNode>(Parse(SsPrinter.Print(ast)).Statements[0]);
        Assert.Throws<InvalidOperationException>(() => Parse("perform mechanical"));
        Assert.Throws<InvalidOperationException>(() => Parse("melody { perform expressive C4 q }"));
        Parse("sequence perform { C4 q } play perform");
    }

    [Fact]
    public void LegacyLegatoGapIsMeasuredInEventsAndMidi()
    {
        var notes = Notes("C4 h legato D4 h legato", false);
        Assert.Equal(1.94, notes[0].DurationBeats);
        Assert.Equal(0.06, notes[1].StartBeat - notes[0].DurationBeats, 8);
        var ast = Parse("tempo 60 melody { C4 h legato D4 h legato }");
        using var stream = new MemoryStream();
        MidiGenerator.Write(Interpreter.Interpret(ast), stream);
        stream.Position = 0;
        var midiNotes = MidiFile.Read(stream).GetNotes().OrderBy(n => n.Time).ToArray();
        Assert.Equal(29, midiNotes[1].Time - midiNotes[0].EndTime);
    }

    [Fact]
    public void ConnectedNotesOverlapWithinBoundAndSoftenNextAttack()
    {
        var notes = Notes("C4 h legato D4 h legato E4 h legato");
        Assert.InRange(notes[0].DurationMs - notes[1].StartBeat * 1000, 1, 40.001);
        Assert.True(notes[1].Performance!.Connected);
        Assert.True(notes[1].Performance!.Attack > notes[0].Performance!.Attack);
        Assert.True(notes[^1].Performance!.PhraseEnd);
        Assert.True(notes[1].Velocity > notes[^1].Velocity);
    }

    [Theory]
    [InlineData("C4 h legato C4 h legato")]
    [InlineData("C4 h legato C5 h legato")]
    [InlineData("C4 h legato accent D4 h")]
    public void RepetitionsLeapsAndAccentsRearticulate(string body)
    {
        var notes = Notes(body);
        Assert.False(notes[1].Performance!.Connected);
        Assert.True(notes[0].StartBeat + notes[0].DurationBeats < notes[1].StartBeat);
        if (body.Contains("C5")) Assert.Equal(72, notes[1].MidiNumber);
    }

    [Fact]
    public void RestAndPhraseBoundariesPreventConnectionsIncludingRelease()
    {
        const string source = "perform expressive tempo 60 track lead { instrument flute phrase { articulation legato C4 h D4 h } rest q phrase { articulation legato E4 h F4 h } }";
        var notes = AstToNoteEventAdapter.Convert(Parse(source))["lead"];
        Assert.False(notes[2].Performance!.Connected);
        Assert.True(notes[1].StartTimeSeconds + notes[1].DurationSeconds + notes[1].Timbre.Envelope.Release <= 4.000001);
        var pcm = SoundScript.Wave.Mixing.Mixer.RenderTrack(notes, 44100);
        Assert.All(pcm.Skip(4 * 44100 + 1).Take(44100 - 2), sample => Assert.Equal(0, sample));
    }

    [Fact]
    public void StaccatoMidiTimingAndVelocityAreUnchanged()
    {
        const string body = "C4 e staccato C4 e staccato D4 e staccato G4 e staccato";
        var before = Notes(body, false);
        var after = Notes(body);
        Assert.Equal(before.Select(n => (n.StartBeat, n.DurationBeats, n.Velocity)),
            after.Select(n => (n.StartBeat, n.DurationBeats, n.Velocity)));
        Assert.All(after, n => Assert.False(n.Performance!.Connected));
    }

    [Theory]
    [InlineData("crescendo", true)]
    [InlineData("decrescendo", false)]
    public void SustainedDynamicsEvolveWithinOneNote(string envelope, bool grows)
    {
        var ast = Parse($"perform expressive tempo 60 track lead {{ instrument violin phrase {{ {envelope} C4 w }} }}");
        var note = AstToNoteEventAdapter.Convert(ast)["lead"].Single();
        Assert.Equal(grows, note.Performance!.GainEnd > 1);
        Assert.True(note.Performance.Evolution > 0);
        var pcm = NoteRenderer.Render(note, 44100);
        double Rms(int offset) => Math.Sqrt(pcm.Skip(offset).Take(4410).Average(x => (double)x * x));
        Assert.Equal(grows, Rms(3 * 44100) > Rms(44100));
        Assert.Equal(pcm, NoteRenderer.Render(note, 44100));
    }

    [Fact]
    public void LayeredVoicesConnectIndependently()
    {
        var ast = Parse("perform expressive tempo 60 track lead { layer flute layer violin phrase { articulation legato C4 h D4 h E4 h } }");
        var midi = Interpreter.Interpret(ast).Tracks.Single().Notes;
        Assert.Equal(6, midi.Count);
        Assert.Equal(4, midi.Count(n => n.Performance!.Connected));
        var wave = AstToNoteEventAdapter.Convert(ast)["lead"];
        Assert.Equal(6, wave.Count);
        Assert.Equal(4, wave.Count(n => n.Performance!.Connected));
    }

    [Fact]
    public void ExpressiveTracksHaveIndependentInstrumentChannels()
    {
        var ast = Parse("perform expressive track a { instrument flute C4 h } track b { instrument piano C4 h } track c { layer violin layer cello C4 h }");
        var program = Interpreter.Interpret(ast);
        Assert.Equal(4, program.Tracks.SelectMany(t => t.Notes).Select(n => n.Channel).Distinct().Count());
        Assert.Contains(program.Tracks[1].ProgramChanges, p => p.ProgramNumber == 0);
        Assert.DoesNotContain(program.Tracks.SelectMany(t => t.Notes), n => n.Channel == 9);
    }

    [Fact]
    public void ChordsDoNotConnectIntoMelodyOrReleaseAcrossRests()
    {
        var ast = Parse("perform expressive tempo 60 track lead { layer flute layer violin C4 q legato Cmaj h rest q D4 q legato }");
        var midi = Interpreter.Interpret(ast).Tracks.Single().Notes;
        Assert.Equal(10, midi.Count);
        Assert.All(midi, n => Assert.False(n.Performance!.Connected));
        var wave = AstToNoteEventAdapter.Convert(ast)["lead"];
        Assert.Equal(10, wave.Count);
        Assert.All(wave.Where(n => n.PerformanceIntent!.IsChord), n =>
            Assert.True(n.StartTimeSeconds + n.DurationSeconds + n.Timbre.Envelope.Release <= 3.000001));
    }

    [Fact]
    public void TiesRemainSingleAttacksAndShortNotesNeverOverlap()
    {
        Assert.Single(Notes("C4 h ~ C4 h"));
        var shortNotes = Notes("C4 for 0.1 legato D4 for 0.1 legato E4 for 0.1 legato");
        Assert.All(shortNotes, n => Assert.False(n.Performance!.Connected));
        for (var i = 1; i < shortNotes.Count; i++)
            Assert.True(shortNotes[i - 1].StartBeat + shortNotes[i - 1].DurationBeats < shortNotes[i].StartBeat);
    }

    [Fact]
    public void ChannelExhaustionIsExplicitRatherThanSharingInstruments()
    {
        var source = "perform expressive " + string.Join(" ", Enumerable.Range(0, 16).Select(i => $"track t{i} {{ C4 q }}"));
        var error = Assert.Throws<InvalidOperationException>(() => Interpreter.Interpret(Parse(source)));
        Assert.Contains("at most 15", error.Message);
    }

    [Fact]
    public void OptInMidiCarriesAudioHintsAndIsByteDeterministic()
    {
        var ast = Parse("perform expressive tempo 60 melody { C4 h legato D4 h legato }");
        byte[] Render()
        {
            using var stream = new MemoryStream();
            MidiGenerator.Write(Interpreter.Interpret(ast), stream);
            return stream.ToArray();
        }
        var bytes = Render();
        Assert.Equal(bytes, Render());
        using var stream = new MemoryStream(bytes);
        var midi = MidiFile.Read(stream);
        Assert.Contains(midi.GetTrackChunks().SelectMany(c => c.Events).OfType<TextEvent>(),
            e => e.Text.StartsWith("SoundScript.performance.v1:"));
        var notes = midi.GetNotes().OrderBy(n => n.Time).ToArray();
        Assert.True(notes[0].EndTime > notes[1].Time);
    }

    [Fact]
    public void HumanizedPhraseAttackDoesNotMoveIntoRest()
    {
        var ast = Parse("perform expressive tempo 60 track lead { instrument flute humanize timing=0.1 velocity=0.01 seed=17 C4 h rest q D4 h }");
        var wave = AstToNoteEventAdapter.Convert(ast)["lead"];
        Assert.True(wave[1].StartTimeSeconds >= 3);
        Assert.False(wave[1].Performance!.Connected);
    }

    [Fact]
    public void BothRenderersUseSameConnectionDurationsAtChangingTempo()
    {
        var ast = Parse("perform expressive tempo 60 track lead { instrument flute C4 h legato tempo 90 D4 h legato E4 h legato }");
        var midi = Interpreter.Interpret(ast).Tracks.Single().Notes;
        var wave = AstToNoteEventAdapter.Convert(ast)["lead"];
        for (var i = 0; i < midi.Count; i++)
            Assert.Equal(midi[i].DurationMs / 1000, wave[i].DurationSeconds, 6);
    }

    [Theory]
    [InlineData("performance-harbor")]
    [InlineData("performance-bells")]
    [InlineData("performance-ode")]
    [InlineData("performance-clockwork")]
    [InlineData("performance-lanterns")]
    public void MusicalCaseIsDeterministicAndProducesComparisonArtifacts(string key)
    {
        var source = SoundScript.Playground.PerformanceExamples.Source(key);
        var after = Parse(source);
        var before = Parse(source.Replace("perform expressive", ""));
        var program = Interpreter.Interpret(after);
        Assert.DoesNotContain(program.Warnings, w => w.Contains("incomplete") || w.Contains("exceeds expected"));
        Assert.Equal(Interpreter.Interpret(before).Tracks.Sum(t => t.Notes.Count), program.Tracks.Sum(t => t.Notes.Count));
        Assert.All(program.Tracks.SelectMany(t => t.Notes), n => Assert.True(n.DurationMs > 0));
        var wave = WaveRenderer.RenderStereoToBytes(after);
        Assert.True(wave.Length > 44100);
        Assert.Equal(wave, WaveRenderer.RenderStereoToBytes(after));
        var directory = Environment.GetEnvironmentVariable("SOUNDSCRIPT_PERFORMANCE_ARTIFACTS");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, key + ".ss"), source);
        foreach (var (label, ast) in new[] { ("before", before), ("after", after) })
        {
            var interpreted = Interpreter.Interpret(ast);
            MidiGenerator.Write(interpreted, Path.Combine(directory, $"{key}-{label}.mid"));
            File.WriteAllBytes(Path.Combine(directory, $"{key}-{label}.wav"), label == "after" ? wave : WaveRenderer.RenderStereoToBytes(ast));
            var adapted = AstToNoteEventAdapter.Convert(ast);
            var report = new
            {
                key, label,
                midi = interpreted.Tracks.Select(t => new { t.Name, Notes = t.Notes.Select(n => new
                {
                    n.MidiNumber, n.StartBeat, n.DurationBeats, n.Velocity, n.Channel,
                    StartSeconds = interpreted.TempoMap.BeatsToMilliseconds(0, n.StartBeat) / 1000,
                    DurationSeconds = n.DurationMs / 1000, n.Performance
                }) }),
                wave = adapted.Select(t => new { Name = t.Key, Notes = t.Value })
            };
            File.WriteAllText(Path.Combine(directory, $"{key}-{label}.json"),
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }
}

# Original transcription validation corpus

Five CC0 original generated melodies. `TranscriptionFixtures.cs` is the generator and
preserves exact pitch, onset and duration ground truth. `measurements.json` contains
ground truth and measured source/round-trip results; `.wav` are actual test inputs,
`.ss` are generated source, `-preview.wav` are existing Wave renderer reconstructions.
No copyrighted third-party music, recording, lyrics or score is used.

Regenerate from repository root in PowerShell:

```powershell
$env:SOUNDSCRIPT_TRANSCRIPTION_FIXTURES = "$PWD/src/SoundScript.Tests/Golden/transcription"
dotnet test src/SoundScript.Tests --filter FullyQualifiedName~FiveOriginalFixtures
```

These synthetic timbres exercise harmonic/vibrato/envelope differences. They do not
validate real singers, microphones, flute, violin or piano recordings. The rubato
fixture intentionally lacks a single reliable tempo: its 120 BPM label is the
nominal authoring reference, and automatic estimation selects 95 BPM. Measured
seconds and off-grid beats remain available; use a tempo override when known.

# DevOps test sonification

This sample turns a test summary into deterministic WAV and MIDI cues. Passed tests create a bright repeated motif; failures add a lower cue whose dynamic changes with severity. The generated SoundScript source is also saved so the mapping is inspectable and reviewable.

Run it from the repository root:

```bash
dotnet run --project samples/DevOpsSonification/DevOpsSonification.csproj -- --total 100 --passed 96 --failed 4
dotnet run --project samples/DevOpsSonification/DevOpsSonification.csproj -- --total 100 --passed 100 --failed 0 --output artifacts/samples/devops-green
```

The default output directory is `artifacts/samples/devops-sonification`, containing `devops-status.ss`, `devops-status.wav`, `devops-status.mid`, and a manifest with both SHA-256 digests. Repeating the same command produces identical media bytes. See [the .NET API guide](../../docs/dotnet-api.md) and [the application demos guide](../../docs/application-samples.md).

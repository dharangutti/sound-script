# Deterministic test fixture generator

This sample creates exact WAV and MIDI fixtures from C# for regression tests, media tests, and reproducible examples. It renders each output twice, reloads the generated `.ss` through `CompileFile`, and compares the consumer output byte-for-byte before writing the fixture and its manifest.

Run it from the repository root:

```bash
dotnet run --project samples/TestFixtureGenerator/TestFixtureGenerator.csproj
dotnet run --project samples/TestFixtureGenerator/TestFixtureGenerator.csproj -- artifacts/samples/fixture-seed-7 7
```

The default output directory is `artifacts/samples/test-fixture-generator`. It contains `fixture.ss`, `fixture.wav`, `fixture.mid`, `manifest.json`, and `consumer-check.txt`. The optional seed changes the deterministic motif; repeating a seed reproduces the same output. See [the .NET API guide](../../docs/dotnet-api.md) and [the application demos guide](../../docs/application-samples.md).

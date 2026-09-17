# Dynamic application audio

This sample models application events such as success, warning, new message, and deployment failure. Each event is converted at runtime into a different SoundScript motif, tempo, and dynamic marking, showing how a .NET application can select context-sensitive feedback without embedding audio assets.

Run it from the repository root:

```bash
dotnet run --project samples/DynamicAudio/DynamicAudio.csproj
dotnet run --project samples/DynamicAudio/DynamicAudio.csproj -- artifacts/samples/dynamic-audio-custom
```

The default output directory is `artifacts/samples/dynamic-audio`. It contains one WAV per event and `manifest.txt` with the selected musical state and SHA-256 digest. The source uses `SoundScriptEngine.Compile(...).RenderWave()` from the public NuGet facade. See [the .NET API guide](../../docs/dotnet-api.md) and [the application demos guide](../../docs/application-samples.md).

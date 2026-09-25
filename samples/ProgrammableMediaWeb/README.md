# Programmable media in a browser

ASP.NET Core and plain JavaScript consume the SoundScript NuGet package. No project references or frontend framework.
This sample follows the development version inherited from Directory.Build.props.
Start with a [source checkout with submodules](../../README.md#try-it-in-60-seconds).
From the repository root, pack that development snapshot and restore from its local feed:

```powershell
dotnet pack src/SoundScript -c Release -o artifacts/packages
dotnet restore samples/ProgrammableMediaWeb -p:RestoreAdditionalProjectSources=../../artifacts/packages --packages ./artifacts/sample-cache
dotnet run --project samples/ProgrammableMediaWeb -c Release --no-restore --urls http://127.0.0.1:5198
```

Open http://127.0.0.1:5198. Use Play/Resume, Pause, Restart and the native audio seek control.
Choose Healthy, Warning or Critical to generate different audio, colors, sizes and text from typed application data. The separate **Adaptive runtime values** card uses a single `SoundScriptEngine.CompileRuntime` instance created at startup; edit intensity/x position and apply to request a new complete WAV and scene.
The sample has `/api/audio`, `/api/duration`, `/api/scene?t=2` and `/api/scene.svg?t=2` endpoints;
each accepts `scenario=healthy`, `warning` or `critical`. Invalid times/scenarios return HTTP 400.
The runtime card uses `/api/runtime/info`, `/api/runtime/audio?intensity=0.9&xpos=900`, and `/api/runtime/scene.svg?intensity=0.9&xpos=900&t=2`.

The only authoritative position is `audio.currentTime`. `requestAnimationFrame` schedules repaint requests.
Requests from an earlier seek or scenario are aborted/discarded. SVG is displayed as an image.
This local demo is not a production streaming service: request latency can delay visual presentation.
Each runtime request supplies the complete typed state, updates the shared runtime under a request lock, binds a snapshot, and returns a rendered result. Runtime output is offline rendering, not streaming. This is a source-branch API candidate under validation, not a released-package validation claim.

Browser verification: `npm ci --prefix scripts`, then `node scripts/verify-programmable-media.cjs` while the sample runs.
It uses installed Edge by default (`CHROMIUM_CHANNEL` overrides it).
See [runtime architecture](../../docs/programmatic-media-runtime.md).


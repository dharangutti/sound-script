# Programmable media in a browser

ASP.NET Core and plain JavaScript consume the SoundScript NuGet package. No project references or frontend framework.
From the repository root, first pack the candidate and restore this sample from that local feed:

```powershell
dotnet pack src/SoundScript -c Release -o artifacts/packages
dotnet restore samples/ProgrammableMediaWeb -p:RestoreAdditionalProjectSources=../../artifacts/packages --packages ./artifacts/sample-cache
dotnet run --project samples/ProgrammableMediaWeb -c Release --no-restore --urls http://127.0.0.1:5198
```

Open http://127.0.0.1:5198. Use Play/Resume, Pause, Restart and the native audio seek control.
Choose Healthy, Warning or Critical to generate different audio, colors, sizes and text from typed application data.
The sample has `/api/audio`, `/api/duration`, `/api/scene?t=2` and `/api/scene.svg?t=2` endpoints;
each accepts `scenario=healthy`, `warning` or `critical`. Invalid times/scenarios return HTTP 400.

The only authoritative position is `audio.currentTime`. `requestAnimationFrame` schedules repaint requests.
Requests from an earlier seek or scenario are aborted/discarded. SVG is displayed as an image.
This local demo is not a production streaming service: request latency can delay visual presentation.

Browser verification: `npm ci --prefix scripts`, then `node scripts/verify-programmable-media.cjs` while the sample runs.
It uses installed Edge by default (`CHROMIUM_CHANNEL` overrides it).
See [runtime architecture](../../docs/programmatic-media-runtime.md).


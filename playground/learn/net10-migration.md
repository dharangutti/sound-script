# .NET 10 migration

All 18 projects (including both Blazor WebAssembly apps, CLI test helper and
VisualParity tool) target `net10.0`. `global.json` selects stable .NET 10 SDKs
from the 10.0.300 feature band onward; C# 14 is the framework default. Blazor
packages move together to 10.0.11. Other package versions remain unchanged
because restore/build and integration tests did not require upgrading them.

CLI subprocess tests, media verification scripts, all three CI workflows,
contributor instructions and current website/runtime requirements now use
.NET 10. Historical .NET 8 benchmark descriptions remain historical.

The existing Windows/Linux/macOS test matrix now builds the whole solution
and the standalone VisualParity tool, and publishes the Playground into
`artifacts/playground` for validation. `SOUNDSCRIPT_PLAYGROUND_PUBLISH_DIR`
lets the published-asset tests inspect that fresh output. No workflow has
been dispatched and no production artifacts have been changed.

## Local validation

- Windows, SDK 10.0.303: `dotnet build SoundScript.sln -c Debug` succeeded.
- Complete existing suite: **987 passed, 0 failed, 0 skipped** on .NET 10.
- Release Playground publish succeeded, including trimming and WASM linking.
- CLI mixed Wave/voice/visual WebM export: VP9 video plus Opus audio,
  successfully decoded with FFmpeg.
- Existing tests cover parser/interpreter, MIDI, Wave determinism, voice/speak,
  temporal visuals, media bridges, CLI commands and Playground presets.
- Three existing xUnit2031 assertion-style warnings remain; no warnings were
  suppressed. No framework breaking changes required application rewrites.
- Initial runs encountered Windows Application Control and an uninitialized
  wordbank submodule. After the user disabled Smart App Control and the pinned
  submodule was initialized, the complete suite passed.

Remote Windows/Linux/macOS CI has **not run for this branch**. Local Windows
results are not evidence of remote or cross-platform success.

Microsoft's [.NET 10 compatibility catalogue](https://learn.microsoft.com/en-us/dotnet/core/compatibility/10)
and [ASP.NET Core migration guide](https://learn.microsoft.com/en-us/aspnet/core/migration/90-to-100?view=aspnetcore-10.0)
were reviewed, including browser HTTP streaming and the inlined boot
configuration. The source does not depend on synchronous browser HTTP streams
or read `blazor.boot.json` directly.

Review this migration independently of the subsequent musical-language work.
The CLI now requires .NET 10 (or a self-contained distribution); existing
script syntax and export commands are unchanged by this migration.

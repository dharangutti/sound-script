# Playground startup investigation — 16 September 2026

Branch: `codex/fix-playground-startup`, created from `codex/transcription`.

## Findings and confidence

The reported request returned HTTP 503 and bytes with SHA-256
`J5J7M+vuSdfwlnN/Sn2rAOJqbgPFSQxmN3vK84OiR6Q=`. These are **not** the expected
bytes of `Microsoft.Extensions.DependencyInjection.Abstractions.vec9jc22jj.wasm`.
The active production .NET 10 boot configuration expects
`sha256-6RX5IfR+emeuVsgxEChGKQ0qgoTimaYHB+nhU0wbZaY=`. Both the live successful
response and the file committed to `gh-pages` match that expected value.

The immediate failure mechanism is a failed asset response rejected by SRI,
followed by a failed runtime download/platform startup. The evidence does **not**
identify which upstream component produced the 503 or establish that a deployment
race caused it. The failed response body, timestamp and request/Ray ID were not
captured. A successful fresh load is not proof the intermittent problem is gone.

Verified observations:

- Five sequential requests to the exact affected URL, alternating default and
  revalidation cache modes, returned 200, 26,389 decoded bytes, and the same SRI.
- All 67 integrity-protected resources in the active live boot configuration
  matched their remote bytes. The embedded boot configuration's loader remained
  byte-identical before and after those requests.
- Headers on the tested route included `Server: cloudflare`,
  `cf-cache-status: DYNAMIC`, `Cache-Control: max-age=600`, and GitHub/Fastly cache
  headers. This does not establish Cloudflare settings at all edges or during
  the failed request. No Cloudflare cache rules, workers, header configuration,
  or service worker are defined in this repository.
- GitHub Pages is configured to build `gh-pages` at `/`, custom domain
  `soundscript.net`. The last inspected deployment run was `35041842572`, source
  commit `5f79413cf46bfeb3fecfe4f6c1906f4f46a8af96`; it completed successfully.
- The former publish target copied `wwwroot` over checked-in `docs/playground`.
  It did not remove old files. A .NET 8 `blazor.boot.json` and old assemblies
  remain in that tracked tree and were still served by production alongside
  .NET 10 assets. .NET 10 actually reads the JSON embedded in `dotnet.js`;
  checking only the leftover JSON would verify the wrong generation.
- The Pages action already pushed a single commit. There is no evidence it
  uploads individual files directly to the live site. The proven defect is
  assembling that commit from an overlaid tree. Separate cached HTTP responses
  can still span deployment generations; a Git commit cannot control CDN caches.
- No repository publish step rewrites WASM after SRI generation. A separate
  reproducible defect exists in the old Windows checkout: `core.autocrlf=true`
  converted the old .NET 8 native JS to CRLF, changing its digest to
  `sha256-xA5PqQdfsYnmBdVddgzuvhxFIH3KS7l5w1fN7PkZ+qA=`. The Git blob, LF-normalized
  file and decompressed gzip/Brotli copies all match the boot manifest's expected
  `sha256-YDHEHpc+/PUWFYa9BMCN3EcVlpemjKD024swcpPfMIQ=`. The new verifier rejects
  that checkout. This explains a local old-build integrity failure, not the
  reported production WASM 503. Newline conversion is now prevented for generated
  deployment output, and all framework compression variants are verified.

## Changes

1. Default Release publish output is `artifacts/playground`, not checked-in docs.
   Flattening replaces the complete `_framework` directory. Every publish runs
   the verifier and fails on inconsistent output. Node 22+ is a build dependency.
2. `scripts/publish-site.ps1` publishes into a unique empty staging directory,
   copies documentation **excluding** the old Playground, verifies the completed
   site, then promotes it to `artifacts/site`. A failed build keeps the prior
   artifact. No generated framework file is modified after verification.
3. Deployment publishes only that verified site, with `keep_files: false`,
   `.nojekyll`, and deployment `.gitattributes` containing `* -text`. Existing
   branch-based Pages settings are preserved. In-progress deployments are not
   cancelled by newer builds. Old tracked docs output cannot enter this workflow.
4. The verifier reads .NET 10 embedded boot JSON or legacy boot JSON; checks every
   referenced file and SHA-256; rejects mixed manifests, stale assets, missing
   SRI, and missing resources; and decompresses every gzip/Brotli framework file
   to verify it matches its uncompressed counterpart. It never executes boot JS.
5. Startup retries integrity-protected resource failures at most three times,
   with a 30-second timeout per attempt and cache reload on retries. Every fetch
   retains the exact expected SRI. JS module loading remains with Blazor.
   Permanent corruption still blocks startup and displays an actionable error.
   This mitigates transient response failures; it does not repair CDN outages.
6. CI and deployment run verifier unit tests and actual browser startup checks
   before publication. Browser tests cover all three tabs, same-client reload,
   no service-worker registration, injected 503 recovery, and rejection of
   corrupted WASM. Existing transcription behavior is unchanged in Phase 1.

## Reproduce verification

```powershell
node --test scripts/playground-integrity.test.cjs scripts/playground-startup.test.cjs scripts/transcription-browser-input.test.cjs
./scripts/publish-site.ps1
npm ci --prefix scripts
npx --prefix scripts playwright install chromium
node scripts/verify-playground-startup.cjs artifacts/site/playground
dotnet build SoundScript.sln -c Debug
$env:SOUNDSCRIPT_PLAYGROUND_PUBLISH_DIR = "$PWD/artifacts/site/playground"
dotnet test src/SoundScript.Tests -c Debug --no-build
node scripts/probe-playground.cjs
```

Browser scripts also accept `PLAYWRIGHT_MODULE` and `CHROMIUM_PATH` for an existing
installation. The production probe saves status, headers and digest evidence to
`artifacts/production-integrity-probe.json`; it makes read-only requests.

## Changed files

- `.gitattributes`, `.gitignore`
- `.github/workflows/deploy-pages.yml`, `.github/workflows/tests.yml`
- `src/SoundScript.Playground/SoundScript.Playground.csproj`
- `src/SoundScript.Playground/wwwroot/index.html`
- `src/SoundScript.Playground/wwwroot/js/playground-startup.js`
- `src/SoundScript.Tests/PlaygroundPublishTests.cs`
- `scripts/publish-site.ps1`, `scripts/verify-playground-integrity.cjs`
- `scripts/playground-integrity.test.cjs`, `scripts/playground-startup.test.cjs`
- `scripts/verify-playground-startup.cjs`, `scripts/probe-playground.cjs`
- `scripts/package.json`, `scripts/package-lock.json`
- `docs/playground-startup-investigation.md`

## Gate and remaining work

The artifact can be checked and republished without disabling integrity. However,
the infrastructure-level cause of the historical 503 remains unproven. Do not
call the production incident resolved solely from local tests or successful probes.
To distinguish CDN/origin errors from a deployment-window cache failure, capture
the next failing request's HAR/response, timestamp and Ray/request ID, then correlate
with Cloudflare and GitHub Pages logs. CDN configuration is outside this repository;
avoid transformations of `_framework` files and cache overrides on boot metadata.

Phase 2 Transcription UX work remains gated on understanding and resolving that
production failure, as requested. No claim is made that the original failed
client or cross-edge deployment transitions have been reproduced.

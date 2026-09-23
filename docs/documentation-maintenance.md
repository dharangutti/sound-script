# Maintaining documentation

Configuration supplies facts. Documentation supplies explanation.

Use PowerShell 7 and Node.js 22 or later:

```powershell
./scripts/update-docs.ps1
./scripts/update-docs.ps1 -Check
node --test scripts/docs-state.test.mjs
```

Update validates all inputs and renders in memory before writing only approved
generated bodies. Check never writes: it rejects drift, invalid state or classification,
malformed markers, stale current installs, channel contradictions, missing CLI coverage,
exit-code mismatches and broken local links. Diagnostics identify file, category/check,
actual/expected conditions and corrective action.

## Facts and release state

The [audit](v15-documentation-audit.md#authoritative-facts) records sole ownership.
Directory.Build.props owns development identity; project files own package IDs, tool
name, framework and URLs. Release notes remain authored history, never publication evidence.

Schema 1 of release-state.json requires exactly schemaVersion (integer 1), publicVersion
(SemVer), library.nugetPublished, cli.nugetPublished and cli.githubReleasePublished
(booleans). Flags apply to that exact public version. Unknown fields/schema versions,
missing fields, wrong types, invalid SemVer or public versions newer than development
fail. Prerelease precedence is respected; build metadata does not affect precedence.
Disabled channels do not emit their install command or release link.

After an independently authorized public release, verify each channel, change publicVersion
and its flags together, regenerate and review. Development identity alone must leave
public onboarding unchanged. The deploy workflow checks state; staged homepage history
selects public state explicitly and skips newer development release notes.

## Classification and generated blocks

Every scoped file must be living, historical, generated-only or excluded with a reason
in docs-manifest.json. New files fail until classified. Missing declarations, duplicate
classes and invalid exclusions fail. Explicit declarations take precedence over historical
patterns only with a matching classification and meaningful reason in classificationOverrides.
Contradictory explicit lists always fail.

Only IDs declared in generatedBlocks for a living file may be generated. Use one ordered
pair of GENERATED:ID_START / GENERATED:ID_END HTML comments. Duplicate, missing,
mismatched, reversed or nested markers fail. Historical reports cannot contain current-state
blocks. UTF-8 BOM, per-file newlines and bytes outside generated bodies are preserved.
No clock, network, locale or machine path participates in rendering.

Current install commands must be pinned to publicVersion. Local-feed instructions use
developmentVersion. Keep dated comparisons in historical files, or a small reviewed
HISTORICAL_CONTEXT_START / HISTORICAL_CONTEXT_END comment pair. Such exceptions must
describe actual evidence, never exempt current onboarding. Ordinary old numbers and
algorithmic candidate terminology remain valid. Validation cannot understand arbitrary
natural-language paraphrases; review remains necessary.

Living Markdown links and heading fragments are checked offline using the vendored
Markdown tokenizer; example code is not treated as links. External availability is a
separate manual check. Generated package/release links derive from owned IDs and public
state. Current HTML entry links must target documentation.md.

## Code and terminal presentation

Use a known fence: soundscript, csharp/cs, bash, sh/shell, powershell/pwsh, cmd, json,
xml, javascript/js, html or css. Use text for plain text and diagrams. Use console-output
for logs:

```console-output
Build succeeded.
0 Warning(s)
0 Error(s)
```

The viewer distinguishes source, terminal and output containers. Copy uses code text
alone and never includes labels or decoration. Literal source prompts are retained.
Output blocks also receive Copy so developers can save or share logs; they remain
visually distinct from terminal commands. Inline code receives no copy control.
Fence line endings are normalized to LF by Markdown parsing; the clipboard API receives
the full code text, including intentional blank lines. Windows may return CRLF when
reading its native clipboard. Output is labelled Output, never Run. Clipboard denial falls back to a temporary
selectable text area; failure leaves code intact and reports a selectable-code fallback.
Buttons have keyboard access, visible focus and live status. Long lines scroll; the
toolbar remains visible on phones.

Syntax highlighting is deferred: the existing monospace palette, labels and container
types meet the presentation goal without another dependency or a second SoundScript
grammar. Escaped code remains text, including script-like input.

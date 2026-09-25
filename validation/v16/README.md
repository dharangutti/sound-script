# V16 validation evidence

This directory preserves compact outputs from the unpublished V16 candidate validation. See the [acceptance report](../../docs/v16-acceptance-report.md) for exact commands, scope, limitations, package identity and hashes, and the [DX gate](../../docs/v16-dx-release-gate.md) for its conclusions.

- `results.json`: TRX-derived totals, added-suite counts and package inventory.
- `production-files.txt`: every changed/new `src/` file against baseline main `09bb0b37dd0902c6c6ffcd8201e1f4547409550c`.
- `benchmark.json`: seven-batch observations from the console monitoring example.
- `package-consumer.log`, `media-package.log`: isolated external candidate consumers.
- `runtime-browser.json`, `web-browser.log`, `cli-canonical.log`: real-surface canonical state checks.
- `startup-browser.log`, `transcription-browser.log`, `av-browser.log`: preserved Playground workflows after UX changes.
- `regression.log`, `lab.log`, `browser-unit.log`, `homepage.log`, `docs-check.log`: test/check outputs.

Build output paths in copied logs are normalized to `<repo>`/`<temp>` for readability. Local TRX, package archives, media output, screenshots and browser video remain in ignored `artifacts/`; they are reproducible using the report commands and checked-in validation scripts. The evidence is an observed run, not a promise of universal timing or platform compatibility.

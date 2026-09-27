# VideoLab v0.4 publication validation — 2026-09-28

Phases 0–3 passed. See ../roadmap/PHASE_0_REPORT.md through PHASE_3_REPORT.md for
design, compatibility, limitations and exact milestone evidence.

- 294 native checks: 29 legacy, 66 programmable, 48 normalization, 43 editing,
  53 audience and 55 external annotation checks.
- 48 gallery format/binding pairs rendered twice, byte-identical and fully decoded.
- Ten demos, 26 snapshots and 52 MP4/WebM exports; public browser checks passed.
- Local browser/API checks passed for numeric/typed binding, personal media,
  annotation datasets, rendering, cancellation/reset and cleanup.
- CLI typed and annotation/batch tests passed, including no writes when a later
  dataset is invalid. Twelve independent encoder repetitions match exactly.
- Three explicit disconnected-response shutdown tests pass after an adapter fix.
- All 49 prior Phase 2 MP4/WebM assets retain their exact hashes.
- Acceptance frames and browser layouts were visually inspected.

Static hosting has no upload or native render service. Arbitrary supported values
and personal media use the optional loopback workbench. Source remains authoritative;
there is no drag/drop editor. Text is bounded single-line ASCII with an embedded
font; enums/booleans and annotation categories/styles are deliberately limited.
Repeatability requires identical inputs, tools and environment. Core SoundScript
implementation and APIs are unchanged. Signal Lab remains POC/unpublished.

Publication uses a new immutable tag and main-owned manifest/evidence hashes. A
branch push alone does not deploy. Production staging/browser checks are recorded
with the separate promotion PR; the experimental implementation stays off main.

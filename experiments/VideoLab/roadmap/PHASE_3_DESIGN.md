# Phase 3 design — queued until Phase 2 gate

No Phase 3 implementation yet. This records the next decisions while Phase 2's
render/browser gate runs, so a restart need not rediscover the design.

Keep annotations as bounded declarative data, not runtime scripts. A separate
JSON document has schemaVersion, optional audienceParameter and annotations.
Each annotation has a unique identifier, literal text, half-open start/end frames,
category, severity, plane position/size, audience membership and a built-in style.
Styles are text, callout or highlight. Callouts require explicit local targets.
Categories: instruction, information, warning, inspection, revision, comment.
Severity controls a small predefined palette; arbitrary style scripting is absent.

A pure expansion function consumes base source plus annotation JSON and lowers
records to existing text/callout/shape primitives and audience group predicates.
Compile the result through the same validation path; no second rendering engine.
Retain original base source and annotation identifiers for inspection/provenance.
Existing aggregate element/frame/pixel caps still apply after expansion.

CLI explicitly loads an annotation file. Batch records may name annotation files
relative to their batch directory, caching compilation by dataset content while
reusing one base source. Protect base, batch, annotation and media inputs from
output replacement. Validate all records before rendering any outputs. No implicit
file discovery, arbitrary data fields, external URLs or host evaluation.

One base project and three datasets (QA comments, shop-floor instructions and
engineering review) demonstrate independent annotation families. The private
browser presents curated dataset variants with the base source and dataset both
inspectable. Local mode uses only approved datasets; arbitrary file import UI is
outside this milestone.

Gate: schema/type/boundary/lifetime/category/audience tests, deterministic lowering,
immutable snapshots, stable input media, dataset isolation, output protection,
repeat+decode MP4/WebM, all earlier native checks, browser tests and a report.

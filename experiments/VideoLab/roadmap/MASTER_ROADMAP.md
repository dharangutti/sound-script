# VideoLab Master Roadmap Prompt — Familiar Video Editing + Programmable Media

## Product direction

VideoLab should evolve as an **isolated experimental programmable video composition product**.

It must remain separate from core SoundScript.

The product should appeal to two overlapping audiences:

1. **Normal video users/editors**
   - familiar concepts
   - clips
   - trimming
   - timeline
   - text
   - overlays
   - transitions
   - audio
   - transforms
   - preview/export

2. **Programmatic / automation users**
   - runtime parameters
   - reusable composition definitions
   - conditional layers
   - role-specific variants
   - structured annotations
   - data-driven sequences
   - batch rendering
   - deterministic/repeatable outputs
   - CI-generated media

The goal is NOT to compete feature-for-feature with Premiere Pro, Final Cut or DaVinci Resolve.

The goal is:

> Provide enough familiar video-editing capability that an ordinary editor understands the model immediately, then add programmable capabilities that conventional timeline editors do not naturally provide.

---

# Architectural principle

Think of VideoLab as:

```text
Familiar video composition
        +
programmable rules
        +
structured data
        +
runtime parameters
        =
repeatable families of videos
```

Example:

```text
One base project
      ↓
manual-looking editing model
      +
audience / locale / revision / product parameters
      ↓
Shop Floor version
QA version
Engineering version
Customer version
Training version
```

Do not sacrifice ordinary editing usability merely to make everything programmable.

Likewise, do not sacrifice the deterministic/programmatic architecture merely to imitate conventional editors.

Both layers should coexist.

---

# Isolation requirement

VideoLab remains experimental and isolated.

Expected branch:

```text
experiments/videolab
```

Expected implementation area:

```text
experiments/VideoLab/
```

It must NOT:

- reference production SoundScript internals
- enter the production SoundScript solution
- change the SoundScript compiler/runtime
- change NuGet
- change SoundScript CLI
- trigger a SoundScript release
- become a SoundScript language extension
- force changes into `main` beyond approved Labs hosting metadata/artifacts

VideoLab should remain extractable into its own repository/product later.

Treat easy future extraction as an architectural requirement.

---

# Existing baseline

Do not regress current functionality.

Current VideoLab already demonstrates:

- immutable compositions
- runtime bindings
- immutable snapshots
- `SceneAt(frame)`
- MP4 and WebM rendering
- FFmpeg planning/rendering
- source normalization
- video layers
- audio layers
- shapes
- transforms
- crop
- scale
- rotation
- opacity
- transitions
- expressions
- animations
- conditions
- reusable effects
- structured data sequences
- batch rendering
- repeatability validation
- browser MVP
- publication provenance
- experimental Labs hosting

Preserve all existing validated checks.

Do not rewrite working architecture merely because this roadmap introduces additional features.

---

# Development philosophy

Implement this roadmap in phases.

Each phase must:

1. solve a coherent user problem
2. preserve previous behavior
3. add focused tests
4. document exact semantics
5. remain independently useful
6. pass its acceptance gate
7. be reviewable as a separate PR/milestone where practical

Do NOT implement later phases automatically.

After completing a phase, stop and report.

The next phase requires explicit authorization.

---

# PHASE 0 — Baseline audit and roadmap fit

Before making product changes:

- inspect current `experiments/videolab`
- inspect current browser workbench
- inspect current tests
- inspect current public `/labs/videolab/`
- inspect current MVP publication mechanism
- identify current resource limits
- identify current authoring schema
- identify existing UI/timeline behavior
- confirm current branch/tag/provenance

Run the full existing validation suite.

Record:

```text
baseline commit
test count
browser checks
known limits
supported formats
supported authoring constructs
```

Do not modify product behavior in Phase 0.

### Gate

PASS only if current baseline is reproducible.

---

# PHASE 1 — Familiar editing foundation

## Goal

Make VideoLab understandable to a conventional video user without abandoning its JSON/programmatic foundation.

Do NOT attempt to create a professional NLE.

Add only the highest-value familiar primitives that naturally fit the existing architecture.

## 1A. Text primitive

This is the highest-priority missing feature.

Add deterministic text overlays.

Support at minimum:

```text
text
x
y
font size
color
opacity
start frame
duration
alignment
```

Prefer explicit bundled/controlled font behavior.

Avoid unpredictable system font discovery if it harms reproducibility.

Document exactly how fonts are selected.

Example conceptual authoring:

```json
{
  "type": "text",
  "text": "Inspect weld seam",
  "at": 120,
  "frames": 90,
  "x": 640,
  "y": 120,
  "fontSize": 38,
  "color": "FFFFFF"
}
```

Text should participate in:

- conditions
- transforms where reasonable
- opacity
- animation
- runtime bindings

## 1B. Callout primitive

Build on text + shapes rather than inventing an unrelated subsystem.

Support a simple technical/editorial callout:

```text
label
background
pointer/line
anchor position
target position
```

Example:

```text
[ Inspect bearing ]
         \
          → target
```

Do not build a complex diagramming engine.

## 1C. Basic visual editing affordances

Ensure current model clearly supports ordinary editing concepts:

- trim
- placement
- cut
- crossfade
- position
- scale
- rotation
- opacity
- crop
- simple overlay
- audio gain

Expose these coherently in the browser workbench.

Do not add unnecessary advanced effects yet.

## 1D. Browser timeline readability

Improve the existing read-only/browser MVP sufficiently that an ordinary editor can understand:

```text
clip A
clip B
text
shape
audio
transition
```

A simple track/lane representation is enough.

Do NOT build drag-and-drop editing yet unless it can be done very cheaply without changing core semantics.

The source definition remains authoritative.

## Phase 1 acceptance demo

Create one ordinary editing example:

```text
two video clips
+
crossfade
+
title
+
callout
+
audio
+
basic transform
```

The goal is to show:

> VideoLab can perform recognizable normal video-composition work.

### Phase 1 gate

Do not proceed unless:

- text renders correctly
- callouts render correctly
- MP4 and WebM work
- existing tests remain green
- deterministic behavior is preserved within existing guarantees
- browser example is usable
- no core SoundScript change occurs

STOP after Phase 1 and report.

---

# PHASE 2 — Programmable audience variants

## Goal

Add VideoLab's first major differentiator:

> One composition, multiple audience-specific outputs.

This should build on the normal editing primitives from Phase 1.

## 2A. Typed non-decimal parameters

Current decimal parameters are insufficient for readable audience workflows.

Add a minimal typed parameter model.

Prioritize:

```text
enum
string only where required
boolean
existing decimal
```

Prefer bounded/enumerated values when possible.

Example:

```text
audience = shopfloor | qa | engineering | customer
```

Avoid turning parameters into a general-purpose dynamic scripting language.

## 2B. Conditions using typed parameters

Allow:

```text
when audience == qa
when audience == shopfloor
when showSafety == true
```

Preserve typed AST validation.

No arbitrary host scripting.

## 2C. Named logical layers/groups

Introduce a simple grouping mechanism such as:

```text
layer "qa"
layer "shopfloor"
layer "engineering"
```

A group can contain:

- text
- callouts
- shapes
- video overlays
- audio where appropriate

Groups should remain composition concepts, not UI-only metadata.

## 2D. Audience-specific batch output

Build on existing batch rendering.

Example conceptual batch:

```text
shop-floor.mp4
    audience=shopfloor

qa-review.mp4
    audience=qa

engineering-review.mp4
    audience=engineering
```

All outputs should reuse the same compiled composition structure wherever practical.

# Phase 2 flagship demo

Create ONE source composition representing a technical/product workflow.

Use synthetic assets.

Generate:

### Shop Floor

Show:

- operational steps
- safety warning
- tool/assembly callout

### QA

Show:

- inspection points
- acceptance/check marker
- defect/inspection callout

### Engineering

Show:

- revision note
- design rationale
- technical review annotation

The underlying source footage must remain identical.

Only programmatic bindings/layers should change.

The demo should make the differentiator obvious within seconds.

### Phase 2 gate

PASS when:

```text
one composition
→ three substantially different useful videos
```

with no duplicated source composition.

STOP and report.

---

# PHASE 3 — Structured annotation system

## Goal

Separate annotations from hard-coded composition definitions.

Allow organizations to treat comments/instructions/review information as data.

## 3A. Annotation schema

Design a small bounded schema.

Example:

```json
{
  "id": "qa-017",
  "text": "Inspect weld seam",
  "startFrame": 240,
  "endFrame": 360,
  "audiences": ["qa", "shopfloor"],
  "severity": "warning",
  "x": 720,
  "y": 280
}
```

Potential supported properties:

```text
id
text
start/end
audiences
category
severity
position
target position
style reference
```

Do NOT add arbitrary extensible scripting fields.

## 3B. Annotation categories

Support simple categories such as:

```text
instruction
information
warning
inspection
revision
comment
```

Categories may map to reusable visual styles.

## 3C. Data-to-media generation

Annotations should generate the appropriate:

```text
text
callout
shape
highlight
```

from structured data.

This turns:

```text
data
→ visual communication
```

into a first-class VideoLab capability.

## 3D. External data files

Allow annotation data to be separate from the core composition.

Example:

```text
project.json
annotations.json
batch.json
```

The same base project can therefore consume different review/comment datasets.

## Phase 3 demo

Same base media:

```text
product-demo.mp4
```

plus:

```text
qa-comments.json
shopfloor-instructions.json
engineering-review.json
```

produce different output families.

### Gate

PASS only if the base composition does not need to be duplicated merely to support different annotation sets.

STOP and report.

---

# PHASE 4 — Normal editor quality improvements

## Goal

Strengthen mainstream usefulness while preserving programmatic identity.

Choose features based on genuine usability rather than checkbox competition.

Candidate capabilities:

```text
simple title templates
fade in/out
audio fade
basic audio ducking
simple image overlays
logo/watermark
background colors
simple aspect-ratio output variants
basic speed change if architecture permits safely
simple transition library
```

Do NOT automatically implement all candidates.

First rank them according to:

1. user value
2. architectural fit
3. implementation complexity
4. deterministic semantics
5. testability

Implement only the best subset.

# Explicitly defer heavyweight NLE features

Unless separately authorized, do NOT implement:

```text
professional color grading
node editor
multicam
complex masks
motion tracking
AI object removal
GPU effects ecosystem
professional audio workstation features
plugin ecosystem
After Effects-style compositing
unbounded timelines
full Premiere replacement
```

Those are not required to prove VideoLab's value.

---

# PHASE 5 — Revision and variant generation

## Goal

Make VideoLab excellent at regenerating media when source data changes.

Introduce first-class concepts for:

```text
revision
product variant
locale
audience
customer
environment
```

Do not hard-code industry semantics into the engine.

They should simply be typed parameters/data.

Example:

```text
audience = qa
revision = B
product = ModelX
locale = en-IN
```

Then:

```text
same composition
      ↓
many deterministic build variants
```

## Variant matrix

Allow bounded variant generation such as:

```text
QA / Rev B / Model X
QA / Rev C / Model X
Shop Floor / Rev C / Model X
Customer / Rev C / Model X
```

Avoid accidental combinatorial explosion.

Require explicit requested variants.

---

# PHASE 6 — Video as build artifact

## Goal

Prove the concept that video can participate in software-like build automation.

Conceptually:

```text
source media
+
composition
+
annotations
+
parameters
       ↓
VideoLab build
       ↓
versioned MP4/WebM
```

Support a CI-friendly command surface.

Example conceptual command:

```text
videolab build project.json \
  --audience qa \
  --revision C \
  --output review-C.mp4
```

Exact CLI design should follow existing conventions.

Outputs should expose provenance where practical:

```text
composition hash
input fingerprint
parameter binding
VideoLab version/commit
FFmpeg version
output hash
```

Do not claim cross-machine byte equality beyond current guarantees.

---

# PHASE 7 — Industrial/reference scenario

Only after generic capabilities exist.

Do NOT turn VideoLab into a CAD-specific product.

Build a reference scenario demonstrating the generic model in an engineering context.

Conceptual flow:

```text
CAD / 3D system
     ↓
rendered animation
     ↓
VideoLab
     +
structured annotations
     +
role parameters
     ↓
Engineering review
Shop-floor instruction
QA inspection
Service/training video
```

VideoLab manipulates the rendered media.

It does NOT claim to understand CAD geometry unless explicit model metadata is supplied.

Document this boundary clearly.

# Other candidate domain demonstrations

The architecture must remain generic enough to support later examples such as:

```text
manufacturing
construction/BIM
automotive service
aerospace maintenance
energy/utilities
medical-device training
laboratory workflows
software QA evidence
product demos
training/e-learning
marketing localization
field service
```

Do not encode these industries into the engine.

They are applications of generic programmable-video capability.

---

# PHASE 8 — Performance / longer-form backend

Current correctness-first programmable rendering has intentionally bounded resource limits.

Do not prematurely optimize.

When real use cases exceed those limits, design an optimized backend while preserving the semantic oracle:

```text
Composition
   ↓
Snapshot
   ↓
SceneAt(frame)
```

The existing reference implementation remains the semantic truth.

Optimization must preserve observable meaning.

Possible future strategies may include:

```text
state grouping
segment generation
filter reuse
timeline interval lowering
fewer per-frame FFmpeg nodes
GPU/backend research
```

Do not choose an optimization strategy before profiling.

---

# Product UX principle

The public UX should communicate two levels.

## Level 1 — Familiar

A normal video user should immediately understand:

```text
clips
timeline
text
audio
transitions
overlays
preview
export
```

## Level 2 — Differentiator

Then reveal:

```text
parameters
conditions
data
roles
batch variants
deterministic regeneration
automation
```

The experience should communicate:

> You can use VideoLab like a small composition tool.

and then:

> But unlike ordinary editors, the composition can become a reusable program.

---

# Recommended positioning

Avoid:

> AI video editor

Avoid:

> Premiere alternative

Avoid:

> CAD video system

Avoid:

> manufacturing video tool

Prefer something conceptually similar to:

> **Programmable video composition for repeatable media.**

or:

> **Design a video once. Generate the variants you need.**

or:

> **Video composition that behaves like code.**

Do not finalize marketing language in engineering code without explicit approval.

---

# Browser product direction

The browser workbench should remain a credible demonstration.

Near-term:

```text
playback
demo selection
timeline visualization
parameter selection
frame inspection
source view
export/download
```

Later, if justified:

```text
parameter controls
audience selector
annotation preview
variant comparison
```

Do NOT build a full visual editor prematurely.

A visual authoring UI can be considered after the composition model has stabilized.

---

# Quality rules

Every phase must preserve:

- immutable composition semantics where applicable
- validated bindings
- deterministic evaluation
- explicit resource bounds
- secure process invocation
- no raw shell execution
- no arbitrary FFmpeg-expression injection
- input protection
- atomic output replacement
- existing source normalization rules
- current failure semantics
- reproducibility documentation

Never remove or weaken an assertion just to make a new feature pass.

---

# Deployment rules

A development phase does NOT automatically mean public deployment.

For every phase distinguish:

```text
implemented
validated
MVP-worthy
publicly hosted
```

These are separate decisions.

Do not deploy half-complete capabilities to:

```text
soundscript.net/labs/videolab/
```

The public Lab should expose only features that pass their acceptance gate.

Experimental branch development may continue beyond what is publicly hosted.

---

# Version/milestone approach

Do not create excessive public version noise.

Use meaningful Labs milestones only.

Conceptually:

```text
v0.2 — current programmable composition MVP
v0.3 — familiar editing + annotations foundation
v0.4 — audience/variant generation
v0.5 — structured annotation/data workflow
```

Exact numbering must be chosen after inspecting current tags/history.

Do not retag existing preserved releases.

---

# Required report after every phase

Provide:

## Summary
What user problem was solved.

## Architecture
What changed and why.

## Files changed
Exact files and rationale.

## Compatibility
What existing semantics remained unchanged.

## Tests
Exact command and counts.

## Demonstration
Concrete example proving the phase.

## Public deployment recommendation

One of:

```text
READY FOR LAB PUBLICATION
```

or:

```text
KEEP PRIVATE/BRANCH-ONLY
```

with rationale.

## Next phase

Describe it, but DO NOT implement it without explicit authorization.

---

# Ultimate product test

VideoLab succeeds if both statements become true:

### Conventional user

> “I recognize this as a video composition tool.”

### Programmatic user

> “I can define the composition once and generate many controlled outputs instead of manually editing every version.”

That combination is the product advantage.

---

# Non-goals

Do NOT:

- merge Labs into core SoundScript
- turn SoundScript into a video language
- build a full professional NLE
- add AI merely for marketing
- introduce cloud infrastructure without need
- add databases/accounts/collaboration prematurely
- implement every industry use case
- optimize before profiling
- reduce validation quality
- remove existing deterministic guarantees
- publish unfinished features

Keep VideoLab small, understandable, deterministic and independently extractable.

---

# First execution instruction

For the next Codex session:

**Execute Phase 0 and Phase 1 only.**

Do not start Phase 2.

After Phase 1 validation, stop and provide the required report so the next development decision can be made independently.

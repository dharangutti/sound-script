# Phase 2 implementation decisions

Keep existing decimal parameters and SetMany unchanged. Add typedParameters for
bounded enums (1–32 identifier options) and booleans, sharing one 64-name limit.
No runtime strings are needed: enum symbols cover the audience workflow.
SetBindings accepts JSON scalars, validates mixed numeric/typed updates together,
and freezes copies in Snapshot. Bindings exposes all values; Values remains the
legacy decimal projection. CLI and batch use the same atomic path.

Conditions use the existing closed parser/typed AST with quoted enum symbols,
boolean literals, equality, &&/|| and parentheses. Numeric transforms cannot use
non-numeric values. Enum symbols are validated against the declaration domain.
Groups are named, non-nested composition predicates. Membership applies to video,
shape, text, callout, audio and expanded sequences. Group and element predicates
combine; SceneAt retains excluded elements and records group membership.

One synthetic housing/bearing clip is identical across all audience snapshots.
One audience.json and one batch create shopfloor/qa/engineering views. The browser
adds explicit enum/boolean selectors and three frozen variants. No duplicated
composition, cloud state or production changes.

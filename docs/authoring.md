# Authoring ergonomics

SoundScript keeps one source → parser/AST → interpreter → timeline → preview/export
pipeline. The conveniences below are compile-time lowering into that existing AST;
they do not add a second runtime or a domain-specific language.

## Comments

Use `//` for a line comment and `/* ... */` for a block comment. Comment markers in
quoted strings, URLs, musical notation, and hex colors remain literal. Diagnostics
keep the source line and column after comments are removed.

```ss
// A short cue
visual "card" for 2s { /* style is below */
    shape rectangle
    fill "#2563eb" // quoted colors are safe
}
```

## Constants and markers

`let` supports numbers, times, and strings. `marker` is a named time value. Values
resolve in the compiler, before interpretation, and are immutable and file-local.
Numeric expressions use only `+`, `-`, `*`, `/`, and parentheses.

```ss
let centerX = 640
let gap = 380
let primary = "#2563eb"
let sectionStart = 2s
marker details = sectionStart

visual "engineering" for 6s at details {
    shape roundedRectangle
    fill primary
    set x centerX + gap
    set y 720 / 2
    set width 260
    set height 104
}
```

## Reusable visual styles

Styles lower into the existing `VisualPresentation` values. A visual can `use` one
style and override individual properties locally. Styles support `fill`, `stroke`,
`strokeWidth`, and `fontSize`; shape and text stay on the visual so invalid
shape/property combinations remain diagnosable.

```ss
style "teamBox" {
    fill "#ffffff"
    stroke "#64748b"
    strokeWidth 2
}

visual "engineering" for 6s at 0s {
    shape roundedRectangle
    use "teamBox"
    set x 640
    set y 480
    set width 260
    set height 104
}
```

## Playground

The existing editors now provide syntax highlighting for comments, strings, colors,
notes, durations, instruments, dynamics, keywords, and properties. `Format`,
`Compile / Validate`, contextual completion (`Ctrl+Space`), hover help, block
outline, bracket/block selection, find/replace, deterministic constant rename,
color editing, note transposition, duration editing, and visual duplication are
available in the editor. Validation runs after a short idle delay and separates
errors from warnings with clickable line/column locations.

Music validation reports tracks, scheduled duration, event count, bar/beat marks,
selected-range audition, mute/solo, and note/instrument preview. Visual validation
reports the resolved timeline, active warnings, visual count, audio/visual
durations, and the fixed 1280×720 export profile. Warnings never modify timing.

The built-in `visual-temporal` Playground example uses comments, `let`, `marker`,
`style`, `use`, and numeric constants beside the legacy examples.

## Compatibility

All existing constructs remain valid. New declarations are consumed before runtime;
programs that do not use them produce the same AST, deterministic events, visual
states, and media bytes. Imports still require the CLI or a host that can resolve
their base directory.

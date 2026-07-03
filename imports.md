# Imports

Compose multi-file SoundScript projects with `import`.

## Syntax

```ss
import "relative/path.ss"
```

- Paths must be **relative** to the importing file
- `.ss` extension is optional if the file exists with that suffix
- Absolute paths are rejected

## Behavior

```
main.ss
  └─ import "lib.ss"
       └─ import "shared.ss"   (nested imports allowed)
```

1. `ProgramLoader` recursively resolves imports before interpretation
2. ASTs merge into a single `ProgramNode`
3. **Later definitions override earlier** ones (entry file wins over all imports)
4. **Duplicate block names** emit a warning when overridden
5. **Circular imports** throw an error

## Example

**`import-lib.ss`**

```ss
block intro {
    C4 q E4 q G4 q
}
```

**`imports.ss`**

```ss
import "import-lib.ss"

track melody {
    play intro
    C5 h
}
```

```bash
dotnet run --project src/SoundScript.Cli -- run examples/imports.ss
```

## Import Resolution Diagram

```
┌─────────────┐
│  entry.ss   │
└──────┬──────┘
       │ import "a.ss"
       ▼
┌─────────────┐     import "b.ss"     ┌─────────────┐
│   a.ss      │ ────────────────────► │   b.ss      │
└─────────────┘                       └─────────────┘
       │                                       │
       └───────────────┬───────────────────────┘
                       ▼
              ┌─────────────────┐
              │  MergedProgram  │
              │  (single AST)   │
              └─────────────────┘
```

## Warnings

| Condition | Warning |
|-----------|---------|
| Duplicate block/track name | `Duplicate block name 'X' — later definition overrides earlier.` |

## CLI

The CLI uses `ProgramLoader.Load()` and surfaces import warnings:

```bash
dotnet run --project src/SoundScript.Cli -- run examples/imports.ss
```

## Related

- [blocks.md](blocks.md) — imported blocks
- [architecture.md](architecture.md) — ProgramLoader

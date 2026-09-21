# Corpus v2026.07 (pilot)

July 2026 pilot snapshot for large-scale lemma dictionaries.

## Layout

```
corpus/v2026.07/
  manifest.json       # corpus manifest (see docs/MANIFEST.md)
  en/
    pilot-1k.txt      # 1000 curated lemmas (one per line, Phase 7)
    lemmas.json       # harvested entries (audio metadata + license)
    SOURCES.md        # attribution for CC0 / CC-BY imports
  audio/
    en/               # 44.1 kHz mono 16-bit PCM WAV per lemma
  es/
    lemmas.json       # stub — pilot list deferred to v2026.08
  fr/
    lemmas.json       # stub — pilot list deferred to v2026.08
```

## Status

| Locale | Pilot list | Lemma harvest |
|--------|------------|---------------|
| `en` | 1000 lemmas (`pilot-1k.txt`) | **66** pronunciations: 61 with declared Commons sources, 5 with unresolved provenance; see [SOURCES.md](en/SOURCES.md) |
| `es` | Deferred | Stub only |
| `fr` | Deferred | Stub only |

Locale packs under `data/` remain the runtime source for SoundScript engines. This corpus directory is versioned separately per [VERSIONING.md](../../docs/VERSIONING.md).

## Validation

```bash
node scripts/corpus-provenance.cjs
node --test scripts/corpus-provenance.test.cjs
```

Run from the SoundScript repository root. Checks source metadata and the generated
source list; it does not establish license validity or human/source verification.
Upstream wordbank schema/fixture tests also run in the .NET suite. Do not infer
CC0 status from a historical `license` field without the original receipt.

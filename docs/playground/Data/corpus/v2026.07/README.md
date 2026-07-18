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
| `en` | 1000 lemmas (`pilot-1k.txt`) | **66** harvested pronunciations (Commons CC0/CC-BY) — includes full Jingle Bells word set |
| `es` | Deferred | Stub only |
| `fr` | Deferred | Stub only |

Locale packs under `data/` remain the runtime source for SoundScript engines. This corpus directory is versioned separately per [VERSIONING.md](../../docs/VERSIONING.md).

## Validation

```bash
python3 scripts/validate.py
```

Checks corpus manifest, lemma stubs, pilot line count, and CI fixture overlap.

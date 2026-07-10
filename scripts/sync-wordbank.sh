#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SOURCE="${WORDBANK_SOURCE:-$ROOT/wordbank}"
TARGET="$ROOT/src/SoundScript.Wordbank/Data"

if [[ ! -d "$SOURCE/data" ]]; then
  echo "Wordbank source not found at $SOURCE" >&2
  echo "Set WORDBANK_SOURCE to the soundscript-wordbank checkout, or add the wordbank git submodule." >&2
  exit 1
fi

cp "$SOURCE/manifest.json" "$TARGET/manifest.json"

for locale in "$SOURCE/data"/*; do
  code="$(basename "$locale")"
  rm -rf "$TARGET/$code"
  mkdir -p "$TARGET/$code/words"
  cp "$locale/"*.json "$TARGET/$code/"
  if [[ -d "$locale/words" ]]; then
    cp "$locale/words/"*.json "$TARGET/$code/words/"
  fi
done

if [[ -d "$SOURCE/corpus/v2026.07" ]]; then
  audio_backup=""
  if [[ -d "$TARGET/corpus/v2026.07/audio" && ! -d "$SOURCE/corpus/v2026.07/audio" ]]; then
    audio_backup="$(mktemp -d)"
    cp -R "$TARGET/corpus/v2026.07/audio" "$audio_backup/"
  fi

  rm -rf "$TARGET/corpus"
  mkdir -p "$TARGET/corpus"
  cp -R "$SOURCE/corpus/v2026.07" "$TARGET/corpus/"

  if [[ -n "$audio_backup" ]]; then
    cp -R "$audio_backup/audio" "$TARGET/corpus/v2026.07/"
    rm -rf "$audio_backup"
    echo "Preserved embedded corpus audio (submodule has no audio/ yet)"
  fi

  echo "Synced corpus v2026.07 (metadata + audio)"
fi

echo "Synced wordbank data from $SOURCE to $TARGET"

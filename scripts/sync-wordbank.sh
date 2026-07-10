#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SOURCE="${WORDBANK_SOURCE:-$ROOT/../soundscript-wordbank}"
TARGET="$ROOT/src/SoundScript.Wordbank/Data"

if [[ ! -d "$SOURCE/data/en" ]]; then
  echo "Wordbank source not found at $SOURCE" >&2
  echo "Set WORDBANK_SOURCE to the soundscript-wordbank checkout." >&2
  exit 1
fi

rm -rf "$TARGET/en"
mkdir -p "$TARGET/en/words"
cp "$SOURCE/data/en/"*.json "$TARGET/en/"
cp "$SOURCE/data/en/words/"*.json "$TARGET/en/words/"

echo "Synced wordbank data from $SOURCE to $TARGET"

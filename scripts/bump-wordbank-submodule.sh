#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

if [[ ! -d wordbank ]]; then
  echo "Submodule wordbank/ not found. Run: git submodule update --init --recursive" >&2
  exit 1
fi

echo "Updating wordbank submodule..."
git submodule update --remote wordbank

echo "Syncing embedded data..."
./scripts/sync-wordbank.sh

if git diff --quiet src/SoundScript.Wordbank/Data wordbank; then
  echo "Wordbank data already up to date."
  exit 0
fi

echo "Updated files:"
git status --short src/SoundScript.Wordbank/Data wordbank

cat <<'EOF'

Next steps:
  git add wordbank src/SoundScript.Wordbank/Data
  git commit -m "Bump wordbank submodule to $(git -C wordbank rev-parse --short HEAD)"
EOF

#!/usr/bin/env bash
#
# generate-examples.sh [--check]
#
# Each validated skill ships an example/ project — a compilable projection of its assets/*.cs
# with the <Namespace> placeholder substituted for a fixed namespace. This script keeps that
# projection honest: it regenerates each example's .cs from the skill's assets/ so the committed
# example can't silently drift from what the skill actually ships.
#
#   (no args)   Regenerate every example's .cs in place (the assets/ are the source of truth).
#   --check     Don't write; diff generated output against the committed files and FAIL on drift.
#
# Which files and which fixed namespace come from each example/.generate.json. A skill whose
# assets/ folder is absent (e.g. the skill still lives on an unmerged branch) is SKIPPED — so
# this is safe to run in CI before the skill PRs merge.
set -euo pipefail

MODE="write"
[[ "${1:-}" == "--check" ]] && MODE="check"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
status=0
checked=0

while IFS= read -r manifest; do
  ex_dir="$(dirname "$manifest")"
  skill_dir="$(dirname "$ex_dir")"
  skill="$(basename "$skill_dir")"
  assets_dir="$skill_dir/assets"

  if [[ ! -d "$assets_dir" ]]; then
    echo "skip $skill — no assets/ on this branch"
    continue
  fi

  ns="$(python3 -c "import json;print(json.load(open('$manifest'))['namespace'])")"

  while IFS= read -r f; do
    src="$assets_dir/$f"
    dst="$ex_dir/$f"
    if [[ ! -f "$src" ]]; then
      echo "ERROR ($skill): '$f' is listed in .generate.json but missing from assets/"
      status=1
      continue
    fi
    checked=$((checked + 1))
    if [[ "$MODE" == "check" ]]; then
      if ! sed "s/<Namespace>/$ns/g" "$src" | diff -u "$dst" - >/dev/null 2>&1; then
        echo "DRIFT ($skill): $dst is out of sync with $src — run scripts/generate-examples.sh"
        status=1
      fi
    else
      sed "s/<Namespace>/$ns/g" "$src" > "$dst"
      echo "wrote $dst"
    fi
  done < <(python3 -c "import json;[print(a) for a in json.load(open('$manifest'))['assets']]")
done < <(find "$REPO_ROOT/plugins" -path "*/example/.generate.json" 2>/dev/null | sort)

if [[ "$MODE" == "check" ]]; then
  if [[ "$status" -eq 0 ]]; then
    echo "examples in sync with assets ($checked file(s) checked)"
  fi
else
  echo "regenerated $checked example file(s)"
fi
exit "$status"

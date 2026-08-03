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
# Which files and which fixed namespace come from each example/.generate.json, along with any
# extra "placeholders" the skill's assets carry (e.g. umbraco-custom-error-pages' <ErrorPageAlias>,
# which must resolve to a real Document Type alias for the example to do anything at runtime).
# A skill whose assets/ folder is absent (e.g. the skill still lives on an unmerged branch) is
# SKIPPED — so this is safe to run in CI before the skill PRs merge.
set -euo pipefail

MODE="write"
[[ "${1:-}" == "--check" ]] && MODE="check"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
status=0
checked=0

while IFS= read -r manifest; do
  # <skill>/examples/<approach>/.generate.json — one project per approach, all projecting the
  # same skill's assets/, so the skill dir is two levels up from the manifest.
  ex_dir="$(dirname "$manifest")"
  approach="$(basename "$ex_dir")"
  skill_dir="$(dirname "$(dirname "$ex_dir")")"
  skill="$(basename "$skill_dir")/$approach"
  assets_dir="$skill_dir/assets"

  if [[ ! -d "$assets_dir" ]]; then
    echo "skip $skill — no assets/ on this branch"
    continue
  fi

  # One sed program per skill: <Namespace> → the example's fixed namespace, plus every entry in
  # the manifest's optional "placeholders" map.
  sed_program="$(python3 - "$manifest" <<'PY'
import json, sys
manifest = json.load(open(sys.argv[1]))
subs = {"<Namespace>": manifest["namespace"]}
subs.update(manifest.get("placeholders") or {})
for placeholder, value in subs.items():
    print(f"s|{placeholder}|{value}|g")
PY
)"

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
      if ! sed -e "$sed_program" "$src" | diff -u "$dst" - >/dev/null 2>&1; then
        echo "DRIFT ($skill): $dst is out of sync with $src — run scripts/generate-examples.sh"
        status=1
      fi
    else
      sed -e "$sed_program" "$src" > "$dst"
      echo "wrote $dst"
    fi
  done < <(python3 -c "import json;[print(a) for a in json.load(open('$manifest'))['assets']]")
# -path wildcards match across '/', so prune build output or the copies the SDK drops into
# bin/ get picked up as if they were separate examples.
done < <(find "$REPO_ROOT/plugins" \
           \( -name bin -o -name obj \) -prune -o \
           -path "*/examples/*/.generate.json" -print 2>/dev/null | sort)

if [[ "$MODE" == "check" ]]; then
  if [[ "$status" -eq 0 ]]; then
    echo "examples in sync with assets ($checked file(s) checked)"
  fi
else
  echo "regenerated $checked example file(s)"
fi
exit "$status"

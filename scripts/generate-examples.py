#!/usr/bin/env python3
"""
generate-examples.py --manifest PATH --out DIR   # project the manifest's assets into DIR
generate-examples.py --lint                      # placeholder-check every manifest, write nothing

Each validated skill approach ships an example project that compiles the skill's OWN asset files,
with `<Namespace>` (and any other placeholder the manifest declares) substituted for fixed values.

Generation happens at BUILD TIME, into the project's obj/ — nothing is committed. That is the
point: the file the compiler and the HTTP fixtures see is the skill's asset by construction, so it
cannot drift from what the skill ships. There is no separate drift check to run or forget, because
there is no second copy to drift.

A skill whose assets/ folder is absent (e.g. it still lives on an unmerged branch) is SKIPPED, so a
build is safe before the skill's PR merges. A manifest with no `assets` is declare-only — it exists
to carry `requires`/`host` for an approach with no code of its own — and generates nothing.

Writes are content-comparing: an unchanged asset leaves the generated file's mtime alone, so MSBuild
can still skip the compile. Rewriting unconditionally would recompile the world on every build.
"""

import argparse
import json
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from check_placeholders import check  # noqa: E402

REPO_ROOT = pathlib.Path(__file__).resolve().parent.parent


def load(manifest_path):
    """Return (manifest, assets_dir, asset_names, subs) for a manifest, or None to skip."""
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))

    # <skill>/examples/<approach>/.generate.json — every approach of a skill projects the same
    # assets/, so the skill dir is two levels above the example dir.
    assets_dir = manifest_path.parent.parent.parent / "assets"
    if not assets_dir.is_dir():
        return None

    subs = {"<Namespace>": manifest["namespace"]}
    subs.update(manifest.get("placeholders") or {})
    return manifest, assets_dir, manifest.get("assets") or [], subs


def substitute(text, subs):
    for token, value in subs.items():
        text = text.replace(token, value)
    return text


def generate(manifest_path, out_dir):
    loaded = load(manifest_path)
    if loaded is None:
        print(f"skip {manifest_path.parent.name} — no assets/ on this branch")
        return 0
    _, assets_dir, asset_names, subs = loaded

    asset_paths = [assets_dir / name for name in asset_names]
    missing = [p for p in asset_paths if not p.is_file()]
    for path in missing:
        print(
            f"ERROR: '{path.name}' is listed in {manifest_path} but missing from assets/",
            file=sys.stderr,
        )
    if missing:
        return 1

    if check(manifest_path, asset_paths) != 0:
        return 1

    out_dir.mkdir(parents=True, exist_ok=True)
    for src in asset_paths:
        dst = out_dir / src.name
        rendered = substitute(src.read_text(encoding="utf-8"), subs)
        # Content-compare so an unchanged asset doesn't bump the mtime and force a recompile.
        if dst.exists() and dst.read_text(encoding="utf-8") == rendered:
            continue
        dst.write_text(rendered, encoding="utf-8")
    return 0


def manifests():
    """Every example manifest, skipping the copies the SDK drops into bin/ and obj/."""
    return sorted(
        p
        for p in (REPO_ROOT / "plugins").rglob("examples/*/.generate.json")
        if not {"bin", "obj"} & set(p.parts)
    )


def lint():
    status = 0
    checked = 0
    for manifest_path in manifests():
        loaded = load(manifest_path)
        if loaded is None:
            print(f"skip {manifest_path.parent.name} — no assets/ on this branch")
            continue
        _, assets_dir, asset_names, _ = loaded
        asset_paths = [assets_dir / name for name in asset_names if (assets_dir / name).is_file()]
        if asset_paths and check(manifest_path, asset_paths) != 0:
            status = 1
        checked += len(asset_paths)
    if status == 0:
        print(f"placeholders declared for every asset ({checked} file(s) checked)")
    return status


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=pathlib.Path)
    parser.add_argument("--out", type=pathlib.Path)
    parser.add_argument("--lint", action="store_true")
    args = parser.parse_args()

    if args.lint:
        return lint()
    if not args.manifest or not args.out:
        parser.error("--manifest and --out are required unless --lint is given")
    return generate(args.manifest, args.out)


if __name__ == "__main__":
    sys.exit(main())

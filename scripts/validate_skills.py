#!/usr/bin/env python3
"""Validate published skills against the open Agent Skills (SKILL.md) spec.

Checks every skill under plugins/*/skills/*/SKILL.md for:
  - parseable YAML frontmatter
  - required fields: name, description
  - name matches its folder name; lowercase alphanumeric + hyphens; <= 64 chars
  - description non-empty; <= 1024 chars
  - no non-portable (tool-specific) frontmatter keys
  - relative links/references in the body resolve to real files in the skill dir
  - no stray skill folders missing a SKILL.md
  - no symlinks inside published skills (Windows-hostile)
  - marketplace.json / plugin.json parse and reference real plugin dirs

Also maintains the skills index in AGENTS.md between the SKILLS-INDEX markers:
  python scripts/validate_skills.py --write-index   # regenerate index
  python scripts/validate_skills.py --check-index   # fail if index is stale (CI)

Exit code 0 = all good, 1 = validation errors found.
Requires: pyyaml
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

import yaml

REPO_ROOT = Path(__file__).resolve().parent.parent
PLUGINS_DIR = REPO_ROOT / "plugins"
AGENTS_MD = REPO_ROOT / "AGENTS.md"

INDEX_START = "<!-- SKILLS-INDEX:START"
INDEX_END = "<!-- SKILLS-INDEX:END -->"

# Open-standard fields (agentskills.io). Anything else is flagged so a skill
# never silently depends on one tool's extension.
ALLOWED_FRONTMATTER_KEYS = {"name", "description", "license", "allowed-tools", "metadata"}

NAME_RE = re.compile(r"^[a-z0-9]+(-[a-z0-9]+)*$")
NAME_MAX = 64
DESCRIPTION_MAX = 1024

# Markdown links [text](target) and bare backticked relative paths we care about.
MD_LINK_RE = re.compile(r"\[[^\]]*\]\(([^)\s]+)\)")

FRONTMATTER_RE = re.compile(r"\A---\s*\n(.*?)\n---\s*\n", re.DOTALL)


class Reporter:
    def __init__(self) -> None:
        self.errors: list[str] = []
        self.warnings: list[str] = []

    def error(self, path: Path, msg: str) -> None:
        self.errors.append(f"ERROR   {path.relative_to(REPO_ROOT)}: {msg}")

    def warn(self, path: Path, msg: str) -> None:
        self.warnings.append(f"WARNING {path.relative_to(REPO_ROOT)}: {msg}")


def parse_frontmatter(skill_md: Path, rep: Reporter) -> tuple[dict, str] | None:
    text = skill_md.read_text(encoding="utf-8")
    m = FRONTMATTER_RE.match(text)
    if not m:
        rep.error(skill_md, "missing YAML frontmatter block (--- ... ---) at top of file")
        return None
    try:
        data = yaml.safe_load(m.group(1))
    except yaml.YAMLError as exc:
        rep.error(skill_md, f"frontmatter is not valid YAML: {exc}")
        return None
    if not isinstance(data, dict):
        rep.error(skill_md, "frontmatter must be a YAML mapping")
        return None
    return data, text[m.end():]


def validate_frontmatter(skill_md: Path, fm: dict, folder_name: str, rep: Reporter) -> None:
    name = fm.get("name")
    description = fm.get("description")

    if not isinstance(name, str) or not name.strip():
        rep.error(skill_md, "frontmatter 'name' is required and must be a non-empty string")
    else:
        if name != folder_name:
            rep.error(skill_md, f"name '{name}' does not match folder name '{folder_name}'")
        if len(name) > NAME_MAX:
            rep.error(skill_md, f"name exceeds {NAME_MAX} characters ({len(name)})")
        if not NAME_RE.match(name):
            rep.error(skill_md, "name must be lowercase letters/digits with single hyphens (e.g. 'document-types')")

    if not isinstance(description, str) or not description.strip():
        rep.error(skill_md, "frontmatter 'description' is required and must be a non-empty string")
    else:
        if len(description) > DESCRIPTION_MAX:
            rep.error(skill_md, f"description exceeds {DESCRIPTION_MAX} characters ({len(description)})")
        lowered = description.lower()
        if not any(cue in lowered for cue in ("use when", "use this", "use for", "trigger", "use whenever")):
            rep.warn(skill_md, "description has no obvious 'when to use' cue — agents rely on this to decide whether to load the skill")

    unknown = set(fm) - ALLOWED_FRONTMATTER_KEYS
    if unknown:
        rep.error(
            skill_md,
            f"non-portable frontmatter key(s) {sorted(unknown)} — published skills may only use {sorted(ALLOWED_FRONTMATTER_KEYS)}",
        )


def validate_body_links(skill_md: Path, body: str, skill_dir: Path, rep: Reporter) -> None:
    for target in MD_LINK_RE.findall(body):
        if target.startswith(("http://", "https://", "mailto:", "#")):
            continue
        clean = target.split("#", 1)[0]
        if not clean:
            continue
        resolved = (skill_dir / clean).resolve()
        try:
            resolved.relative_to(skill_dir.resolve())
        except ValueError:
            rep.warn(skill_md, f"link '{target}' points outside the skill folder — bundled resources should live within it")
            continue
        if not resolved.exists():
            rep.error(skill_md, f"link '{target}' does not resolve to a file in the skill folder")


def validate_no_symlinks(skill_dir: Path, rep: Reporter) -> None:
    for p in skill_dir.rglob("*"):
        if p.is_symlink():
            rep.error(p, "symlink inside a published skill — breaks Windows checkouts; commit real files")


def validate_manifests(rep: Reporter) -> None:
    marketplace = REPO_ROOT / ".claude-plugin" / "marketplace.json"
    if marketplace.exists():
        try:
            json.loads(marketplace.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            rep.error(marketplace, f"invalid JSON: {exc}")
    for plugin_json in PLUGINS_DIR.glob("*/.claude-plugin/plugin.json"):
        try:
            json.loads(plugin_json.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            rep.error(plugin_json, f"invalid JSON: {exc}")


def collect_skills(rep: Reporter) -> list[dict]:
    """Return validated skill metadata for the index; report problems as we go."""
    skills: list[dict] = []
    if not PLUGINS_DIR.is_dir():
        return skills

    for plugin_dir in sorted(p for p in PLUGINS_DIR.iterdir() if p.is_dir()):
        skills_root = plugin_dir / "skills"
        if not skills_root.is_dir():
            continue
        for skill_dir in sorted(p for p in skills_root.iterdir() if p.is_dir()):
            skill_md = skill_dir / "SKILL.md"
            if not skill_md.is_file():
                # A folder with content but no SKILL.md is invisible to every agent.
                if any(skill_dir.iterdir()):
                    rep.error(skill_dir, "skill folder has no SKILL.md — it will not be discovered by any agent")
                continue

            parsed = parse_frontmatter(skill_md, rep)
            if parsed is None:
                continue
            fm, body = parsed
            validate_frontmatter(skill_md, fm, skill_dir.name, rep)
            validate_body_links(skill_md, body, skill_dir, rep)
            validate_no_symlinks(skill_dir, rep)

            skills.append(
                {
                    "plugin": plugin_dir.name,
                    "name": str(fm.get("name", skill_dir.name)),
                    "description": str(fm.get("description", "")).strip(),
                    "path": skill_md.relative_to(REPO_ROOT).as_posix(),
                }
            )
    return skills


def render_index(skills: list[dict]) -> str:
    if not skills:
        return (
            "_No skills published yet. This section is regenerated automatically when skills\n"
            "are added under `plugins/*/skills/`._"
        )
    lines = ["| Skill | Plugin | Use when |", "| --- | --- | --- |"]
    for s in skills:
        desc = s["description"].replace("|", "\\|").replace("\n", " ")
        if len(desc) > 200:
            desc = desc[:197] + "..."
        lines.append(f"| [`{s['name']}`]({s['path']}) | `{s['plugin']}` | {desc} |")
    return "\n".join(lines)


def splice_index(agents_text: str, index_block: str) -> str | None:
    start = agents_text.find(INDEX_START)
    end = agents_text.find(INDEX_END)
    if start == -1 or end == -1 or end < start:
        return None
    marker_line_end = agents_text.index("\n", start) + 1
    return agents_text[:marker_line_end] + index_block + "\n" + agents_text[end:]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write-index", action="store_true", help="regenerate the skills index in AGENTS.md")
    parser.add_argument("--check-index", action="store_true", help="fail if the AGENTS.md skills index is stale")
    args = parser.parse_args()

    rep = Reporter()
    validate_manifests(rep)
    skills = collect_skills(rep)

    if args.write_index or args.check_index:
        if not AGENTS_MD.exists():
            rep.error(AGENTS_MD, "AGENTS.md not found — cannot manage skills index")
        else:
            current = AGENTS_MD.read_text(encoding="utf-8")
            updated = splice_index(current, render_index(skills))
            if updated is None:
                rep.error(AGENTS_MD, "SKILLS-INDEX markers missing or malformed")
            elif args.write_index and updated != current:
                AGENTS_MD.write_text(updated, encoding="utf-8")
                print(f"Updated skills index in {AGENTS_MD.relative_to(REPO_ROOT)}")
            elif args.check_index and updated != current:
                rep.error(AGENTS_MD, "skills index is stale — run: python scripts/validate_skills.py --write-index")

    for w in rep.warnings:
        print(w)
    for e in rep.errors:
        print(e)

    print(f"\nValidated {len(skills)} skill(s): {len(rep.errors)} error(s), {len(rep.warnings)} warning(s)")
    return 1 if rep.errors else 0


if __name__ == "__main__":
    sys.exit(main())

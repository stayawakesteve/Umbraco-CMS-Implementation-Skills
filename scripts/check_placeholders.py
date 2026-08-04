#!/usr/bin/env python3
"""
check-placeholders.py MANIFEST ASSET...

Enforces the invariant that an asset may not carry a placeholder its .generate.json doesn't
declare. This exists because a missed placeholder is SILENT: `<filterAlias>` left unsubstituted
in `HasProperty("<filterAlias>")` still compiles, always returns false, and the feature it
guards quietly does nothing. Nothing else in the pipeline can notice that.

Two checks, both cheap and both necessary:

  1. UNDECLARED — a `<Token>` in a position where a placeholder is meaningful, that the manifest
     doesn't map. Positions are deliberately narrow: inside a double-quoted string literal, or in
     a `namespace` declaration. Narrow scoping is what makes this precise — C# generics
     (`Value<bool>`, `Task<IReadOnlyList<ITemplate>>`) and prose about XML elements
     (`// Only <loc> + <lastmod> are emitted`) are never in either position, so they never
     false-positive.

  2. UNUSED — a placeholder the manifest declares that appears in no listed asset. Catches the
     divergence the other way round: a renamed token, or a manifest copied between skills.

Limits worth knowing: string literals are matched per line, so a placeholder inside a verbatim
`@"..."` string spanning lines is not seen. In XML assets only attribute values are string
positions, so element-position tokens (`<Design>`) are ignored — which is correct, since those
are the XML's own structure, not placeholders.
"""

import json
import os
import re
import sys

# A <Token> that could be a placeholder. Requires no whitespace inside, so `a < b` never matches.
CANDIDATE = re.compile(r"<[A-Za-z][A-Za-z0-9_]*>")
# A double-quoted string literal, honouring backslash escapes.
STRING_LITERAL = re.compile(r'"(?:[^"\\]|\\.)*"')
NAMESPACE_DECL = re.compile(r"^\s*namespace\s")


def placeholder_positions(line):
    """Yield the substrings of `line` in which a <Token> would be a placeholder."""
    if NAMESPACE_DECL.match(line):
        yield line
    for match in STRING_LITERAL.finditer(line):
        yield match.group(0)


def short(path):
    """Repo-relative where possible — absolute paths bury the filename these errors are about."""
    try:
        return os.path.relpath(path)
    except ValueError:
        return path


def check(manifest_path, asset_paths):
    """Return 0 if every placeholder lines up, else 1 having explained why on stderr."""
    manifest = json.loads(open(manifest_path, encoding="utf-8").read())
    declared = {"<Namespace>": manifest["namespace"]}
    declared.update(manifest.get("placeholders") or {})

    errors = []
    seen = set()
    reported = set()

    for asset_path in asset_paths:
        with open(asset_path, encoding="utf-8") as handle:
            for number, line in enumerate(handle, 1):
                for fragment in placeholder_positions(line):
                    for token in CANDIDATE.findall(fragment):
                        seen.add(token)
                        # A token used twice on one line is one mistake, not two.
                        if token in declared or (asset_path, number, token) in reported:
                            continue
                        reported.add((asset_path, number, token))
                        errors.append(
                            f"  {short(asset_path)}:{number} carries {token}, which the "
                            f"manifest does not declare.\n    {line.strip()[:100]}"
                        )

    for token in sorted(set(declared) - seen - {"<Namespace>"}):
        errors.append(
            f"  declares {token}, but no listed asset uses it — "
            f"the manifest and the assets have diverged."
        )

    if errors:
        print(f"PLACEHOLDER ERROR ({short(manifest_path)}):", file=sys.stderr)
        print(
            "\n".join(errors)
            + "\n  An undeclared placeholder survives substitution as a literal string: it still "
            + "compiles,\n  so nothing complains, and the code can simply never find what it "
            + "looks for.",
            file=sys.stderr,
        )
        return 1
    return 0


if __name__ == "__main__":
    if len(sys.argv) < 3:
        sys.exit("usage: check_placeholders.py MANIFEST ASSET...")
    sys.exit(check(sys.argv[1], sys.argv[2:]))

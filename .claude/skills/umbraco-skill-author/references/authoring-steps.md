# Authoring steps

Follow these when building a new skill. End with the
[conformance checklist](conformance-checklist.md). Copy-paste skeletons are in
[`skill-template.md`](../templates/skill-template.md).

## Step 1 — Clarify scope

Pin down before writing anything (ask if ambiguous):

- **The task** the skill automates, phrased as user goals (these become trigger phrases).
- **Which plugin** — `content-modelling` vs `implementation`.
- **One approach or several?** An official-docs way *and* a custom/code way → plan a decision table
  and one reference file per approach.
- **Version compatibility** — target Umbraco version and any "introduced in vX" APIs.
- **Backoffice vs file-based** — needs the Umbraco Developer MCP / manual backoffice steps, or pure
  `.cs`/`.cshtml`? This drives the MCP→manual→fallback guidance.

## Step 2 — Scaffold the folder

```
plugins/<plugin>/skills/<kebab-case-name>/
├── SKILL.md            # required
├── references/         # one file per approach (optional if single, trivial approach)
├── assets/             # code templates with <Placeholder> tokens (optional)
├── scripts/            # deterministic helpers the skill runs (optional)
├── evals/evals.json    # objective, repeatable validation
└── templates/          # copy-paste skeletons for authoring (optional)
```

Folder name is kebab-case and must match the `name` in frontmatter.

## Step 3 — Write SKILL.md (keep it thin — it routes)

**Frontmatter**
- `name:` kebab-case, matches the folder.
- `description:` must contain both **trigger phrases** (real user wordings) AND explicit **SKIP**
  conditions (e.g. "non-Umbraco projects or Umbraco < 17"). This is what makes the skill fire at
  the right time — treat it as the most important line in the file.

**Body (in this order)**
1. One-paragraph intro.
2. **Decision table** if there's more than one approach (Reference | Source | Backoffice work |
   caching | best-for …), then a short "How to decide" with a default and the MCP→manual→fallback
   order.
3. **Version compatibility.**
4. **Best practices** — domain guidance, not just restated code.
5. **Validation** — a pointer to `evals/evals.json`.

Don't paste doc links here that already live in a reference file. Push per-approach detail and any
`if/else`-style branching into `references/`.

## Step 4 — Write references/<approach>.md (one per approach)

- Open with "When to choose this approach" and cross-link the alternative.
- List the documented **building blocks** with `.md` doc links (or links to sibling skills).
- Instruct: **fetch the docs before implementing** where the code isn't verbatim in `assets/`.
- Give the step sequence: discover project context → write files (with placeholder substitution) →
  build/verify → "Done" summary for the user.

## Step 5 — assets/ (only if code is needed)

- Code templates with `<Placeholder>` tokens (`<Namespace>`, `<filterAlias>`, …).
- Mark optional/removable lines with a comment.
- The template code itself already follows the best-practices section.

## Step 6 — Draft evals/evals.json

- Realistic, multi-step prompts (see `umbraco-skill-evaluator` for what makes a good prompt).
- One prompt per meaningful scenario: each approach, plus edge cases (headless, MCP-absent, …).
- `expectations[]` are **objective, checkable assertions** — not "the output is good".
- Include a **build-honesty** expectation.
- See the schema in [`skill-template.md`](../templates/skill-template.md), and `umbraco-sitemap`'s
  `evals/evals.json` for a worked example.

## Step 7 — Audit and hand off

- Run `umbraco-skill-validator` (links) and `umbraco-skill-code-analyzer` (code).
- Self-audit against the [conformance checklist](conformance-checklist.md).
- Hand off to `umbraco-skill-evaluator` to prove value against a baseline; iterate from results.

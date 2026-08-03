# Conformance checklist (self-audit)

Run this on the skill you're building before you ship it. Check each item against the actual files;
fix anything that fails. A skill is ready only when every applicable item passes.

**Placement & naming**
- [ ] Folder is kebab-case in the correct plugin (`content-modelling` vs `implementation`), or in
  `.claude/skills/` for authoring tooling
- [ ] `name` in frontmatter matches the folder name exactly

**Description (triggering)**
- [ ] `description` has concrete **trigger phrases** (real user wordings, not just "use for X")
- [ ] `description` has explicit **SKIP** conditions
- [ ] Doesn't collide with an existing skill's triggers (or the overlap is intentional and noted)

**SKILL.md body**
- [ ] Thin/routing — doesn't inline detail or `if/else` branching that belongs in references/assets
- [ ] Decision table + "how to decide" present when there's more than one approach
- [ ] Version compatibility stated
- [ ] Best-practices section is domain guidance, not just restated code
- [ ] Validation section points at `evals/evals.json`
- [ ] No doc link duplicated between SKILL.md and a reference file

**references/**
- [ ] One file per approach, each with "when to choose" + cross-link to the alternative
- [ ] All fetch-me doc links use the `.md` version (or link a sibling skill instead of raw docs)
- [ ] Says "fetch the docs first" wherever the code isn't verbatim in `assets/`

**assets/ (if present)**
- [ ] Templates use `<Placeholder>` tokens with optional/removable lines marked
- [ ] Template code follows the skill's own best-practices section

**scripts/ (if present)**
- [ ] Deterministic, repeatable work lives in a script the skill points at, not re-derived in prose

**evals/**
- [ ] `evals/evals.json` present with realistic, multi-step prompts (one per meaningful scenario)
- [ ] `expectations[]` are objective/checkable, including a build-honesty expectation

**Runtime gate (skip only if the skill ships no `assets/*.cs` — and say so)**
- [ ] `examples/<approach>/` exists per approach, with a `<ProjectReference>` from the reference
  instance for any approach whose behaviour is asserted
- [ ] `.generate.json` maps **every** placeholder the assets carry — a missed one survives into the
  generated code as a literal string, still compiles, and silently never matches
- [ ] `scripts/generate-examples.sh --check` passes (example hasn't drifted from `assets/`)
- [ ] Content the example depends on is declared via `requires`, not asserted by hand
- [ ] Fixture uses `ReferenceSiteFixture.Client`, restores any content it mutates, and asserts
  rendered content rather than just a status code
- [ ] The test was **proven able to fail** — broken deliberately, seen red, reverted

**Before shipping**
- [ ] Passes `umbraco-skill-validator` and `umbraco-skill-code-analyzer` (if available)
- [ ] Eval'd against a baseline with `umbraco-skill-evaluator`

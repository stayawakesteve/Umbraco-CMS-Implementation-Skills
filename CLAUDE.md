# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this repo is

A **Claude Code plugin marketplace** providing Umbraco skills, split into two plugins:

- **`umbraco-cms-content-modelling-skills`** (`plugins/content-modelling/`) — document
  types, element types, data types, compositions, content structure.
- **`umbraco-cms-implementation-skills`** (`plugins/implementation/`) — site build-out,
  templates, views, controllers, delivery.

It is a sibling to the Umbraco Backoffice Skills marketplace and follows the same conventions.

## Structure

```
.claude-plugin/marketplace.json   # Marketplace manifest — lists both plugins
plugins/<plugin>/
  .claude-plugin/plugin.json       # Per-plugin manifest
  skills/<skill-name>/SKILL.md      # Published skills (one folder per skill)
.claude/skills/                    # Repo-authoring skills (NOT published) — e.g. skill-creator
Umbraco-CMS.Skills/                # Reference Umbraco 17 instance (validation target)
Umbraco-CMS.Skills.sln
```

### Published vs authoring skills

- **Published skills** ship to users and live in `plugins/*/skills/`.
- **Authoring skills** (tooling to create/validate/maintain skills, such as
  `skill-creator`) live in `.claude/skills/` and are not part of any plugin.

Don't put authoring tooling in a plugin's `skills/` folder, and don't put
user-facing skills in `.claude/skills/`.

## Conventions

- **Skill folders** are kebab-case and each contains a `SKILL.md` with YAML frontmatter
  (`name`, `description`). Match the structure of the Umbraco Backoffice Skills repo.
- **Versions** are kept in sync between `marketplace.json` and each plugin's `plugin.json`.
  When bumping a plugin version, update both.
- **Marketplace name:** `umbraco-cms-implementation-marketplace`.

## Workflow

Changes land via **branch → pull request → squash-merge into `main`**:

1. Branch off `main` (never commit directly to `main`).
2. Commit, push, open a PR with `gh pr create --base main`.
3. Address review, then `gh pr merge <n> --squash --delete-branch`.
4. `git checkout main && git pull --ff-only`.

Only commit/push/merge when explicitly asked.

## Reference instance

`Umbraco-CMS.Skills/` (+ `Umbraco-CMS.Skills.sln`) is a committed Umbraco **17** web project
(`net10.0`, `Umbraco.Cms 17.5.3`, SQLite unattended install, **Clean** starter kit) used to
validate that skill output compiles and serves. It was scaffolded with the **Package Script
Writer CLI** (`psw`); the exact command is in the README, and package versions are centrally
managed in `Umbraco-CMS.Skills/Directory.Packages.props`. Only the scaffolding is committed —
the runtime SQLite DB, `bin/`, `obj/`, the `Umbraco.Skills.Sandbox/` scratch project, and
`.local-nuget-feed/` are `.gitignore`d (the project's own nested `.gitignore` covers Umbraco
runtime paths), and Clean re-installs on first boot. **Never commit** runtime data.

**Deterministic validation (`dotnet test`).** Runtime proof that a skill's code compiles and
serves correctly is a model-free `dotnet test` gate:
- Each validated skill ships `plugins/implementation/skills/<skill>/example/` — a
  `Microsoft.NET.Sdk.Razor` project compiling the skill's *chosen-approach* `assets/*.cs` with
  the `<Namespace>` placeholder substituted for `Umbraco.Skills.Examples.<Skill>` (plus any other
  placeholder the assets carry, via the manifest's `placeholders` map). `assets/` stay
  the source of truth; `scripts/generate-examples.sh [--check]` regenerates/verifies the example
  (skips skills whose `assets/` aren't on the current branch). Host wiring a skill needs
  (e.g. the 500 page's `UseExceptionHandler`) ships as an `IComposer`/`IUmbracoPipelineFilter`
  **inside the example**, so the shared instance's `Program.cs` is never touched.
- `Umbraco-CMS.Skills/Umbraco-CMS.Skills.csproj` `<ProjectReference>`s every example (one shared
  host); `Program.cs` exposes `public partial class Program` for the test host.
- `Umbraco-CMS.Skills.TestHost/` (NUnit + `WebApplicationFactory`) boots the instance in-process
  against an isolated test SQLite DB and HTTP-asserts each skill (see `SitemapTests.cs`,
  `CustomErrorPagesTests.cs`). The host is booted **once per assembly** by `ReferenceSiteFixture`
  and shared via `ReferenceSiteFixture.Client` — Umbraco's process-wide static state means a
  second host in the same process breaks whichever fixture runs later. Runs in CI
  (`.github/workflows/validate-skills.yml`).

The `umbraco-reference-instance` authoring skill (in `.claude/skills/`) documents this gate and
also offers a manual boot/`try` harness (`https://localhost:44372`, `admin@example.com` /
`1234567890`) for interactive poking and backoffice-dependent steps. It complements
`umbraco-skill-evaluator` (which grades whether Claude *writes* the right code) by proving the
code *runs*.

## Source references

Skills are most accurate when the Umbraco source is available as a working directory:

```bash
/add-dir /path/to/Umbraco-CMS
```

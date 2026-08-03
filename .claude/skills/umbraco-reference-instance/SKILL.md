---
name: umbraco-reference-instance
description: >
  Boot the committed reference Umbraco site and prove that a skill's produced code actually
  runs. Use this whenever you want to run/validate an implementation or content-modelling
  skill against a real, running Umbraco instance — e.g. "test the sitemap skill in Umbraco",
  "does this skill's code actually work", "boot the reference site", "run the instance",
  "validate <skill> end-to-end". This is the runtime counterpart to `umbraco-skill-evaluator`:
  the evaluator grades whether Claude *writes* the right code; this skill proves the code
  *builds and serves* in Umbraco. Authoring/maintainer tooling — not shipped in any plugin.
---

# Umbraco reference instance

`Umbraco-CMS.Skills/` (at the repo root, with `Umbraco-CMS.Skills.sln`) is a real Umbraco
**17** web project used only for validating skills. It:

- targets `net10.0`, `Umbraco.Cms 17.5.3` (scaffolded via the `psw` CLI; versions live in
  `Umbraco-CMS.Skills/Directory.Packages.props`) — matching the skills' "Umbraco 17+" target;
- installs **unattended** on first boot into a SQLite DB (`admin@example.com` / `1234567890`);
- ships the **Clean** starter kit, so there is real content (Document Types, templates,
  published pages) for skill output to run against — `/sitemap.xml` has URLs to emit, a
  missing page has a site to render a 404 into, the Delivery API returns real nodes.

The runtime DB and build output are `.gitignore`d — only the project scaffolding is
committed, and Clean re-installs on first boot. **Never commit** a populated `App_Data`
SQLite DB, `bin/`, `obj/`, the `Umbraco.Skills.Sandbox/` scratch project, or
`.local-nuget-feed/`.

## Environment conventions

Same variable names as the backoffice test-runner, so tooling is interchangeable:

| Variable | Default | Meaning |
|---|---|---|
| `UMBRACO_URL` | `https://localhost:44372` | Base URL the instance binds to (also `http://localhost:60372`). |
| `UMBRACO_USER_LOGIN` | `admin@example.com` | Unattended admin email. |
| `UMBRACO_USER_PASSWORD` | `1234567890` | Unattended admin password. |

The scripts read these from the environment and fall back to the defaults above.

## Just boot the site

```bash
.claude/skills/umbraco-reference-instance/scripts/instance.sh boot    # start + wait until ready
.claude/skills/umbraco-reference-instance/scripts/instance.sh status  # is it up?
.claude/skills/umbraco-reference-instance/scripts/instance.sh stop     # stop only if this script started it
```

`boot` is idempotent: if the instance is already answering on `UMBRACO_URL` it reuses it and
does not start a second one. First boot is slow (unattended install + Clean import) — the
script polls for up to ~4 minutes.

Log in to the backoffice at `<UMBRACO_URL>/umbraco` with the credentials above. For
clicking through the backoffice, use the `umbraco-chrome-navigation` skill.

## Validate a skill deterministically (the CI gate)

Runtime validation is a **`dotnet test` gate — no LLM, reproducible pass/fail**. Each validated
skill ships a committed `example/` project that compiles its chosen-approach `assets/*.cs` with
the `<Namespace>` placeholder substituted for a fixed namespace; the reference instance
references every example, and `Umbraco-CMS.Skills.Tests` boots that one host in-process
(`WebApplicationFactory`) and asserts each skill's endpoints over HTTP.

```bash
dotnet test Umbraco-CMS.Skills.sln          # boots the instance in-process, asserts skill endpoints
scripts/generate-examples.sh --check        # fail if any example/ drifted from its skill's assets/
```

To add a skill to the gate:

1. Create `plugins/implementation/skills/<skill>/example/` with:
   - `<Skill>.Example.csproj` — `Microsoft.NET.Sdk.Razor`, `PackageReference Umbraco.Cms.Web.Website 17.*`.
   - `.generate.json` — `{ "namespace": "Umbraco.Skills.Examples.<Skill>", "assets": [ …chosen files… ] }`,
     plus an optional `"placeholders"` map for any *other* placeholder the assets carry, resolved to
     something that exists in the instance (e.g. umbraco-custom-error-pages maps
     `<ErrorPageAlias>` → `error`, Clean's Error node, so the code has a real node to find).
   - the generated `.cs` (run `scripts/generate-examples.sh`). Pick **one** approach for
     mutually-exclusive assets — e.g. the sitemap skill's `SitemapController` and
     `SitemapIndexController` both map `GET /sitemap.xml`, so the example lists only Approach A
     (`SitemapController` + `SitemapComposer` + `SitemapCacheInvalidator`).
   - a **host-wiring shim** if the skill needs `Program.cs`/config changes: ship them as an
     `IComposer` + `IUmbracoPipelineFilter` in the example so the shared instance is never edited.
     See `umbraco-custom-error-pages/example/ExampleHostWiring.cs`, which applies the 500 page's
     `UseExceptionHandler` (via `PrePipeline`) and `ReservedPaths` entry that way, and adds a
     deliberately-throwing endpoint so a 500 can be provoked. Keep such harness files out of
     `.generate.json` — the generator only rewrites the files it lists.
2. Add a `<ProjectReference>` to the example in `Umbraco-CMS.Skills/Umbraco-CMS.Skills.csproj`.
3. Add an NUnit fixture in `Umbraco-CMS.Skills.Tests/` that HTTP-asserts the skill's behaviour
   (see `SitemapTests.cs` / `CustomErrorPagesTests.cs`). Use the shared host via
   `ReferenceSiteFixture.Client` — **don't** `new ReferenceSiteFactory()` per fixture. Umbraco
   holds process-wide static state (`StaticServiceProvider`, which the `Umbraco.Extensions`
   friendly extension methods resolve through), so a second host booted after a first is disposed
   makes skill code fail in whichever fixture runs later — a fixture that passes alone and fails in
   a full run is this bug.

Then check the test actually gates: change the skill's behaviour (e.g. point a `.generate.json`
placeholder at a Document Type that doesn't exist), confirm the fixture goes red, and revert. A
test that passes either way proves nothing about the skill.

`assets/*.cs` stay the single source of truth; the committed `example/` is a reviewable
projection kept honest by `generate-examples.sh --check` (which skips skills whose `assets/`
aren't on the current branch, so it's safe pre-merge).

## Explore interactively (manual boot)

For poking at a skill by hand, or for the parts a `dotnet test` can't cover (backoffice setup —
e.g. sitemap Approach B's Document Type + content node, driven via `umbraco-chrome-navigation`),
use the manual harness:

```bash
scripts/instance.sh try plugins/implementation/skills/umbraco-sitemap  # materialize assets → sidecar → reference
scripts/instance.sh boot
curl -sk https://localhost:44372/sitemap.xml
scripts/instance.sh reset                                              # restore the committed instance
```

`try` copies **every** `assets/*.cs` into a sidecar library (namespace-substituted) — so for
mutually-exclusive assets, point it at a pruned copy. This path is for exploration; the
`dotnet test` gate above is the source of truth for whether a skill works.

## Relationship to other skills

- `umbraco-skill-evaluator` — grades whether Claude *produces* correct skill output (LLM
  grading). Run it for quality/regression scoring; run **this** skill to prove the output
  compiles and serves.
- `umbraco-chrome-navigation` (backoffice plugin) — drives the running backoffice for the
  browser half of validation.

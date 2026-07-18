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

## Validate a skill's code end-to-end

Skills ship loose `assets/*.cs` (controllers, composers, content finders, …) using a
`namespace <Namespace>;` placeholder — not a `.csproj`. This harness turns those assets into
a compilable unit, loads it into the running site, and lets you exercise the feature over
HTTP.

```bash
# 1. Materialize the skill's assets into a sidecar library and reference it from the instance
.claude/skills/umbraco-reference-instance/scripts/instance.sh try plugins/implementation/skills/umbraco-sitemap

# 2. Boot (rebuilds with the referenced library)
.claude/skills/umbraco-reference-instance/scripts/instance.sh boot

# 3. Exercise the feature over HTTP (example: the sitemap skill)
curl -sk https://localhost:44372/sitemap.xml            # expect a well-formed <urlset>
curl -sk -o /dev/null -w '%{http_code}\n' https://localhost:44372/this-page-does-not-exist

# 4. Tear down — remove the reference and delete the scratch project (leaves the repo clean)
.claude/skills/umbraco-reference-instance/scripts/instance.sh reset
```

What `try` does:

1. Creates `Umbraco.Skills.Sandbox/` — a Razor Class Library (`Microsoft.NET.Sdk.Razor`, so
   its controllers/views register as an application part) referencing `Umbraco.Cms.Web.Website`
   `17.*`.
2. Copies the skill's `assets/*.cs` in, substituting `<Namespace>` → `Umbraco.Skills.Sandbox`
   (so `namespace <Namespace>.Controllers;` becomes `Umbraco.Skills.Sandbox.Controllers`).
   It **fails loudly** if any literal `<Namespace>` remains.
3. Adds the sandbox as a `<ProjectReference>` on `Umbraco-CMS.Skills.csproj`. Umbraco's
   `IComposer` and ASP.NET controllers in the referenced assembly are then discovered
   automatically on boot.

`reset` removes the reference and deletes `Umbraco.Skills.Sandbox/`, restoring the committed
instance exactly.

> **Mutually-exclusive assets.** `try` copies *every* `assets/*.cs`. Some skills ship
> alternatives that must not both be registered — e.g. the sitemap skill's
> `SitemapController.cs` and `SitemapIndexController.cs` both map `GET /sitemap.xml`, which is
> an ambiguous route at runtime. Copy only the chosen approach into a pruned folder and point
> `try` at that (as the skill's own guidance dictates — Approach A is the three files
> `SitemapController` + `SitemapComposer` + `SitemapCacheInvalidator`).
>
> **Backoffice steps.** Some skills also need backoffice work (Approach B of the sitemap skill
> needs a Document Type + content node). Do those via the `umbraco-chrome-navigation` skill
> before the HTTP checks — the `try` harness only wires up the C# assets.

## Deferred: packaging mechanism (NuGet vs ProjectReference)

The default is a `ProjectReference` to the sidecar library because it is reliable and needs
no per-skill `.csproj`. The sidecar is deliberately **pack-ready**: to test the real NuGet
distribution path instead, `dotnet pack Umbraco.Skills.Sandbox` into `.local-nuget-feed/`,
then `dotnet add Umbraco-CMS.Skills reference` → `dotnet add package` from that feed. Whether
the shipped artifact is a NuGet package (harness-materialized vs skill-authored `.csproj`) or
stays a `ProjectReference`, and whether skills are validated one-at-a-time or all together, is
not yet decided — the sidecar keeps every option a one-line change.

## Relationship to other skills

- `umbraco-skill-evaluator` — grades whether Claude *produces* correct skill output (LLM
  grading). Run it for quality/regression scoring; run **this** skill to prove the output
  compiles and serves.
- `umbraco-chrome-navigation` (backoffice plugin) — drives the running backoffice for the
  browser half of validation.

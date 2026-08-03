# Runtime validation (the `dotnet test` gate)

`evals/evals.json` grades whether Claude *writes* the right code. This gate proves the code
*compiles and serves* — no LLM, no grader, reproducible pass/fail, run on every PR. Both matter and
neither substitutes for the other: an eval can score full marks on code that doesn't build, and a
green gate says nothing about whether Claude picks the right approach.

To run it: `dotnet test Umbraco-CMS.Skills.sln`. For booting the site by hand, inspecting its
content, and debugging failures, see the `umbraco-reference-instance` skill — this file covers only
what *you* produce as an author.

## What actually gets checked

Adding a skill to the gate buys four distinct things. Know which you're getting, because it's easy
to assume more coverage than exists:

1. **It compiles.** The example project is built as part of the solution, so an API that no longer
   exists fails the build. This is the highest-value, lowest-effort layer — it's what caught
   `umbraco-custom-error-pages` shipping pre-v14 APIs (`FirstChild(alias)`, `GetAtRoot()`) that
   could not compile on Umbraco 17 at all.
2. **It serves.** An NUnit fixture boots the reference instance in-process and asserts real HTTP
   responses — status, content type, rendered body.
3. **It hasn't drifted.** `scripts/generate-examples.sh --check` fails if the committed example
   stopped matching your `assets/`.
4. **The content it needs exists.** Declared in the manifest (below) and asserted centrally.

Note the limit: **only committed `assets/` can be gated.** A reference that says "fetch the tutorial
and copy its code" produces nothing to compile or assert, so that approach is invisible here — the
gate can only ever cover what the skill ships.

## What you add

```
plugins/<plugin>/skills/<skill>/
├── assets/                                  # the source of truth (already yours)
└── examples/<approach>/                     # one folder PER APPROACH, not per skill
    ├── .generate.json                       # the recipe (see below)
    ├── <Skill>.Example.csproj               # Microsoft.NET.Sdk.Razor
    ├── <Generated>.cs                       # written by the generator — don't hand-edit
    ├── <Skill>Tests.cs                      # your fixture, beside the code it tests
    └── ExampleHostWiring.cs                 # only if the skill needs Program.cs/config changes
```

Then one `<ProjectReference>` in `Umbraco-CMS.Skills/Umbraco-CMS.Skills.csproj`.

Three things about this layout are load-bearing:

- **One folder per approach.** Two approaches often can't coexist in one host — the sitemap skill's
  two controllers both map `GET /sitemap.xml`, and only one `IContentLastChanceFinder` can be
  registered at all (it's a setter). Separate projects let each be compiled even when only one can
  be loaded.
- **Tests live here, but compile elsewhere.** `Umbraco-CMS.Skills.TestHost` globs
  `**/examples/*/*Tests.cs` into a single assembly, because Umbraco keeps process-wide static state
  and every fixture must share one host. A test project per skill would boot an Umbraco each.
- **Host wiring ships inside the example.** If your skill tells users to edit `Program.cs` or
  `appsettings.json`, do the equivalent from an `IComposer`/`IUmbracoPipelineFilter` in the example
  rather than editing the shared instance. See `umbraco-custom-error-pages`, which applies
  `UseExceptionHandler` and a `ReservedPaths` entry that way.

## How `.generate.json` works

The generator (`scripts/generate-examples.sh`) copies your `assets/*.cs` into the example,
substituting placeholders. `assets/` stays the single source of truth; the committed example is a
mechanical projection of it, which is what lets the gate prove that *the code users are told to copy*
is the code that was tested.

```json
{
  "_comment": "Free-text note. JSON has no comments; nothing reads this key.",
  "namespace": "Umbraco.Skills.Examples.<Skill>",
  "placeholders": { "<ErrorPageAlias>": "error" },
  "assets": ["PageNotFoundContentFinder.cs", "ErrorController.cs"],
  "requires": { "documentTypeAliasAtRoot": ["<ErrorPageAlias>"] }
}
```

- **`namespace`** — what `<Namespace>` becomes. Required.
- **`assets`** — which of the skill's asset files this example compiles. Required. List only ONE
  approach's files; mutually exclusive assets in one project give you two endpoints on one route.
- **`placeholders`** — every *other* `<Token>` your assets carry, mapped to a value that exists in
  the instance. **Miss one and it silently survives into the generated code** as a literal string:
  it still compiles, so nothing complains, and the code can simply never find what it's looking for.
  Check your assets for tokens before assuming `<Namespace>` is the only one.
- **`requires`** — content the example needs in order to prove anything, asserted by
  `ReferenceContentPreconditionsTests`. A `<Token>` entry is resolved through your own
  `placeholders` map, so an alias is declared once rather than repeated into a test. Omit the key
  entirely if the skill needs no particular node.

Regenerate with `scripts/generate-examples.sh`, verify with `--check`. Skills whose `assets/` aren't
on the current branch are skipped, so it's safe to run before your skill PR merges.

Two behaviours worth knowing: an asset listed in `assets` but missing from disk is an error, while
an unknown top-level key (a typo'd `requires`, say) is silently ignored — so if a precondition
never seems to run, check the spelling first.

## Writing the fixture

Model it on `SitemapTests.cs` or `CustomErrorPagesTests.cs`. The essentials:

- Use the shared host: `ReferenceSiteFixture.Client`. **Never** `new ReferenceSiteFactory()` — a
  second host in the same process leaves whichever fixture runs later resolving services from a
  disposed provider, and the symptom is a fixture that passes alone and fails in a full run.
- Assert what the skill promises, over HTTP: status code, content type, and a marker proving the
  right *content* rendered — not merely that something responded.
- If you mutate content, restore it in a `finally`. Other fixtures share the instance.
- If you mutate content and then assert on output, wait for the source the code under test actually
  reads before asserting. Umbraco's published cache updates asynchronously, and reading too early
  can rebuild stale output *and cache it*. `SitemapCacheInvalidationTests` documents this trap in
  detail; the short version is that a key lookup or the Delivery API are not stand-ins for the
  traversal your code performs.

**Then prove the test can fail.** Break the thing it checks — point a `requires` alias at a
nonexistent Document Type, or change a cache key — confirm it goes red, and revert. A test that
passes either way is worse than no test, because it reports coverage you don't have.

When you break something to check this, restore it with `touch` on the file afterwards, or verify
the rebuilt binary: `mv file.bak file` restores an older mtime, MSBuild then skips recompiling, and
the broken build silently persists into later runs. That cost hours once.

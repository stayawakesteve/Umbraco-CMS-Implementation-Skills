# Running these evals

Run the four evals in `evals.json` together, with and without the skill, using the
`umbraco-skill-evaluator` skill.

## Project fixture (every eval, both sides)

Each run gets its own sandbox:

```
<run>/outputs/
  Directory.Packages.props     # copy of the repo-root file, so the project restores and builds
  project/                     # the "user's project": the agent edits here, in place
```

To build `project/`, copy `files/project/` (a minimal Umbraco 17 site, root namespace `Acme.Web`,
ModelsBuilder `Nothing`, a master layout that reads `siteName` with an ancestors fallback), then
overlay the eval's own folder:

| Eval | Overlay | Starting state |
|---|---|---|
| 1 | — | The project as copied |
| 2 | — | The project as copied |
| 3 | `files/locations/` | `IBranchDirectory` with an in-memory implementation and its composer |
| 4 | `files/two-sites/` | `appsettings.json` with the misnamed `WebRouting:DisableFindContentByIdPath` |

Every assembled project builds with `dotnet build -p:RazorCompileOnBuild=true`. No Umbraco instance
or MCP is running, so backoffice steps are graded on the instructions given.

## Run prompt

Each run is told where the project is and to edit it in place, that it may run `dotnet build` and
read the public Umbraco docs, and to save its reply to `outputs/final-answer.md` and a step log to
`transcript.md`.

Give the with-skill run a **snapshot** of the skill containing only `SKILL.md`, `references/` and
`assets/`: a run that can reach `evals/` or `examples/` will read them. Tell the baseline run not to
read `plugins/`, `.claude/` or any Umbraco source checkout.

## Grading notes

Grade code expectations from the files under `outputs/project/`, not from `final-answer.md` alone. A
build claim is checked by building the run's project.

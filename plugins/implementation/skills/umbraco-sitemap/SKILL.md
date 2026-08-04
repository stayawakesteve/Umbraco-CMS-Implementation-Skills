---
name: umbraco-sitemap
description: >
  Add an XML sitemap to an Umbraco 17+ site. Offers two approaches: the official docs
  Razor-template + Document Type approach, and a custom cached API controller that needs
  no backoffice setup. Use this skill whenever the user asks to add, create, build,
  implement, or enable an XML sitemap for an Umbraco site, including related goals like
  "improve SEO", "let search engines index the site", or "set up robots.txt".
  SKIP: non-Umbraco projects or Umbraco < 17.
---

# Sitemap

Two supported ways to add an XML sitemap:

| | **A — Cached controller** (default) | **B — Razor template** |
|---|---|---|
| Reference | [approach-a-cached-controller.md](references/approach-a-cached-controller.md) | [approach-b-razor-template.md](references/approach-b-razor-template.md) |
| Source | Custom code from documented building blocks | Verified template + package manifest in `assets/`, following the official [tutorial](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/tutorials/creating-an-xml-site-map.md) |
| Backoffice work | None — file-based only | Required: Document Types, composition, content node |
| Sitemap URL | Fixed `/sitemap.xml` | The XmlSiteMap content node (e.g. `/xml-sitemap`) |
| Caching | In-memory, auto-invalidated | None — rendered per request |
| Best for | Headless/Delivery-API sites, no backoffice access | Traditional Razor sites, per-page editor control |

### How to decide

Default to A. Choose B when the team wants the official-docs approach or per-page editor
control (priority / change frequency / hide). B's Document Types, composition and template ship as
`assets/sitemap-package.xml` + `assets/xmlSitemap.cshtml`, so it can be installed as a package or
built by hand from that spec; prefer the
[Umbraco Developer MCP](https://docs.umbraco.com/umbraco-in-ai/mcp/cms-developer-mcp) for the
backoffice steps, and if unavailable walk the user through them manually — don't drop to A just
because the MCP is missing. Only fall back to A if backoffice access isn't possible at all.

If ambiguous, briefly offer both and recommend A.

## Version compatibility

Both target **Umbraco 17+**. Approach A uses `IDocumentNavigationQueryService` (Umbraco 15+).

## Validation

Assertions for both approaches live in [`evals/evals.json`](evals/evals.json); run them with the `umbraco-skill-evaluator` skill.

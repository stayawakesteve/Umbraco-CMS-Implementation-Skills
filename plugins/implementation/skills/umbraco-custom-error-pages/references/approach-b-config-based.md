# Approach B — Config-based 404 (no custom code)

The **"Recommended"** method in the official tutorial,
[Implement Custom Error Pages](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/tutorials/custom-error-page)
([Markdown version](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/tutorials/custom-error-page.md)).
Read that for the reasoning; this file is the verified version of it.

There is no custom C# at all: Umbraco's built-in `ContentFinderByConfigured404` resolves the page from a
GUID in `appsettings.json`. The skill ships the two artefacts you need, both covered by the runtime gate
(installed, published and rendered on the starter-kit-free reference host):

| File | What it is |
|---|---|
| [`assets/error-pages-package.xml`](../assets/error-pages-package.xml) | The `errorPage404` Document Type and its template record. |
| [`assets/errorPage404.cshtml`](../assets/errorPage404.cshtml) | The template. Copy verbatim, then restyle. |

Requires **Umbraco 16.1+** (16.0 has a regression this method trips over — the tutorial calls it out).

## When to choose this over Approach A

- The team wants **zero custom C#**.
- Single-site, or multi-**lingual** — it supports per-culture 404 pages. It does **not** do per-domain
  multi-site resolution; for that use [Approach A](approach-a-content-finder.md).

**Never implement both.** Approach A's `SetContentLastChanceFinder` is
`AddUnique<IContentLastChanceFinder, T>()`, and the registration it replaces is
`ContentFinderByConfigured404` — this approach's entire implementation. Add A and B stops working, with
no error to tell you.

## Steps

1. **Install the schema.** Import `error-pages-package.xml` from the backoffice (Packages), or build the
   same shape by hand — prefer the
   [Umbraco Developer MCP](https://docs.umbraco.com/umbraco-in-ai/mcp/cms-developer-mcp); if it's
   unavailable, walk the user through the backoffice UI. Don't drop to Approach A just because the MCP is
   missing.
2. **Copy `assets/errorPage404.cshtml`** to `Views/errorPage404.cshtml` (match the template alias).
3. **Create and publish a node** of type *Error Page 404*. Anywhere in the tree — config finds it by
   GUID, not by position. Set its *Page title*, or the template falls back to the node name.
4. **Copy the node's Id (GUID)** from its Info tab.
5. **Add the config block** to `appsettings.json`:

   ```json
   {
     "Umbraco": {
       "CMS": {
         "Content": {
           "Error404Collection": [
             { "Culture": "default", "ContentKey": "PASTE-THE-GUID-HERE" }
           ]
         }
       }
     }
   }
   ```

   `"default"` is the documented fallback culture, used when no domain matches. For a multilingual site
   add one entry per culture (`"en-US"`, `"da"`, …) alongside it.

## Notes

- **Set exactly one of `ContentKey` or `ContentId`.** `ContentErrorPage.IsValid()` enforces
  `HasContentId ^ HasContentKey`, so supplying both is a configuration error. Prefer `ContentKey`: node
  ids differ between environments even more readily than GUIDs do.
- **The GUID is per-environment content, not schema.** This is the approach's one real awkwardness: each
  environment either shares content (restored databases) or needs its own entry. Say so when
  recommending it.
- **The template must set the status code itself** — `Context.Response.StatusCode = 404;`. A
  content-rendered error page otherwise returns 200 with error markup, which looks correct to a human
  and is wrong for every crawler.
- **Keep the template self-contained.** A 404 is served for a URL that matched nothing, so a layout or
  partial that assumes a valid route (reading ancestors, building breadcrumbs) can throw and turn a tidy
  404 into a 500. The shipped template uses `Layout = null` for that reason.
- **Test the body, not just the status.** Umbraco answers 404 for an unmatched URL whether or not your
  configured page was found, so a status-code-only check passes even when the GUID is wrong. Assert on
  content the page actually renders.

## 500 errors

This approach covers 404s only. There is no config-based option for 500s — use the shared
[500 controller](500-error-controller.md) regardless of which 404 approach was chosen.

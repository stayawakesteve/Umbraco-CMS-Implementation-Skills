# Approach B — Razor template + Document Type

The sitemap is a **content node**: a Document Type with a template, rendered at its own URL. Follows
the shape of the official tutorial,
[Creating an XML Sitemap](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/tutorials/creating-an-xml-site-map.md)
— read that for the reasoning and screenshots.

Unlike the tutorial, **this skill ships the artefacts**, verified against a real Umbraco:

| File | What it is |
|---|---|
| [`assets/xmlSitemap.cshtml`](../assets/xmlSitemap.cshtml) | The template. Copy verbatim. |
| [`assets/sitemap-package.xml`](../assets/sitemap-package.xml) | `xmlSiteMap` Document Type, its template record, and the `xmlSiteMapSettings` composition. |

Both are covered by the runtime gate — installed and rendered on the starter-kit-free reference host,
with the filtering asserted — so they are known to work on the targeted Umbraco rather than assumed to.

Don't choose this approach for a headless / Delivery-API-only site — use
[Approach A](approach-a-cached-controller.md) instead.

Choose it when the team wants the content-node approach and/or per-page editor control
(priority, change frequency, hide) via Document Types.

## Installing the schema

`sitemap-package.xml` is an Umbraco package manifest, so there are two routes:

1. **Import it** from the backoffice (Packages), which creates everything in one step.
2. **Treat it as the spec** and build the same shape by hand — prefer the
   [Umbraco Developer MCP](https://docs.umbraco.com/umbraco-in-ai/mcp/cms-developer-mcp); if it's
   unavailable, walk the user through the backoffice UI one step at a time. Don't drop to Approach A
   just because the MCP is missing.

Only fall back to [Approach A](approach-a-cached-controller.md) if backoffice access isn't possible
at all.

## Steps

1. **Install the schema** (above). It gives you the `xmlSiteMap` Document Type — allowed at root, with
   an `excludedDocumentTypes` TextString — and the `xmlSiteMapSettings` composition with
   `hideFromXmlSiteMap`, `xmlSiteMapPriority` and `xmlSiteMapChangeFrequency`.
2. **Apply the `xmlSiteMapSettings` composition** to every content Document Type that should appear in
   the sitemap. The package can't do this for you — it doesn't know your types. This is the step people
   forget, and forgetting it doesn't break anything visibly: pages just can't be individually hidden.
3. **Copy `assets/xmlSitemap.cshtml`** to `Views/xmlSiteMap.cshtml` (match the template alias the
   Document Type points at).
4. **Create and publish an XML Sitemap content node** under the site root. The sitemap lives at *that
   node's* URL — Umbraco derives the segment from the node's NAME, so "XML Sitemap" gives
   `/xml-sitemap/`.
5. **Set `excludedDocumentTypes`** on that node if whole types should be omitted — comma-separated
   aliases. Include the sitemap's own type so it doesn't list itself.

## Notes

- The sitemap URL is the **content node's URL**, not a fixed `/sitemap.xml` route. Reference it in
  `wwwroot/robots.txt`: `Sitemap: https://www.yoursite.com/xml-sitemap`.
- **No caching** — rendered per request, which is the point: editor changes show immediately.
  Approach A caches and has no per-page control.
- Priority and change frequency use the built-in Textstring editor so the package depends only on data
  types every Umbraco already has. Swapping in a slider and a dropdown is a cosmetic change to the
  editing experience and doesn't affect the rendered sitemap.
- The loop variable in the template is `node`, **not** `page`. `@page` is a reserved Razor directive,
  so `@page.Url(...)` fails to compile — and because templates are compiled at runtime, that surfaces
  only as a 500 with no diagnostics.
- Apply the same sitemaps.org / Google rules the tutorial follows: absolute `<loc>` URLs,
  `application/xml; charset=utf-8`, and stay under the **50,000 URL / 50 MB per-file limit** (split
  into a `<sitemapindex>` beyond that). Google ignores `<priority>`/`<changefreq>`, so those are
  editor conveniences rather than required output — the template omits them when blank, because an
  empty `<priority>` is invalid.

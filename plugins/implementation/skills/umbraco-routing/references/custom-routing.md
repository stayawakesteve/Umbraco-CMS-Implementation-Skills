# Approach B — Custom routing code

C# that changes how Umbraco builds URLs (outbound) or matches them (inbound). Choose this only when
[built-in routing](built-in-routing.md) can't express the rule: a URL computed from a property, or a
URL pattern with no page behind it.

## Building blocks (fetch the docs before implementing)

- **Outbound pipeline, `IUrlSegmentProvider`, `IUrlProvider`:** [Outbound Request Pipeline](https://docs.umbraco.com/umbraco-cms/17.latest/develop-with-umbraco/application-code/backend-and-custom-logic/routing/request-pipeline/outbound-pipeline.md)
- **`IContentFinder`:** [IContentFinder](https://docs.umbraco.com/umbraco-cms/17.latest/develop-with-umbraco/application-code/backend-and-custom-logic/routing/request-pipeline/icontentfinder.md)
- **Inbound pipeline, `RoutingRequestNotification`:** [Published Content Request Preparation](https://docs.umbraco.com/umbraco-cms/17.latest/develop-with-umbraco/application-code/backend-and-custom-logic/routing/request-pipeline/published-content-request-preparation.md)
- **Virtual-page routes:** [Custom MVC Routes](https://docs.umbraco.com/umbraco-cms/17.latest/develop-with-umbraco/application-code/backend-and-custom-logic/routing/custom-routes.md)

## Docs corrections (Umbraco 17)

| Docs snippet | Problem | Use instead |
|---|---|---|
| `content.GetById(1234)` / `GetById(2074)` in finders and `FindContent` | Ids differ per environment | Find by Document Type alias (`IDocumentNavigationQueryService.TryGetDescendantsKeysOfType`) or by key |
| Segment provider implementing only `GetUrlSegment(content, culture)` | Umbraco calls the `published` overload; without it the flag is dropped and the provider can't tell the draft from the live value | Implement `GetUrlSegment(content, published, culture)` too, as the asset does |
| Custom route model `Product : ContentModel` "must implement `IPublishedContent`" | `ContentModel` doesn't implement it; a layout typed `UmbracoViewPage` can't bind it | Derive from `PublishedContentWrapped` |

## The files

Verified templates in [`../assets/`](../assets), all registered by `RoutingComposer.cs` or by attribute:

- `SkuUrlSegmentProvider.cs`: a product's segment becomes `{name}-{sku}`.
- `SkuShortLinkContentFinder.cs`: `/p/{sku}` answers 301 to that product.
- `RoutingComposer.cs`: registers both.
- `StoreFinderController.cs` + `storeFinderCity.cshtml`: `/stores/{city}`, a virtual page.

Each is independent; copy only the ones the task needs. Replace `<Namespace>` and the placeholders
(`<ProductDocTypeAlias>`, `<SkuPropertyAlias>`, `<ShortLinkPrefix>`, `<VirtualRoutePrefix>`,
`<LandingDocTypeAlias>`) with the site's real aliases, and rename the classes to fit.

## Steps

1. **Discover:** root namespace, the Document Type and property aliases involved, folder conventions
   (`Routing/`, `Controllers/`, `Composers/`), and whether the site is multi-site (domains on roots).
2. **Pick the block** from the sections below and write it from the asset.
3. **Register it.** Providers and finders in a composer; virtual-page controllers by `[Route]`.
4. **Republish affected pages** when you changed segments: see *URL segment provider*.
5. **Build and request** the URLs, both the new ones and the old ones (see *Verify*).

### URL segment provider (`IUrlSegmentProvider`)

Changes one node's segment; the rest of the path, domains and redirects keep working. Umbraco calls
providers in order until one returns a non-null segment, then falls back to its own.
- **Register with `Insert<T>()`** so it runs before the default provider. Return `null` for content it
  doesn't handle.
- **The segment is stored at publish.** Existing pages keep their old URL until republished:
  use **Publish with descendants** on their parent. Umbraco then records a 301 from each old URL. Tell the user to do this; it is easy to miss.
- **No `/`** in a segment; `ToUrlSegment()` strips it. To add a level to the path, it has to be a
  real node.
- Override `HasUrlSegmentChanged` only if the segment comes from outside the content (an API, a
  database), so redirect tracking can tell when it changed.

### Content finder (`IContentFinder`)

Maps a URL that matched no page to a page, a redirect, or nothing. Umbraco runs finders in order and
stops at the first that returns `true`. Built-in order on 17: `?umbPageId=`, URL, `/{guid}`,
`umbracoUrlAlias`, tracked redirects.
- **`Append<T>()`** unless your URL also matches a real page and must win; then
  `InsertBefore<ContentFinderByUrlNew, T>()`, and the finder runs on every page request.
- **Check the prefix first** and return `false` fast. Cache or index any lookup that isn't cheap.
- **Redirect or render.** `request.SetRedirectPermanent(url)` keeps one canonical URL;
  `request.SetPublishedContent(node)` renders the node at this URL (duplicate content, so only when
  this *is* the canonical URL, e.g. it pairs with an `IUrlProvider`).
- **Multi-site:** `request.Domain` is set before finders run; search only under its root.
- **404 pages are not finders:** use `umbraco-custom-error-pages` (`IContentLastChanceFinder`).
- **Not for the Content Delivery API:** headless paths are resolved by `IApiContentPathResolver`.

### Custom URL provider (`IUrlProvider`)

Only when the URL can't be expressed as a segment change (e.g. products served from a different
section's path). Derive from `NewDefaultUrlProvider` and override `GetUrl`, register with
`builder.UrlProviders().Insert<T>()`, and **always pair it with a content finder** that routes every
URL it produces. Without one the pages are unroutable, as the docs warn.

### Virtual-page route (`IVirtualPageController`)

A URL pattern rendered by your own controller with an Umbraco node as `CurrentPage`, so the layout,
navigation and node properties work. Use it when the data isn't in Umbraco (an API, a database). If
the node already exists at that URL, hijack its route instead (`umbraco-templating`).
- **Inherit `UmbracoPageController` and implement `IVirtualPageController`.** It cannot inherit
  `RenderController`.
- **`[Route("stores/{city}")]` is the registration.** Umbraco maps attribute routes; nothing else
  is needed. Without attribute routing, map a conventional route with `.ForUmbracoPage(findContent)`
  in an `UmbracoPipelineFilter` (see the docs), which doesn't support `[Route]`.
- **`FindContent` returning `null` ends the request with a bare 404** before the action runs. Find
  the node by Document Type, not id.
- **The view model wraps the node** (`PublishedContentWrapped`), so the master layout still binds.
- **Views:** `View("name", model)` resolves `Views/name.cshtml`.
- A route that only returns JSON or XML doesn't need any of this: use a plain controller.

### File extensions

Umbraco treats any path with an extension (`/feed.xml`, `/stores/london.json`) as a static-file
request and creates no Umbraco context for it. A virtual page there fails with a 500 ("Wasn't able
to get an UmbracoContext"). Opt the path back in, in `Program.cs` or a composer:

```csharp
builder.Services.Configure<UmbracoRequestOptions>(options =>
    options.HandleAsServerSideRequest = request => request.Path.StartsWithSegments("/feed.xml"));
```

### Changing a request after routing (`RoutingRequestNotification`)

A notification handler receives the `IPublishedRequestBuilder` after content and template are found
and before rendering, to swap the template, set a redirect, or change the culture. Prefer a finder
or a property when either can do it: this runs on every routed request.

## Verify

Build, then request each URL: the new URL answers 200 with the right node, the old URL 404 or 301
as intended, unknown values 404, and `Url()` on the node returns the new URL. If the site can't be
run here, say so instead of claiming it works.

## Done

Tell the user what was registered and where, which pages must be republished, the URLs to test, and
any `HandleAsServerSideRequest` or config change made.

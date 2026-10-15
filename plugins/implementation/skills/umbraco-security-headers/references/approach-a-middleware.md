# Approach A — Custom middleware

Three files: a middleware that sends the headers, a settings class, and a composer that registers
both and puts the middleware at the start of Umbraco's pipeline. No package dependency, and no
change to `Program.cs`.

## When to choose this approach

- The site has no header library yet, or you are replacing scattered `app.Use` header code.
- You want the policy in source control as one reviewable file, with a report-only switch in
  configuration.
- You are building a package, which can't ask users to edit `Program.cs`.

If the project already sends headers with NWebsec, use [Approach B](approach-b-nwebsec.md).

## Building blocks (fetch before changing the design)

- **The CSP and the backoffice's sources:**
  [Content Security Policy (CSP)](https://docs.umbraco.com/umbraco-cms/run-in-production/infrastructure-and-ops/health-check/guides/contentsecuritypolicy.md),
  section *Adding a CSP using custom middleware*.
- **Pipeline filters** (`UmbracoPipelineFilter`, `PrePipeline`):
  [Custom Middleware](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/application-code/backend-and-custom-logic/routing/custom-middleware.md)
- **Composers:**
  [Composing](https://docs.umbraco.com/umbraco-cms/model-your-content/content-types-and-structure/composing.md)

## What the code does

Read the templates in [`../assets/`](../assets):

- `SecurityHeadersMiddleware.cs` sends the backoffice policy under `/umbraco` and the site policy
  everywhere else. It also sends `nosniff`, `SAMEORIGIN`, `Referrer-Policy` and
  `Permissions-Policy`, and removes `X-XSS-Protection`. It sets the headers before the request
  runs, so the output cache stores them with the page. It sets them again as the response starts,
  because an exception handler clears them before rendering the error page. It never replaces a
  CSP that is already on the response.
- `SecurityHeadersSettings.cs` holds `ReportOnly`, bound from the `SecurityHeaders` section.
- `SecurityHeadersComposer.cs` registers both and adds the middleware in `PrePipeline`. That runs
  before static files and media, which end the pipeline early. Anything registered later never sees
  those responses.

## Steps

1. **Discover the project.** Find the root namespace, the `Umbraco.Cms` version (17.3+), the
   folder conventions, and every existing source of these headers: `app.Use` blocks, NWebsec,
   `web.config` `customHeaders`, and the CDN or proxy. Remove those, or keep them out of this
   middleware, so each header has one source.
2. **List what the front end loads.** Open the layout and partials, and note every external
   script, stylesheet, font, image, iframe and `fetch` origin, plus every inline `<script>` and
   `<style>` and `style="…"` attribute (including ones in rich text).
3. **Write the three files,** replacing `<Namespace>`. Put them where the project keeps composers,
   or at the project root.
4. **Edit `SitePolicy`** with the origins from step 2. For inline tags:
   - add `asp-csp-nonce` to each inline `<script>`/`<style>`, and add
     `@addTagHelper *, Umbraco.Web.Common` to `Views/_ViewImports.cshtml`;
   - on output-cached pages, prefer a `'sha256-…'` hash or moving the script to a file;
   - inline `style="…"` attributes can't take a nonce. Move them to CSS, or add `'unsafe-inline'`
     to `style-src` only.

   Leave `BackOfficePolicy` as shipped unless a backoffice package needs another source.
5. **Roll out report-only.** Add `"SecurityHeaders": { "ReportOnly": true }` to `appsettings.json`
   (or the environment's settings), deploy, and browse the site and backoffice with the console
   open. Fix each violation, then set `ReportOnly` to `false`.
6. **Build and verify.** If a `.csproj` exists, run `dotnet build`, then run the checks in the
   skill's *Verify* section. If there is no project to build or run, say so. Don't describe the
   headers as working.

## Done

Tell the user:

- which files were added, and that `SitePolicy` is the place to add origins later;
- that `ReportOnly` switches between report-only and enforcing without a code change;
- which header sources were removed, and anything the CDN or proxy still adds;
- that `Server` is removed in Kestrel or IIS settings, not in this middleware.

# Hardening checklist

Work through this list when auditing a site or preparing it for production. Report every item as
**done**, **not applicable** (with the reason), or **needs the user** (a decision or access you
don't have, such as IP ranges, IdP registration or hosting configuration). End with that status
list and the files changed.

## Fetch first

- [Lock down access to the Umbraco folder](https://docs.umbraco.com/umbraco-cms/run-in-production/security/lock-access-to-umbraco-folder.md):
  locking down `/umbraco` by IP on IIS.
- [Umbraco security settings](https://docs.umbraco.com/umbraco-cms/run-in-production/security/security-settings.md):
  pointers to password, routing and Forms settings (see *Docs corrections*).
- [Health Check](https://docs.umbraco.com/umbraco-cms/run-in-production/infrastructure-and-ops/health-check.md):
  run it first, and treat its results as the starting backlog.

## Checklist

1. **Runtime mode `Production`** (`Umbraco:CMS:Runtime:Mode`). Umbraco then refuses to boot
   unless the entry assembly is built in Release (JIT optimizer on), `ModelsBuilder:ModelsMode`
   is `Nothing`, `Global:UseHttps` is `true` and `WebRouting:UmbracoApplicationUrl` is set
   ([https.md](https.md)). Those four are the only boot checks. A failing one means the site fails
   at startup: the process stays up, but every request returns a 500 `BootFailedException`, and
   only the log lists the failing checks.

   Separately, and **not** a boot check: Production has no Razor runtime compilation and no
   in-memory models, so views and models must be compiled into the build. Generate models
   (`SourceCodeManual`) before switching to `Nothing`, and remove any
   `RazorCompileOnBuild`/`RazorCompileOnPublish` `false` settings. Otherwise the site boots but
   pages 404 with "No physical template file was found".
2. **HTTPS everywhere, HSTS on**: [https.md](https.md).
3. **Security headers and CSP, technology headers removed**: [security-headers.md](security-headers.md).
4. **Restrict `/umbraco` by IP or VPN** where editors work from known networks. The docs' IIS
   rule already exempts `/umbraco/api`, `/umbraco/surface` and `/umbraco/delivery` (the Delivery
   API and the member OIDC endpoints). Add any package paths under `/umbraco` that must stay
   public, plus `/umbraco/basic-auth` if basic authentication fronts the site. Keep
   `/umbraco/management/api` **inside** the restriction; if an off-network integration needs it,
   allow that caller's IP rather than exempting the path. The docs' load-balancer variant matches
   `X-Forwarded-For`, which a client can forge unless the load balancer overwrites it, so use it
   only when the origin is reachable through the load balancer alone. On nginx, Front Door or an
   App Gateway WAF, apply the same rules there. With no host-level option, there is no documented
   in-app pattern: a middleware before `app.UseUmbraco()` that returns 403 for `/umbraco` paths
   outside the exemptions, checked against `Connection.RemoteIpAddress` after
   `UseForwardedHeaders()` ([https.md](https.md)), does the job.
5. **Routing settings:** set `WebRouting:DisableAlternativeTemplates` and
   `WebRouting:DisableFindContentByIdentifierPath` to `true` unless the site relies on them (both
   default to `false`). The first stops visitors rendering pages with other templates; the second
   stops `/{guid}` lookups outside preview. Integer-ID lookup through `?umbPageID=<id>` comes from
   `ContentFinderByPageIdQuery`, which no setting turns off; remove it with
   `builder.ContentFinders().Remove<ContentFinderByPageIdQuery>()` if pages shouldn't be reachable
   that way. Public access still applies to pages found either way.
6. **Accounts:** user password and lockout rules set explicitly, 2FA or SSO for administrators,
   no shared logins, and API users for integrations
   ([authentication-users.md](authentication-users.md)). Remove or disable the install-time
   admin account if it isn't a named person.
7. **Secrets out of source control:** connection strings, `DeliveryApi:ApiKey`,
   `Imaging:HMACSecretKey`, `BasicAuth:SharedSecret`, OAuth and Delivery API client secrets, SMTP
   credentials, and `Unattended:UnattendedUserPassword` (unattended installs write it into
   `appsettings.json`) go in environment variables, user secrets or a vault
   ([Azure Key Vault](https://docs.umbraco.com/umbraco-cms/run-in-production/security/key-vault.md)).
8. **Rate limiting** for login, registration, password reset, form posts and public APIs:
   [API rate limiting](https://docs.umbraco.com/umbraco-cms/run-in-production/security/api-rate-limiting.md).
   It uses an `UmbracoPipelineFilter`'s `PostRouting` slot, so endpoint metadata is available.
   Behind a proxy, partition by the forwarded client IP, not the proxy's.
9. **Uploads and rich text:** register an `IFileStreamSecurityValidator`
   ([Server-side file validation](https://docs.umbraco.com/umbraco-cms/run-in-production/security/serverside-file-validation.md))
   if editors or members can upload. Files under `/media` are served from the site's own origin,
   and the default `Content:DisallowedUploadedFileExtensions` list doesn't block `svg`, `html`,
   `htm` or `js`, so an uploaded file with script in it is stored XSS. Umbraco does not sanitise
   SVG. Prefer `Content:AllowedUploadedFileExtensions` (an allow-list, which takes precedence) and
   leave `svg` out of it, or sanitise SVG in the validator, when untrusted users can upload.
   Consider an `IHtmlSanitizer`
   ([Sanitizing the RTE](https://docs.umbraco.com/umbraco-cms/run-in-production/security/serverside-sanitizing.md))
   when untrusted users can author rich text, and never render member-submitted HTML with
   `Html.Raw`.
10. **Members and personal data:** sensitive fields marked, member-only pages protected, media
    exposure checked ([authorization.md](authorization.md)).
11. **Cookies:** the session cookie is `UMB_SESSION`. Umbraco registers it HttpOnly. To force
    `Secure`, add your own `builder.Services.AddSession(...)` call; the last registration wins.
    Put it after the `CreateUmbracoBuilder()...Build();` chain and **before**
    `WebApplication app = builder.Build();` (see *Docs corrections*). List the backoffice cookies
    in the site's cookie policy.
12. **Staging and preview environments:** front-end
    [basic authentication](https://docs.umbraco.com/umbraco-cms/run-in-production/security/basic-authentication.md)
    or IP restriction, plus `noindex`. Never reuse the production Delivery API key.
13. **Image URL signing:** set `Imaging:HMACSecretKey`
    ([Imaging settings](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/configuration/imagingsettings.md))
    so only URLs Umbraco generated are resized. Without it, anyone can request arbitrary
    resize parameters and fill the image cache. The Health Check dashboard flags a missing key.
    Treat it as a secret (item 7).
14. **Load-balanced or containerised hosting:** every instance must share one persisted ASP.NET
    Core Data Protection key ring
    ([Load balancing](https://docs.umbraco.com/umbraco-cms/run-in-production/infrastructure-and-ops/server-setup/load-balancing.md)),
    or auth cookies, antiforgery tokens and member sessions fail across servers and after
    restarts.
15. **Swagger and package dashboards:** the Management API Swagger UI under `/umbraco/swagger` is
    turned off only when `ASPNETCORE_ENVIRONMENT` is `Production`, not by runtime mode, so a slot
    named `Staging` exposes it. Set the environment, or keep it behind item 4. Packages that add
    their own dashboards (for example Hangfire) need their own authorization.
16. **Stay patched:** check the installed version against
    [Umbraco security advisories](https://github.com/umbraco/Umbraco-CMS/security/advisories)
    and the [Trust Center](https://umbraco.com/trust-center/). Upgrade within the major
    version promptly.
17. **Regulated hosting:** follow the
    [FIPS setup](https://docs.umbraco.com/umbraco-cms/run-in-production/security/setup-umbraco-for-a-fips-server.md)
    only if the user says the server must be FIPS-compliant.

## Docs corrections

Where the docs pages and Umbraco 17 disagree, or read ambiguously, follow this list:

- **[Security settings](https://docs.umbraco.com/umbraco-cms/run-in-production/security/security-settings.md)**
  names `DisableFindContentByIdPath`. That property was removed; the setting is
  `WebRouting:DisableFindContentByIdentifierPath` (item 5).
- **[Cookies](https://docs.umbraco.com/umbraco-cms/run-in-production/security/cookies.md)** says to
  add the `AddSession` call "after `Build()`". It means after the
  `CreateUmbracoBuilder()...Build();` chain. Calling it after `WebApplication app =
  builder.Build();` throws, because the service collection is read-only by then (item 11).
- **[Lock down access to the Umbraco folder](https://docs.umbraco.com/umbraco-cms/run-in-production/security/lock-access-to-umbraco-folder.md)**
  is current: its rule already exempts `umbraco/api`, `umbraco/surface` and `umbraco/delivery`.
  Its load-balancer variant trusts `X-Forwarded-For`; see item 4 for when that's safe.

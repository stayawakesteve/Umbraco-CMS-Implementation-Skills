# Approach A — Backoffice users

Editors and administrators sign in to `/umbraco` with an external provider, and new users are
created on first sign-in in the Umbraco groups their provider roles map to. For website visitors,
use [members.md](members.md) instead.

## Fetch first

- [External login providers](https://docs.umbraco.com/umbraco-cms/run-in-production/security/external-login-providers.md):
  auto-link options, the `authProvider` manifest and its button options, custom login button
  elements.
- For a provider-specific handler instead of OpenID Connect:
  [Entra ID tutorial](https://docs.umbraco.com/umbraco-cms/run-in-production/tutorials/add-microsoft-entra-id-authentication.md)
  ("Users" section) or
  [Google tutorial](https://docs.umbraco.com/umbraco-cms/run-in-production/tutorials/add-google-authentication.md).

The docs' samples need correcting in three places, which the asset already does:

- The Entra tutorial's inline alternative passes `EntraIdBackOfficeAuthenticationOptions.SchemeName`.
  That class has no `SchemeName`, so the sample doesn't compile. The constant lives on the
  provider-options class.
- The generic "Transferring Claims" sample reads an undefined `externalLogin` variable. The
  callback's second parameter, `loginInfo`, holds the claims.
- `OAuthOptionsHelper.SetDefaultErrorEventHandling` only accepts `OAuthOptions` (Google,
  Microsoft Account, GitHub). For `OpenIdConnectOptions`, wire `OnRemoteFailure` and
  `OnAccessDenied` to `SetUmbracoRedirectWithFilteredParams`, as the asset does.

## Steps

1. **Discover.** Find the root namespace and the Umbraco version (17+). Find out how composers
   are organised and whether a provider is already registered (`AddBackOfficeExternalLogins`,
   `App_Plugins/*/umbraco-package.json`). Ask for the provider, the tenant or issuer, which
   provider groups or app roles map to which Umbraco groups, and whether password login must stay.
2. **Add the package:** `dotnet add package Microsoft.AspNetCore.Authentication.OpenIdConnect`.
3. **Write the code.** Copy [`../assets/BackOfficeExternalLogin.cs`](../assets/BackOfficeExternalLogin.cs)
   and replace the placeholders:

   | Placeholder | Example | Rule |
   |---|---|---|
   | `<Namespace>` | `MySite.Security` | The project's namespace |
   | `<SchemeName>` | `EntraId` | No `Umbraco.` prefix; never renamed later |
   | `<ProviderLabel>` | `Contoso Entra ID` | Button text |
   | `<CallbackPath>` | `/umbraco-entraid-signin` | Unique; not the members scheme's path |

4. **Write the button manifest.** Copy [`../assets/umbraco-package.json`](../assets/umbraco-package.json)
   to `App_Plugins/<AnyFolder>/umbraco-package.json` with the same placeholder values.
   `<Namespace>` only prefixes the extension's alias. Set `meta.linking.allowManualLinking` to
   match the code: `false` when `DenyLocalLogin` is on.
5. **Configure.** Add the section below. The secret goes in user secrets locally and in
   environment variables or a vault elsewhere: `ExternalLogins__BackOffice__ClientSecret`.

   ```json
   "ExternalLogins": {
     "BackOffice": {
       "Authority": "https://login.microsoftonline.com/<tenant-id>/v2.0",
       "ClientId": "<application-id>",
       "AllowedEmailDomains": [ "contoso.com" ],
       "GroupMappings": { "Umbraco.Editors": "editor", "Umbraco.Admins": "admin" }
     }
   }
   ```

   `GroupMappings` keys are the values of the provider claim named in `GroupClaimType`. That
   defaults to `ClaimTypes.Role`, which is where Entra ID app roles (`roles`) arrive. Values are
   Umbraco group **aliases** (`admin`, `editor`, `writer`, `translator`, `sensitiveData`), not
   display names.
6. **Reserve the callback path.** Add `~/<callback-path>/` to `Umbraco:CMS:Global:ReservedPaths`,
   keeping the existing entries.
7. **Register the redirect URI** at the provider as an absolute `https://` URL for each
   environment, ending in the callback path. For Entra ID, define app roles and assign them.
8. **Verify** (next section).

## Verify

- `GET /umbraco/management/api/v1/manifest/manifest/public` contains
  `"forProviderName":"Umbraco.<SchemeName>"`. The button is on the login screen.
- Clicking it redirects to the provider with `redirect_uri` ending in the callback path and
  `scope` containing `email`.
- Browsing the callback path directly returns to the backoffice login screen
  (`flow=external-login`), not an exception page.
- A first sign-in creates a user in the mapped groups only. Removing the role at the provider
  removes the group at the next sign-in.

If the site can't be run, say which of these were not checked.

## What trips people up

- **No button.** The login screen shows only providers named in a manifest with
  `"allowPublicAccess": true` (it loads before anyone is signed in), and `forProviderName` must be
  `Umbraco.` + the scheme name.
- **"No authentication handler is registered for the scheme 'Umbraco.X'".** The manifest names a
  scheme the C# never registered: a different spelling, or the composer or extension method isn't
  running.
- **Clicking the button throws "Provide Authority, MetadataAddress, Configuration, or
  ConfigurationManager".** An `IConfigureNamedOptions` class compared `name` with the bare scheme
  name, so it never ran. Named options arrive with the `Umbraco.` prefix.
- **"The Name value cannot be null".** Umbraco refuses to create a user without
  `Identity.Name`. OpenID Connect sends the display name as `name`, which .NET doesn't map, so set
  `TokenValidationParameters.NameClaimType = "name"`.
- **"has not provided the email claim".** Auto-linking reads `ClaimTypes.Email`. Request the
  `email` scope. In Entra ID, an account without a mailbox needs the `email` optional claim.
- **Users with no sections.** `defaultUserGroups` defaults to **none**; the docs' sample comment
  saying "Default: Editor" is wrong. It applies only when a user is created. An alias that
  doesn't exist is dropped silently, so `"Editors"` (the display name) gives no group. Sync
  groups in `OnExternalLogin` instead; changes made there are saved with the sign-in.
- **Everyone becomes an administrator.** A fixed `admin` in `defaultUserGroups` or `OnAutoLinking`
  makes every account in the tenant an administrator. Grant `admin` only through a mapped provider
  role.
- **Account takeover by email.** Auto-linking links to an *existing* user with a matching email,
  administrators included. A multi-tenant Entra ID app (`/common`, `/organizations`) accepts
  tenants whose admins can set any email on their accounts. Use a single-tenant authority and
  invite outside staff as guests, or allow-list tenant IDs (`tid`) in `OnExternalLogin`. Restrict
  email domains and refuse `email_verified: false`. Entra ID doesn't send `email_verified`, so
  there the tenant pin is what protects you. For Google Workspace, also check the `hd` claim.
- **Locked out after `DenyLocalLogin`.** It removes password login for everyone and disables
  password changes. Before turning it on, confirm a provider administrator can sign in, and keep
  the documented [admin password reset](https://docs.umbraco.com/umbraco-cms/run-in-production/security/reset-admin-password.md)
  as the way back in.
- **Renaming the scheme unlinks everyone.** On boot, Umbraco compares the registered backoffice
  providers with the last set it saw. It purges stored logins for any provider that has gone,
  and a renamed scheme counts as gone. With auto-linking, users relink by email; without it,
  they're locked out.
- **Umbraco's 2FA is skipped.** `Umbraco:CMS:Security:UserBypassTwoFactorForExternalLogins`
  defaults to `true`, so enforce MFA at the provider, for example with Entra ID Conditional
  Access.
- **Redirect URI mismatch** (`AADSTS50011` and similar). Behind a TLS-terminating proxy, the
  handler builds an `http://` redirect URI unless forwarded headers are configured. Locally, the
  port must match too.
- **Basic authentication** on the site needs `Umbraco:CMS:BasicAuth:RedirectToLoginPage: true`,
  because the browser's native prompt can't show provider buttons.
- **Large Entra ID query strings** can 404 on IIS. See "Common issues" in the docs page.
- **Tokens in the cookie.** Don't set `SaveTokens` or copy large claims into the backoffice
  identity; it lives in the auth cookie. Persist tokens with `IExternalLoginWithKeyService` (docs).

## Swapping the handler

To use `AddMicrosoftAccount`, `AddGoogle` or another OAuth handler, replace `AddOpenIdConnect` in
the composer and `IConfigureNamedOptions<OpenIdConnectOptions>` with that handler's options type.
Then call `oAuthOptionsHelper.SetDefaultErrorEventHandling(options, SchemeName)` instead of the two
events. Keep `BackOfficeSsoLoginProviderOptions` and `BackOfficeSsoRules` as they are. Check that
the handler maps an email and a name claim; Microsoft Account and Google both do.

## Done

Tell the user where the code, manifest and configuration went, and which redirect URI to register
per environment. Say which groups each provider role grants and whether password login remains.
List the checks from **Verify** that were and weren't made.

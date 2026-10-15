# Authentication: members

How website visitors register and sign in, on Razor sites and headless front ends. For
backoffice editors, see [authentication-users.md](authentication-users.md).

## Fetch first (pick the page for the task)

| Task | Docs page |
|---|---|
| Registration, login, logout, login status in Razor | [Member registration and login](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/tutorials/members-registration-and-login.md) |
| Members, member types, member groups | [Members](https://docs.umbraco.com/umbraco-cms/manage-and-publish-content/users-and-members/members.md) |
| Current member in code | [IMemberManager](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/templating-and-rendering/querying/imembermanager.md) |
| Social or SSO login for members | [External login providers](https://docs.umbraco.com/umbraco-cms/run-in-production/security/external-login-providers.md), "Members" sections |
| Members who exist only in the IdP | [Lightweight external members](https://docs.umbraco.com/umbraco-cms/run-in-production/security/lightweight-external-members.md) |
| Two-factor authentication | [Two-factor authentication](https://docs.umbraco.com/umbraco-cms/run-in-production/security/two-factor-authentication.md), "Members" section |
| Headless login (SPA, mobile, Next.js) | [Protected content in the Delivery API](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/headless-and-apis/content-delivery-api/protected-content-in-the-delivery-api.md) |
| Server-to-server access to protected content | [Server-to-server access](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/headless-and-apis/content-delivery-api/protected-content-in-the-delivery-api/server-to-server-access.md) |

## Razor sites: what trips people up

- **Use the built-in surface controllers** (`UmbLoginController`, `UmbRegisterController`,
  `UmbLoginStatusController`) through `Html.BeginUmbracoForm`. They bring antiforgery validation,
  lockout and the member sign-in manager. A hand-rolled login action that checks passwords
  through `IMemberService` loses all of that.
- **Only redirect to local URLs** after login. `UmbLoginController` already checks
  `Url.IsLocalUrl` on its `RedirectUrl`. Any custom login or registration action that accepts a
  return URL must do the same, or it becomes an open redirect.
- **New members must be approved** to sign in, and `UmbRegisterController` always creates them
  approved. If an editor must approve sign-ups, that needs a custom registration action or a
  handler that un-approves new members. Say which one you built.
- **Member password rules are separate settings from user rules:**
  `Umbraco:CMS:Security:MemberPassword` and `MemberDefaultLockoutTimeInMinutes`. The defaults are
  the same as for users, including a **30-day** lockout after 5 failed attempts. On a public site
  that lets anyone lock a member out by guessing, so lower the lockout time and add rate limiting
  ([hardening.md](hardening.md) item 8). Set `MemberAllowConcurrentLogins` explicitly; it falls
  back to the users' `AllowConcurrentLogins`.
- **Group assignment on registration** is code (a `MemberSavedNotification` handler in the
  tutorial). Without it, public-access rules that check a group never match a new member.

## Headless sites (Delivery API member authorization)

- Opt in with `DeliveryApi:MemberAuthorization:AuthorizationCodeFlow:Enabled` and list
  **absolute** `LoginRedirectUrls`. Wildcards are not supported. Restart Umbraco after changing it.
- **Rebuild `DeliveryApiContentIndex`** after enabling or disabling member authorization. Until
  then, protected content is still missing from or present in responses as before, and this is
  the commonly missed step.
- The client uses Authorization Code + PKCE with `client_id` `umbraco-member`. The flow redirects
  to the site's member login page, which you build (for example a content page with an
  `UmbLoginController` form). It is expected at `/Account/Login` by default; the docs show how to
  change it.
  The docs warn against public OIDC clients in web apps. For a server-rendered front end, prefer
  a confidential, server-side flow or server-to-server access (see the table above).
- A SPA on another origin also needs CORS for the token and API calls
  ([cors.md](cors.md)).

## External and lightweight members

Member external logins are registered with `AddMemberExternalLogins` and
`SchemeForMembers(...)`. This is a different builder from the backoffice one, so a provider set
up for editors does not log in members. Lightweight external members skip content-entity storage
entirely. Read the docs' trade-offs (no backoffice editing, groups held differently) before
choosing it.

**Related:** [authorization.md](authorization.md) (what a signed-in member can then reach).

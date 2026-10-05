# Virtual-directory (/NanoAssetSuit) root-relative URL scan and fix (2026-10-05)

**Purpose:** Find every hardcoded root-relative URL/path that breaks when CodexAsset is hosted as the IIS sub-application `/NanoAssetSuit` on live (example: `"/Account/Login"`), then fix it so the app works **both** at site root (`http://localhost:8080/`) and under a virtual directory.

**Status:** The scan is done. Fixes are in the SoT and committed locally (see `docs/LIVE-DEPLOY-CHANGELOG.md` for the hash). Not pushed, not published, no DB changes, and no changes to IIS or the local `Web.config`.

**Scan baseline:** `main` @ `ea07d5b` (clean working tree for `src/`, `tests/`, `database/`).
**Scanned:** `src/**` (`*.cs`, `*.cshtml`, `*.js`, `*.css`, `*.config`, `*.asax`), `database/**` (`*.sql`), `tests/**`. Excluded: `bin`, `obj`, `packages`, `node_modules`, `artifacts`, `_bak`, `_tmp*`, `*.min.js` / `*.min.css`.
**Patterns used:** `Redirect\("/`, `new RedirectResult\(`, `"/[A-Za-z{]` (C#), `["']/["']\s*\+`, `(href|action|src|formaction|data-*)="/`, JS `fetch|ajax|$.get|$.post|url:|location=|window.open` with `'/`, CSS `url(/`, `loginUrl|defaultUrl|defaultRedirect|httpRedirect|rewrite`, `localhost:8080`, `BuildTenantPath|NormalizeTenantSlug|TenantRouteUrl|CreateTenantRedirect|TenantLoginRedirect`, `Url.Content("[^~]`, `.LinkUrl`, `ExternalBaseUrl`.

Line numbers below are **pre-fix** (baseline `ea07d5b`). Every one comes from an actual file read or search hit.

---

## Pattern borrowed from HireHub (already live under `/HireHub`)

| HireHub file | Pattern | Mirrored in CodexAsset |
|---|---|---|
| `HR.Web\Helpers\TenantAuthRedirectHelper.cs` (`GetApplicationVirtualPath`, `StripApplicationVirtualPath`) | `HttpRuntime.AppDomainAppVirtualPath ?? "/"`; strip the app path before parsing `{tenant}/{controller}` segments | New `src/AssetManagement.Web/Helpers/AppPathHelper.cs` (`GetApplicationVirtualPath`, `GetAppBasePath`, `StripApplicationVirtualPath`) |
| `HR.Web\Helpers\ExternalUrlHelper.cs` (`ToAbsoluteUrl`, `StripApplicationPathPrefix`, `BuildRequestBaseUri`) | The base URL includes `Request.ApplicationPath`. When the base URL already includes the vdir, the duplicate prefix is removed from the relative path | `AppPathHelper.CombineBaseAndAppRelative` (fixes the QR double-prefix). CodexAsset `ExternalUrlHelper` already mirrors `BuildRequestBaseUri` |
| `HR.Web\Filters\TenantAuthorizeAttribute.cs` | Login redirect built via `UrlHelper` (app-path aware), not a literal | Login redirects now go through `AppPathHelper.ToAppAbsolute("~/Account/Login")` / `BuildTenantPath` |
| `HR.Web\Services\ReportDocumentBuilder.cs` `ToAbsoluteUrl` | `VirtualPathUtility.ToAbsolute("~/...")` for app-relative assets | `Url.Content("~/...")` / `ToAppAbsolute` for logos, notification links, login form |
| `HR.Web\Web.config:31` / `:18`, `Web.Release.config:7` | `forms loginUrl="~/Account/Login"`; `ExternalBaseUrl="https://nanosoft.africa/Hirehub"` (**includes the vdir**) | CodexAsset `Web.config:51` already uses `~/Account/Login`. On live, `ExternalBaseUrl` should be `https://nanosoft.africa/NanoAssetSuit` (config not changed here) |
| HireHub JS | No global JS base-path variable. Server-generated URLs go into `data-*` attributes | `label-designer.js` now reads `data-sample-scan-url` from the view |

---

## Severity A: breaks login, post-login, MFA, forced password change, or core navigation

| File | Line | Hardcoded value | What breaks on live under /NanoAssetSuit | Fix (status) |
|---|---|---|---|---|
| `Web/Helpers/TenantUrlHelper.cs` | 218 | `"/" + normalized + "/" + controller + "/" + action` (BuildTenantPath) | Every tenant redirect and every `TenantRouteUrl` link (75 refs / 23 files: sidebar, layout log-off form, global search, dashboard redirects) goes to `/slug/...` instead of `/NanoAssetSuit/slug/...` → 404 or the wrong app | Prefixed with `AppPathHelper.GetAppBasePath()` (`HttpRuntime.AppDomainAppVirtualPath`, trailing slash trimmed). **FIXED** |
| `Web/Helpers/TenantUrlHelper.cs` | 437 | `new RedirectResult("/Account/Login")` (CreateTenantRedirect fallback) | Invalid slug → redirect to the site root login, outside the app | `AppPathHelper.ToAppAbsolute("~/Account/Login")`. **FIXED** |
| `Web/Helpers/TenantUrlHelper.cs` | 461 | `new RedirectResult("/Account/Login")` (CreateTenantLoginRedirect) | Unauthenticated access with no tenant → root login (404) | same. **FIXED** |
| `Web/Helpers/TenantUrlHelper.cs` | 467 | `"/Account/Login?returnUrl=" + ...` | Same as above, with returnUrl | `ToAppAbsolute("~/Account/Login") + "?returnUrl="...`. **FIXED** |
| `Web/Views/Account/Login.cshtml` | 46 | `loginAction = "/" + tenantToken + "/Account/Login"` | Tenant login form POSTs to `/slug/Account/Login` (outside the app) → login is impossible | `Url.Content("~/" + tenantToken + "/Account/Login")`. **FIXED** |
| `Web/Helpers/PlatformAdminHelper.cs` | 16, 95 | `PlatformOrganizationsIndexPath = "/Platform/Organizations/Index"` → `new RedirectResult(...)` | Platform-admin post-login redirect (`AccountController` 79, 1457) goes to root | New `PlatformOrganizationsIndexUrl` (app-absolute). Redirect uses it; const kept. **FIXED** |
| `Web/Helpers/LocalReturnUrlHelper.cs` | 48–57 | segment parse of `returnPath` assumes `/slug/Dashboard` | `/NanoAssetSuit/slug/Dashboard` is not recognised as the default landing, so users without Reports.View get bounced to the Dashboard after login | `AppPathHelper.StripApplicationVirtualPath` before parsing. **FIXED** |
| `Web/Helpers/PostLoginRedirectHelper.cs` | 94–100 | `segments[1]` treated as the controller | Under the vdir, `segments[1]` is the slug, so a valid returnUrl is ignored after login | Strip app path before splitting. **FIXED** |
| `Web/Global.asax.cs` | 253, 261 | forced password change → `BuildTenantPath(slug,"Account","ChangePassword")` → `VirtualPathUtility.ToAbsolute` | Redirect loop / 404 on forced password change | Covered by the BuildTenantPath fix (`ToAbsolute` leaves rooted paths unchanged). Non-tenant branch already `~/`. **FIXED via BuildTenantPath** |
| `Web/Controllers/AccountController.cs` | 64 | `Redirect(TenantUrlHelper.BuildTenantLoginPath(...))` | Slug-case-mismatch redirect leaves the app | Covered by BuildTenantPath. **FIXED via BuildTenantPath** |
| `Web/Controllers/AccountController.cs` | 1479 | `CreateTenantRedirect(...)` in `RedirectToLocal` | Post-login / post-MFA default destination leaves the app | Covered by BuildTenantPath. **FIXED via BuildTenantPath** |
| `Web/Controllers/BaseController.cs` | 76 | `CreateTenantRedirect(tenant, controllerName, "Index")` (Landing) | Tenant landing redirect leaves the app | Covered by BuildTenantPath. **FIXED via BuildTenantPath** |

## Severity B: breaks common pages, sidebar, platform admin, or forms

| File | Line | Hardcoded value | What breaks on live | Fix (status) |
|---|---|---|---|---|
| `Web/Areas/Platform/Views/Shared/_PlatformLayout.cshtml` | 41 | `href="@PlatformAdminHelper.PlatformOrganizationsIndexPath"` | Platform brand link → root | `PlatformOrganizationsIndexUrl`. **FIXED** |
| `Web/Areas/Platform/Views/Shared/_PlatformLayout.cshtml` | 68 | same (sidebar brand) | Sidebar brand link → root | same. **FIXED** |
| `Web/Helpers/ImpersonationSessionHelper.cs` | 133–134 | `"/Platform/Organizations/OrganizationDetails/{0}"`, `"/Platform/Organizations/Index"` | Post-impersonation-expiry fallback leaves the app | `AppPathHelper.ToAppAbsolute("~/...")`. **FIXED** |
| `Web/Helpers/ImpersonationSessionHelper.cs` | 147–148 | `"/Dashboard/Index"`, `"/{0}/Dashboard/Index"` | Unlock fallback leaves the app | same. **FIXED** |
| `Web/Filters/ImpersonationExpiryFilterAttribute.cs` | 72 | `?? "/Platform/Organizations/Index"` | Fallback redirect leaves the app | same. **FIXED** |
| `Web/Areas/Platform/Controllers/OrganizationsController.cs` | 657–659 | `Response.Redirect(TenantRouteUrl(...))` | Impersonation guard redirect leaves the app | Covered by BuildTenantPath. **FIXED via BuildTenantPath** |
| `Web/Views/Notifications/Index.cshtml` | 79 | `href="@item.LinkUrl"`. Stored as `"/Assets/Details/{id}"`, `"/PurchaseRequests/Details/{id}"`, `"/PendingApprovals"` by `NotificationService` 126–198, `TransferService` 100–328, `PurchaseRequestService` 309–536, `AssetService` 826–1049, `AssetRequestService` 196–304, `CustodianService` 64, `ReceivingService` 436 | "Open" on every notification → 404 | Rendered via `AppPathHelper.ToAppAbsolute(item.LinkUrl)`. **Stored data unchanged** (no DB write). **FIXED (render-time)** |
| `Web/Helpers/OrganizationLogoHelper.cs` | 17 (path saved at 48) | `urlHelper.Content(logoPath.TrimStart('~'))` with stored `"/Content/OrganizationLogos/..."` | Organisation logos broken (Content() leaves rooted paths unchanged) | `Url.Content("~/" + path.TrimStart('~','/'))`. http(s) and `//` are passed through. **FIXED** |

## Severity C: edge cases, QR, emails, scripts, config

| File | Line | Hardcoded value | What breaks on live | Fix (status) |
|---|---|---|---|---|
| `Web/Helpers/AssetQrUrlHelper.cs` | 20, 32 | `BuildTenantPath(...)` + `CombineBaseAndRelative(ExternalBaseUrl, relative)` | Printed QR codes point outside the app. With the BuildTenantPath fix and `ExternalBaseUrl=https://host/NanoAssetSuit` you'd get a **double prefix** | `AppPathHelper.CombineBaseAndAppRelative`: strips the duplicate vdir only when the base URL already ends with it (HireHub rule). **FIXED** |
| `Infrastructure/Services/MembershipService.cs` | 472, 480–481 | base = request authority (no app path) + `"/" + slug + "/Account/ResetPassword"` | Password-reset email link misses `/NanoAssetSuit` when ExternalBaseUrl is unset or ignored (loopback port mismatch) | New `AssetScanUrlHelper.ResolvePasswordResetBaseUrl(configured, requestUrl, HttpRuntime.AppDomainAppVirtualPath)` appends the app path to the request-derived base. **FIXED** |
| `Infrastructure/Services/UserInvitationService.cs` | 320, 327 | same, `/Account/AcceptInvite` | Invite email link misses `/NanoAssetSuit` | same. **FIXED** |
| `Web/Controllers/BaseController.cs` | 58–59 | `Request.Url.GetLeftPart(UriPartial.Authority)` (ResolveApplicationBaseUrl) | Report / requisition / security-log export logo URLs (`ReportBrandingHelper.cs` 58) miss the vdir | `+ AppPathHelper.NormalizeBasePath(Request.ApplicationPath)`. **FIXED** |
| `Web/Scripts/app/label-designer.js` | 651 | `'/nanosoft/AssetScan/Lookup?code=AST-1001'` | Label-designer sample QR shows a wrong URL (cosmetic) | Reads `data-sample-scan-url` (set in `LabelDesigner.cshtml` via `AssetQrUrlHelper.BuildScanUrl`). Old value kept as fallback. **FIXED** |
| `Web/Controllers/DatabaseConsoleController.cs` | 77 | `"/Sys/Database"` fallback (only when `Request == null`) | Practically unreachable | `AppPathHelper.ToAppAbsolute("~/Sys/Database")` for consistency. **FIXED** |
| `Web/Scripts/app/asset-label-zebra.js` | 77–84 | `'/Scripts/vendor/'` fallback | Only used if the script tag isn't found. Normal path derives the prefix from the `<script src>`, which views emit via `Url.Content("~/Scripts/app/asset-label-zebra.js")`, so it already includes the vdir | **LEFT** (safe in practice) |
| `Web/smtp.secrets.config` | 10 | `ExternalBaseUrl = http://localhost:8080` | Local value only | **LEFT** (config not touched). **Live must set** `ExternalBaseUrl = https://nanosoft.africa/NanoAssetSuit` (HireHub: `https://nanosoft.africa/Hirehub`) |
| `Runner/Program.cs` | 222, 347 | `"/" + slug + "/Account/Login"`, `http://localhost:{port}/nanosoft/...` | Console output of the CLI tool only | **LEFT** (not web) |

### Counts (true positives)

| Severity | Rows | Fixed directly | Fixed via BuildTenantPath | Left (safe / config / non-web) |
|---|---|---|---|---|
| A | 12 | 8 | 4 | 0 |
| B | 8 | 7 | 1 | 0 |
| C | 9 | 6 | 0 | 3 |
| **Total** | **29** | **21** | **5** | **3** |

---

## Checked and SAFE (false positives or already vdir-aware)

- `Url.Action(...)` (382 refs / 125 files), `Url.RouteUrl("Tenant"/"Default", ...)`, `Url.Content("~/...")` (94 refs / 39 files), `Html.BeginForm(...)` without a literal action, `RedirectToAction(...)`: all include the app path automatically.
- `TenantUrlHelper.TenantRouteUrl` non-tenant branch → `url.RouteUrl("Default", ...)`: already safe.
- `Web.config:51` `forms loginUrl="~/Account/Login"`. `Global.asax.cs` 268–275 uses `FormsAuthentication.LoginUrl` (a `~` path that `Response.Redirect` resolves). `customErrors mode="Off"`. No `httpRedirect` / rewrite rules.
- `Global.asax.cs` 203–216, 238–242 `path.Contains("/account/login")` etc.: `Contains` matches with or without the prefix.
- `Filters/TenantFilterAttribute.cs` 246, `Filters/BusinessExceptionFilter.cs` 64 `IndexOf("/api/...")`: substring checks. `BusinessExceptionFilter.cs` 46 redirects to the absolute referrer.
- `Areas/Platform/Controllers/OrganizationsController.cs` 210 `portalUrl + "/Account/Login"`: `portalUrl` comes from `ExternalUrlHelper.GetTenantPortalUrl`, which already includes `Request.ApplicationPath` (same as HireHub).
- Platform `Organizations/Index.cshtml` 109–110, `OrganizationDetails.cshtml` 29–31: `Url.RouteUrl` + `new Uri(Request.Url, ...)`. `_PlatformLayout.cshtml` 167 uses `Url.Content("~/Account/DownloadAdminCredentials")`.
- `PendingApprovalsController` `ResolveDetailsUrl` and `AssetScanController` `DetailsUrl` use `Url.Action` / `TenantRouteUrl`.
- `XlsxImportTemplateBuilder.cs` 426–446 `PartName "/xl/..."`: OOXML package part names, not URLs.
- `ReportBrandingHelper.cs` 80 builds a `"/" + path` logo path that is combined with `applicationBaseUrl` (now includes the vdir).
- CSS: no `url(/...)` hits. JS: no `fetch` / `$.ajax` / `$.get` / `$.post` / `location =` with root-relative literals (JS gets URLs via `data-*` attributes rendered with `Url.Action`).
- `database/**`: no email templates with root-relative or absolute app links.

## Does fixing BuildTenantPath alone cover TenantRouteUrl?

**Yes.** Both `TenantRouteUrl` overloads (`TenantUrlHelper.cs` 259, 297) call `BuildTenantPathWithExtraQuery`, which calls `BuildTenantPath` whenever the tenant slug is valid, and use `url.RouteUrl("Default")` (already safe) otherwise. `CreateTenantRedirect`, `BuildTenantLoginPath`, `CreateTenantLoginRedirect`, the Global.asax forced-password-change redirect, and `AssetQrUrlHelper` also go through it. So the one-line BuildTenantPath fix covers all 75 `TenantRouteUrl` references in 23 files, plus 8 `CreateTenantRedirect` and 7 `TenantLoginRedirect` references. It does **not** cover the literal `/Account/Login` fallbacks, `Login.cshtml`, `PlatformAdminHelper`, impersonation fallbacks, notification links, logos, segment parsing in `LocalReturnUrlHelper` / `PostLoginRedirectHelper`, or email/report base URLs. Those were fixed separately (above).

## Verification performed (no IIS or DB changes)

1. **Build:** `MSBuild AssetManagementModule.sln /p:Configuration=Release`. Domain, Application, Infrastructure, Web and Runner all build. Both test projects fail to compile because of **pre-existing errors unrelated to this change** (`ApprovalWorkflowSelfApproveTests`, `DepartmentRequisitionFlowResolverTests`, `DepartmentHierarchyRulesTests`, `PerformanceTestFixture` reference APIs that don't exist in `src` at `ea07d5b`). The new NUnit cases in `AssetScanUrlHelperTests.cs` therefore can't run through vstest until that is fixed.
2. **Path-building probe:** a throwaway console exe in `%TEMP%` against the built `AssetManagement.Web.dll` / `AssetManagement.Application.dll`. **25/25 PASS**: sub-app `/NanoAssetSuit` vs root `/` for `ToAppAbsolute`, `StripApplicationVirtualPath`, `CombineBaseAndAppRelative` (no double prefix with `ExternalBaseUrl=https://nanosoft.africa/NanoAssetSuit`), `ResolvePasswordResetBaseUrl` (3-arg and legacy 2-arg), and `BuildTenantPath` / `BuildTenantLoginPath` outside ASP.NET (unchanged root behaviour).
3. **Razor views precompiled under virtual path `/NanoAssetSuit`** (`aspnet_compiler -v /NanoAssetSuit` on a temp copy). All views compile, including the 4 changed views. Pre-existing unrelated issue: `Views/Permissions/Index.cshtml(11)` "Unexpected `if` keyword after `@`" had to be excluded from the temp copy so the compiler could continue. **Not fixed here, and worth a separate look.**
4. **HTTP (read-only GET) against the existing local IIS:** `http://localhost:8080/Account/Login` → 200 (root still serves the currently deployed build; IIS not modified). `http://localhost:8080/NanoAssetSuit/Account/Login` → 200 "Organization Not Found": there is **no local `/NanoAssetSuit` sub-app**, so the root app treats `NanoAssetSuit` as a tenant token.
5. **Not done (needs approval):** a full end-to-end repro under `/NanoAssetSuit` needs either Allan's existing IIS wiring for the sub-app or approval to create a **temporary** sub-app / IIS Express site pointing at a published copy, with a LocalDB E2E connection string in the copy only.

## Prepare / do-not-do

- Nothing was pushed, published to nanosoft.africa, or written to `.\SQLEXPRESS`. No IIS config or local `Web.config` was changed.
- Live config to check at deploy time: `ExternalBaseUrl` (platform setting or `Web.config`) = `https://nanosoft.africa/NanoAssetSuit`.

# .NET 10 migration plan — Nanosoft Asset Management (AssetSuite / NIS)

This document describes the side-by-side port from **.NET Framework 4.0 + ASP.NET MVC 3** to **.NET 10 + ASP.NET Core MVC**. The legacy app remains under `src/` and continues to run on IIS. The new stack lives under `net10/`.

**Source of truth for this plan:** GitHub `main` in this repository. Allan’s local release `rel1016a` may be ahead; the `net10/` layout uses **compile links** into `src/AssetManagement.*` so unpushed local changes can be merged into GitHub and picked up without restructuring the port.

---

## Phase 1 delivered (this branch)

| Item | Location |
|------|----------|
| Migration plan | `docs/NET10-MIGRATION.md` (this file) |
| .NET 10 solution | `net10/AssetSuite.sln` |
| Web host | `net10/src/AssetSuite.Web` (`Program.cs`, Autofac, cookie auth, tenant routes) |
| Linked domain/application/infrastructure | `net10/src/AssetSuite.{Domain,Application,Infrastructure}` |
| Tests | `net10/tests/AssetSuite.Tests`, `net10/tests/AssetSuite.IntegrationTests` |
| First vertical slice | Login (POST), layout/nav, read-only **Privacy** / **Terms** |

---

## Inventory (legacy `src/AssetManagement.Web` + layers)

### Controllers (37 root + 5 Platform area)

| Area | Controllers |
|------|-------------|
| Tenant app | `Account`, `Home`, `Dashboard`, `Assets`, `AssetCategories`, `AssetTypes`, `AssetSubTypes`, `AssetRequests`, `AssetScan`, `Assignments`, `AuditLogs`, `Captcha`, `Claims`, `Custodian`, `DatabaseConsole`, `Departments`, `Documents`, `Incidents`, `InsurancePolicies`, `Maintenance`, `Notifications`, `PendingApprovals`, `Permissions`, `PurchaseRequests`, `Purchases`, `Reports`, `Returns`, `Roles`, `Search`, `SecurityLogs`, `Settings`, `Suppliers`, `Transfers`, `UserInvitations`, `Users` |
| Platform area | `Organizations`, `Licenses`, `PlatformSettings`, `SecurityLogs`, `Users` |
| Base / shared | `BaseController` |

### Global filters (`FilterConfig`)

`AntiForgeryExceptionFilter`, `PlatformAdminTenantRedirect`, `HandleError`, `BusinessExceptionFilter`, `NoCache`, `AuditLog`, `TenantFilter`, `RequireMfaEnforcement`, `ImpersonationExpiry`, optional `FreshPortalSecurity`.

### Auth and session

- **Forms Authentication** (`System.Web.Security`), ticket user-data format (`userId|orgId|accessToken|uaHash|[RequirePasswordChange]`).
- **Post-authenticate** pipeline in `Global.asax.cs`: access-token validation, UA fingerprint, lockout, forced password change.
- **Session** for MFA pending state, impersonation, legal consent, email verification.
- **Authorization**: custom `PermissionAuthorize`, `TenantAuthorize`, `AnyPermissionAuthorize` + `IAuthorizationService` / module permissions.

### Dependency injection

- **Autofac** (`AutofacConfig`) — per-HTTP-request lifetime, full service graph (~70+ registrations).

### Data access

- **ADO.NET** via `ISqlConnectionFactory`, `UnitOfWork`, `UserAccountRepository`, query repositories under `Infrastructure/Queries` and `Infrastructure/Repositories`.
- **Schema**: `database/scripts/` (unchanged; no destructive migrations in port).
- **No EF** at runtime.

### Configuration

- `Web.config` / `connectionStrings.config` / `machineKey.config` / `smtp.secrets.config` (secrets not in repo).
- `AppSettings` for feature flags (`FreshPortalMode`, `MfaAllowAnyCode`, SMTP, attachment paths, etc.).

### Views / UI

- Razor 3 views under `Views/` and `Areas/Platform/Views/`.
- `_Layout`, partials (`_Navbar`, `_LoginBackground`, impersonation bar, support overlay).
- Static content: `Content/` (Bootstrap, `site.css`, module CSS), `Scripts/`.

### Bundling

- `BundleConfig` + `System.Web.Optimization` (CSS/JS bundles). **ASP.NET Core:** static files + optional LibMan/npm/vite; no Optimization bundle.

### Background / async work

- **Transactional outbox** (`OutboxWriter`, `OutboxDispatcher`, `OutboxMessage` entity) — not Hangfire/Quartz.
- Email and webhooks enqueued via outbox (processed by dispatcher/worker pattern in Infrastructure).

### Security / dev-only (must not ship to production)

- `SecurityDiagnostics` + `dev-security-output.log` when `MfaAllowAnyCode=true` (see `.cursor/rules/production-dev-security-cleanup.mdc`).

---

## MVC 3 / System.Web → ASP.NET Core mapping

| Legacy | .NET 10 target |
|--------|----------------|
| `HttpApplication` / `Global.asax` | `Program.cs`, middleware pipeline |
| `RouteConfig` + `TenantRouteConstraint` | Endpoint routing + `TenantRouteConstraint` (`net10/.../Infrastructure`) |
| `FilterConfig` attributes | Middleware + `IAsyncActionFilter` / endpoint filters |
| `FormsAuthentication` cookie | `CookieAuthentication` with compatible **user-data claims** (optional: machine-key interop for shared `.ASPXAUTH` with IIS — not done in phase 1) |
| `HttpContext.Current` / Session | `IHttpContextAccessor`, `ISession`, `AspNetCoreHttpBridge` for linked services |
| `DependencyResolver` / Autofac MVC | `Autofac.Extensions.DependencyInjection` + `ConfigureContainer` |
| `System.Data.SqlClient` | `System.Data.SqlClient` NuGet (same ADO.NET patterns) |
| `ConfigurationManager` | `IConfiguration` + `appsettings.json` (+ user secrets / env) |
| `BundleConfig` | `wwwroot` + linked `legacy-content` from repo `Content/` (phase 1) |
| `Areas` | ASP.NET Core areas (Platform area port later) |
| `ValidateAntiForgeryToken` | Built-in antiforgery |
| `JsonResult` / `JsonRequestBehavior` | `Json()` / `IActionResult` |
| `HandleError` | Exception handler + ProblemDetails |
| `System.Web.Caching` (`ReferenceDataCache`) | `IMemoryCache` (stub in phase 1; full port batch 2) |

---

## Risks and mitigations

| Risk | Mitigation |
|------|------------|
| GitHub behind local `rel1016a` | Compile-link legacy sources; merge local → GitHub before large ports |
| Shared auth cookie with IIS | Phase 1 uses same **name** (`.ASPXAUTH`) but Core cookie format; document dual login or add machine-key ticket decrypt later |
| `System.Web` in Infrastructure services | Ported copies under `net10/.../Services` + `AspNetCoreHttpBridge` |
| Large controller surface | Batch checklist below; permission filters per controller |
| Linux CI cannot build net40 Web | `dotnet build net10/AssetSuite.sln` on Linux; full MVC validation on Windows/IIS |
| SQL required for real login | Connection string via config; integration tests avoid DB on login GET |
| Outbox / email without worker | Register dispatcher; host background service in batch 4 |

---

## Batched port order (checklists ~10 items)

### Batch 1 — **Done (phase 1)**

- [x] Solution skeleton `net10/AssetSuite.sln`
- [x] Link `AssetManagement.Domain` sources
- [x] Link `AssetManagement.Application` (+ compat: label layout JSON, Excel import)
- [x] Link `AssetManagement.Infrastructure` (+ compat: SQL factory, auth helpers, org scope, membership)
- [x] `Program.cs`, IIS-friendly hosting (`UseForwardedHeaders` ready to add)
- [x] Autofac core auth registrations
- [x] Cookie authentication + ticket user-data claims
- [x] Tenant routes `{tenant}/{controller}/...`
- [x] Account `Login` GET/POST (no MFA/legal/captcha yet)
- [x] Layout + Privacy/Terms read-only pages
- [x] Unit + integration smoke tests

### Batch 2 — **Suggested next** (global pipeline + read helpers)

1. Port `TenantFilterAttribute` → filter + tenant context items
2. Port `PermissionAuthorize` / `IAuthorizationService` integration
3. Full `ReferenceDataCache` with `IMemoryCache`
4. Expand Autofac module toward parity with `AutofacConfig` (query services)
5. Static files: move to `wwwroot` with cache headers (`NoCache` equivalent)
6. Global exception handling (`BusinessExceptionFilter`)
7. `PlatformAdminTenantRedirect` behavior
8. Login GET: organization branding (slug → org) without failing when DB down
9. Anti-forgery + rate limit parity tests
10. CI job: `dotnet build` + `dotnet test` on `net10/`

### Batch 3 — Auth completion

1. MFA verify/setup flows + session keys
2. Legal consent modal + `LegalConsentSession`
3. Email verification gate
4. Forgot password / reset / invitation accept
5. Captcha on login
6. `PostAuthenticate` parity (access token + UA hash validation middleware)
7. Impersonation session + platform bar
8. `ChangePassword` / forced change redirect
9. Logout + session cleanup
10. Optional: decrypt legacy Forms tickets for single sign-on with IIS

### Batch 4 — Dashboard + assets read path

1. `DashboardController.Index` + view model
2. `Reports.View` permission
3. `AssetsController` index/details read-only
4. Search autocomplete (read)
5. Notifications badge (read)
6. Navbar partials from legacy `_Layout`
7. Outbox background hosted service
8. Attachment storage registration
9. E2E smoke against SQL (Windows)
10. Performance: pagination defaults (10 rows)

### Batch 5–N (abbreviated)

- **Operations:** assignments, transfers, returns, maintenance, incidents, claims, purchases, PRs, approvals workflow UI.
- **Admin:** users, roles, permissions, departments, suppliers, settings (incl. label printer).
- **Platform area:** organizations, licenses, platform settings, super-admin flows.
- **Reports/export:** `ReportService`, document generation, Excel exports.
- **Hardening:** remove `SecurityDiagnostics` from Release, secrets via vault, production checklist.

---

## Project layout (`net10/`)

```
net10/
  AssetSuite.sln
  src/
    AssetSuite.Domain/          # links ../../src/AssetManagement.Domain/**
    AssetSuite.Application/     # links Application + Compat/*
    AssetSuite.Infrastructure/  # links Infrastructure + Compat/* + Services/*
    AssetSuite.Web/             # ASP.NET Core MVC host
  tests/
    AssetSuite.Tests/
    AssetSuite.IntegrationTests/
```

**Do not modify** `src/AssetManagement.*` net40 projects for port logic; add compat only under `net10/`.

---

## Configuration

| Setting | Purpose |
|---------|---------|
| `ConnectionStrings:AssetManagementConnection` | Same SQL Server DB as legacy |
| `AssetSuite:CookieName` | Default `.ASPXAUTH` |
| `AssetSuite:MfaAllowAnyCode` | Dev only; never true in production |
| User secrets / environment | SMTP, machine key (if interop added) |

IIS: deploy as ASP.NET Core Module in-process or out-of-process behind existing site; reverse-proxy path base if mounted under `/nanosoft`.

---

## Verification commands

```bash
cd net10
dotnet build AssetSuite.sln
dotnet test AssetSuite.sln
```

Legacy NUnit (Windows / full framework): `tools/ci/run-cloud-unit-tests.sh` (Linux builds tests only; Web project requires Windows MSBuild).

---

## Blockers (current)

- **Real login E2E** requires SQL Server + seeded tenant (e.g. `nanosoft@asset.local`) — not available on Linux cloud agent.
- **Forms cookie interop** with legacy IIS not implemented; dual cookies or sticky routing needed for parallel run.
- **MFA, legal consent, captcha** not ported (batch 3).
- **Autofac graph** is minimal; most controllers will fail DI until batch 2 expands registrations.

---

*Last updated: phase 1 branch `cursor/net10-migration-phase1-924e`.*

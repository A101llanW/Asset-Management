# Asset Management Module — ASP.NET Web Application

Browser-based asset management built with **ASP.NET MVC** on **.NET Framework 4.0**: Razor views, Forms Authentication, permission-driven workflows, and SQL Server storage.

## Solution Structure

- `AssetManagementModule.sln`
- `src/AssetManagement.Web` — **ASP.NET MVC 3** web app (controllers, Razor views, Bootstrap UI, IIS)
- `src/AssetManagement.Application` — business services and view models
- `src/AssetManagement.Domain` — entities and enums
- `src/AssetManagement.Infrastructure` — SQL Server repositories, auth, file storage, audit
- `tests/AssetManagement.Tests` — NUnit service rule tests
- `tests/AssetManagement.PerformanceTests` — NUnit `[Category("Performance")]` SQL benchmark tests (requires large dataset seed)

## Runtime Stack

- **ASP.NET MVC 3** web application (this is the system users run in the browser)
- **.NET Framework 4.0**, C# 6
- **Forms Authentication** + **Autofac** dependency injection
- **SQL Server** (`AssetManagementConnection` in `Web.config`)
- Schema/seed from `database/scripts/` on startup when `AutoInitializeDatabase=true` (no EF migrations at runtime)

## Implemented Scope

- Assets CRUD, custody (assign / transfer / return), configurable approvals (transfer, disposal, purchase)
- Asset requests, purchase requests, departments, suppliers, users, roles & permissions
- Maintenance, incidents, insurance claims (list, detail, asset tab history)
- Document upload/download per asset (filesystem storage, configurable root in Settings)
- Dashboard KPIs, audit logs, notifications, depreciation engine (service-level)
- **Phase 4 — UX & productivity:** global search (tag, serial, custodian, department); list UX with search/filter/pagination/sort on Assets, Assignments, and Asset Requests with role-aware defaults; bulk asset actions (permission-gated, audited); mobile-friendly return/transfer wizards; custodian self-service (My Assets, report issue, request return, acknowledge receipt); dashboard v2 trends (assignments/month, approval backlog, department book value, loss/damage rate); lifecycle panel on asset details linking depreciation, insurance, and disposal flows

## Demo Credentials

After running migration `database/scripts/004_Migrations/005_Multitenancy.sql`:

- **Platform admin:** `superadmin@asset.local` / `P@ssw0rd!` at `/Account/Login` (platform only — not a tenant company login)
- **Tenant users:** sign in at `/{organization-slug}/Account/Login` (primary demo org: `/nanosoft/Account/Login`)
- **Company admin:** `{slug}@asset.local` / `P@ssw0rd!` (e.g. `nanosoft@asset.local` for slug `nanosoft`)
- Legacy tenant users at `/default/Account/Login`: asset manager, procurement, finance, department head, staff, auditor — all `P@ssw0rd!`
- Additional staff users: `itstaff@asset.local`, `opsstaff@asset.local`, `labtech@asset.local` — all `P@ssw0rd!`

### Demo data (default tenant)

Re-run `tools/database/Initialize-Database.ps1` to apply the diverse seed (`017_DiverseDemoAssets.sql`). The default org includes **19 assets** across IT, Finance, HR, Operations, and Admin — laptops, desktops, printers, routers, furniture, lab equipment, and a fleet vehicle — with linked assignments, incidents, maintenance, insurance claims, asset requests, and purchase requests.

Example asset tags for scan/label testing: `IT-LTP-001` (barcode `BC-IT-LTP-001`), `OPS-MED-001` (under maintenance), `IT-LTP-003` (damaged + claim `CLM-2026-003`).

### Asset import template

Download the Excel template from **Assets → Import from Excel → Download template**. The workbook has three sheets:

| Sheet | Purpose |
| ----- | ------- |
| **Instructions** | How to fill in the file (same text as the Import Assets page) |
| **Import** | Row 1 = column headers (`*` = required), row 2 = Required/Optional, row 3 = example, row 4+ = your data |
| **Lists** | Hidden pick-lists for condition, status, depreciation, and insurance columns |

School opening-balance imports use **Department**, **Class**, and **Quantity**:

- **Department** — `Classroom` for class assets; `Administration` or `Information Technology` for admin/IT rows
- **Class** — required when Department is Classroom (e.g. `2A`, `3B`); for admin/IT rows, the sub-unit name (e.g. `Comp Lab - Senior`)
- **Quantity** — optional unit count; if blank, counts are parsed from Description (e.g. `12 units`)

Required columns: AssetName, AssetCategory, AssetType, Brand, Model, PurchaseDate, AcquisitionCost. Asset tags and QR codes are generated on import. The first pass can auto-provision missing departments, categories, and asset types. Fixture copies live at `e2e/fixtures/school-import-template.xlsx` and `database/fixtures/nis-school-opening-balance.xlsx`.

Workflow highlights:

| Asset | Scenario |
| ----- | -------- |
| `OPS-MED-001` | Under maintenance (`MNT-2026-002`) — lab microscope calibration |
| `IT-LTP-003` | Damaged laptop → incident → completed repair → claim under review |
| `IT-LTP-001` | Minor resolved incident (`INC-2026-001`) |
| HR pending | Asset request for `HR-CHR-002`; purchase request `PR-2026-004` (GPS trackers) |

New organizations receive an auto-generated portal slug (Recruitment-style); platform admins see the login URL on the organization details page.

### Multitenancy & elevation

Platform admins manage organizations at `/Platform/Organizations`. Elevation into a tenant requires company-admin approval, then session-based impersonation (no auth-cookie swap). Company admins see a freeze overlay while elevation is active.

## Local Setup

From **any** PowerShell window (use your clone path):

```powershell
& "C:\path\to\Asset-Management\Start-Dev.ps1"
```

Or double-click `Start-Dev.cmd` in the repo root. The script restores packages, creates the IIS site if needed (`-SetupSite` on first run may require an elevated PowerShell), builds, deploys to `C:\inetpub\AssetManagement`, and waits for the app.

Manual steps (same result):

1. Open `AssetManagementModule.sln` in Visual Studio (or use MSBuild from a Developer PowerShell prompt).
2. Confirm **`AssetManagement.Web`** is the startup project (bold in Solution Explorer). If F5 still says a class library cannot be started, run `.\tools\dev\Reset-VisualStudioStartup.ps1` and reopen the solution.
3. Ensure SQL Server LocalDB or SQL Express is available.
4. Adjust `AssetManagementConnection` in `src/AssetManagement.Web/Web.config` if needed.
5. One-time IIS setup (elevated PowerShell): `.\tools\deploy\Setup-IisSite.ps1`
6. Build and run on IIS: `.\tools\deploy\Start-IisDev.ps1 -WaitForReady`
7. Open `http://localhost:8080/nanosoft/Account/Login`
8. Database scripts run automatically when `AutoInitializeDatabase` is enabled (Debug).

Visual Studio **F5** starts `AssetManagement.Web` on IIS Express (shared profile in `AssetManagementModule.slnLaunch`). If an existing `.suo` still points at a library, run `.\tools\dev\Reset-VisualStudioStartup.ps1`, then reopen the solution — or pick **AssetManagement.Web (IIS Express)** from the toolbar. F5 on Domain / Application / Infrastructure now launches `Start-Dev.ps1` instead of the class-library error. `AssetManagement.Runner` is the executable alternative (local IIS). Local IIS on port 8080 remains the supported day-to-day path (`C:\inetpub\AssetManagement`).

## Development vs Production

The repo stays in **development mode** by default. Daily work uses `Web.config` as committed — no Release transforms are applied when you build for local IIS with the **Debug** configuration.

| Setting | Development (`Web.config` / Debug) | Production (Release publish only) |
| ------- | ---------------------------------- | --------------------------------- |
| `compilation debug` | `true` | `false` (via `Web.Release.config`) |
| `customErrors` | `Off` (full error details) | `RemoteOnly` |
| `AutoInitializeDatabase` | `true` (schema/seed on startup) | `false` — run `tools/database/Initialize-Database.ps1` explicitly |
| `RequireSecureCookies` | `false` (HTTP localhost works) | `true` |
| `RequireHttpsRedirect` | `false` | `true` (HTTP → HTTPS redirect) |
| `RequireMfaForAllUsers` | `false` (MFA only for platform/company admin) | `true` (all users) |
| `MfaAllowAnyCode` | `true` (E2E/dev bypass) | `false` |
| Auth cookie `Secure` | Only when HTTPS or `RequireSecureCookies=true` | `true` (HTTPS + app setting) |
| Security headers (HSTS, etc.) | Not added | Added in Release transform |
| Connection string | Inline in `Web.config` | External `connectionStrings.config` |
| Forms auth `machineKey` | Auto-generated (Debug) | External `machineKey.config` (Release) |

Full before/after deployment checklist: [docs/DEPLOYMENT-READINESS.md](docs/DEPLOYMENT-READINESS.md).

`Web.Debug.config` reaffirms dev values on Debug builds. `Web.Release.config` applies production-ready settings **only** when publishing or building with **Release** — the running dev app is unchanged.

## Production Deployment

Publish with **Release** configuration so `Web.Release.config` transforms are applied:

- `debug=false`, `customErrors=RemoteOnly`
- `AutoInitializeDatabase=false` — schema changes are **not** applied on app startup
- `RequireSecureCookies=true` plus `httpCookies requireSSL="true" sameSite="Lax"`
- Security headers (HSTS, `X-Content-Type-Options`, `X-Frame-Options`)
- Connection strings moved to an external file
- Forms auth `machineKey` moved to an external file (`machineKey.config`)

**Before deploying:**

1. Copy `src/AssetManagement.Web/connectionStrings.config.example` to `connectionStrings.config` in the same folder (this file is gitignored).
2. Set the production SQL Server connection string in `connectionStrings.config`.
3. Copy `src/AssetManagement.Web/machineKey.config.example` to `machineKey.config` (gitignored). Generate unique validation and decryption keys; deploy the **same** file to every IIS node in a web farm. See [docs/DEPLOYMENT-READINESS.md](docs/DEPLOYMENT-READINESS.md#forms-auth-machinekey).
4. Run database migrations explicitly:

```powershell
.\tools\database\Initialize-Database.ps1 -ServerInstance "YOUR_SERVER" -Database "AssetManagementModuleDb"
# or incremental:
.\tools\database\Invoke-Migrations.ps1 -Targets @("YOUR_SERVER|AssetManagementModuleDb")
```

Database operational scripts (including **destructive E2E reset** guards) are documented in [`tools/database/README.md`](tools/database/README.md).

5. Publish with **Release** configuration (Visual Studio or MSBuild). The publish output includes the transformed `Web.config` referencing `connectionStrings.config` and `machineKey.config` — deploy all three files to the server.

## Build & Test

```bash
dotnet build src/AssetManagement.Application/AssetManagement.Application.csproj
dotnet test tests/AssetManagement.Tests/AssetManagement.Tests.csproj
```

### Performance tests (NUnit, Category: Performance)

Benchmarks asset list, dashboard KPIs, and global search against a LocalDB database seeded with ~100k assets. Tests auto-skip when the large dataset is not present.

```powershell
# One-time: initialize schema + optional 100k asset seed (~5–15 min)
.\tools\database\Initialize-Database.ps1 -IncludeLargeDataset

# Run performance suite only
dotnet test tests/AssetManagement.PerformanceTests/AssetManagement.PerformanceTests.csproj --filter "TestCategory=Performance"
```

Override the connection string with `ASSETMANAGEMENT_TEST_CONNECTION` when not using LocalDB defaults. Schedule in CI as a nightly job rather than on every PR.

Second demo tenant for isolation smoke tests: `/demo-b/Account/Login` (`demo@asset.local` / `P@ssw0rd!`).

### End-to-end tests (Playwright)

Full-stack browser tests live in `e2e/` and exercise auth, custody workflows, disposal approvals, scan lookup, department scope, and validation rules against **local IIS** + SQL Server.

**Prerequisites:** Node.js 18+, IIS with ASP.NET 4.x, SQL Server LocalDB (or Express).

```powershell
# From repo root — installs Playwright, resets an isolated E2E database, builds the web app, and runs the suite
.\e2e\scripts\run-e2e.ps1
```

The E2E run uses database `AssetManagementModuleDb_E2E` and temporarily points `Web.config` at it. Demo credentials apply (`default@asset.local` / `P@ssw0rd!` for the default tenant company admin; `superadmin@asset.local` for platform admin).

**CI:** Add a job that runs `.\e2e\scripts\run-e2e.ps1` on Windows agents with IIS, SQL Server LocalDB, and Node.js 18+. Unit tests (`dotnet test tests/AssetManagement.Tests`) can run on every PR; schedule E2E nightly if agent setup is heavy.

Build the web project with MSBuild / Visual Studio (MVC 3 Web Application targets).

**Note:** The legacy `.build/DbInit` tool is deprecated. Use [`tools/database/`](tools/database/README.md) scripts and app startup (`DatabaseConfig`) instead.

## SQL Server Guidance

See `docs/SQL_SERVER_CONFIGURATION.md`.

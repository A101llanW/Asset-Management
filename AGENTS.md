# CodexAsset — Agent Instructions

## Overview

ASP.NET MVC 3 asset management app on .NET Framework 4.x (C# 6).
Layers: Domain → Application → Infrastructure → Web (IIS).

Solution: `AssetManagementModule.sln`

Startup project for Visual Studio F5 is **`AssetManagement.Web`** (IIS Express). `Domain`, `Application`, `Infrastructure`, and test projects are class libraries and cannot be started directly. `AssetManagement.Runner` is the executable that starts local IIS.

## Cursor Cloud specific instructions

Cloud agents for this repo should run on **Windows with IIS**, not Linux.

### Build and run (Windows + IIS)

Prerequisites: IIS with ASP.NET 4.x, MSBuild/Visual Studio Build Tools, SQL Server LocalDB or Express.

One-time site setup (elevated PowerShell):

```powershell
.\tools\deploy\Setup-IisSite.ps1
```

Build, deploy, and verify on IIS:

```powershell
& "C:\path\to\Asset-Management\Start-Dev.ps1"
```

Or from the repo root:

```powershell
.\Start-Dev.ps1
```

Open `http://localhost:8080/nanosoft/Account/Login` (demo: `nanosoft@asset.local` / `P@ssw0rd!`).

Alternative entry points:

- `.\tools\deploy\Publish-To-Iis.ps1` — rebuild and mirror `src\AssetManagement.Web` to `C:\inetpub\AssetManagement`
- `src\AssetManagement.Runner\bin\Release\net40\AssetManagement.Runner.exe` — invokes `Start-IisDev.ps1`
- `.\e2e\scripts\start-app.ps1` — IIS by default; pass `-UseIisExpress` for legacy IIS Express

Repository-managed cloud environment (`.cursor/environment.json`) uses:

- `tools/ci/setup-windows-environment.ps1` on install
- `tools/deploy/Start-IisDev.ps1 -WaitForReady` on start

### Verify changes without IIS (Linux fallback)

If a cloud agent boots on Linux, only library/unit-test verification is available:

```bash
bash tools/ci/run-cloud-unit-tests.sh
```

Do **not** expect to build `AssetManagement.Web` or run the MVC app on Linux.

### Web / Razor changes

If you change controllers or views under `src/AssetManagement.Web/`:

1. Match `@model` to the action's view model (see `.cursor/rules/razor-controllers.mdc`).
2. Note in the PR that MSBuild/Web validation requires Windows CI or local Visual Studio.
3. Do not commit `connectionStrings.config`, `machineKey.config`, or `smtp.secrets.config`.

### Database

- Schema: `database/scripts/` — applied via `tools/database/Initialize-Database.ps1` (Windows/PowerShell).
- Dev: `AutoInitializeDatabase=true` in Web.config when running locally.
- Cloud: prefer unit tests; do not run destructive E2E reset scripts unless explicitly asked.

### Auth / MFA (dev and cloud testing)

- Demo tenant login: `/nanosoft/Account/Login`
- Demo user: `nanosoft@asset.local` / `P@ssw0rd!`
- For cloud runs, assume MFA bypass is enabled via environment (`MfaAllowAnyCode=true`).
- Never enable dev MFA bypass in Release/production configs.

### Conventions

- .NET Framework 4.0, C# 6 only — no C# 7+ syntax.
- NUnit 2.x in tests (`Assert.Throws<T>`, not MSTest).
- Paginate large lists (default ~10 rows + "View more").
- Scope all data access by tenant/organization ID.

### Production checklist

Before shipping: remove `SecurityDiagnostics` and dev-security logging (see `.cursor/rules/production-dev-security-cleanup.mdc`).

## Key paths

| Path | Purpose |
|------|---------|
| `src/AssetManagement.Web/` | MVC app (Windows/IIS) |
| `src/AssetManagement.Application/` | Business services |
| `tests/AssetManagement.Tests/` | NUnit unit tests (cloud-verifiable) |
| `database/scripts/` | SQL migrations |
| `tools/database/` | DB init/migration scripts |
| `e2e/` | Playwright (Windows + IIS Express only) |

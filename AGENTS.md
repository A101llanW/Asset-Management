# CodexAsset — Agent Instructions

## Overview

ASP.NET MVC 3 asset management app on .NET Framework 4.x (C# 6).
Layers: Domain → Application → Infrastructure → Web (IIS).

Solution: `AssetManagementModule.sln`

## Cursor Cloud specific instructions

Cloud agents run on **Ubuntu Linux**. This repo's web app targets **Windows (IIS + MSBuild)**.
Do **not** expect to build `AssetManagement.Web` or run Playwright E2E in the cloud VM.

### Verify changes (cloud)

Cloud VMs run Ubuntu. Tests target **net40**, so `dotnet test` does not work on Linux — use Mono + NUnit 2.x instead.

After C# changes in Domain, Application, Infrastructure, or Tests:

```bash
bash tools/ci/run-cloud-unit-tests.sh
```

This restores, builds, and runs all unit tests via `mono` and the NUnit 2.6.4 console runner. The cloud environment install script (`tools/ci/setup-cloud-environment.sh`) installs .NET SDK 8, Mono, `en-KE` locale data, and the NUnit runner.

On **Windows** (local or CI), use:

```bash
dotnet test tests/AssetManagement.Tests/AssetManagement.Tests.csproj -c Release
```

Full solution build and web UI verification happen on **Windows CI** (`.github/workflows/build.yml`).

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

# Database operational scripts

These scripts are **intentionally separated** from app startup. They do not run when you start the web app unless you explicitly invoke them or enable `AutoInitializeDatabase` in Debug builds.

SQL content lives in `database/scripts/`. These PowerShell entry points apply that content.

## Scripts

| Script | Destructive? | When to use |
|--------|--------------|-------------|
| **`Initialize-Database.ps1`** | No DROP | Fresh DB or re-apply schema + migrations + seed + indexes. Creates DB if missing. |
| **`Invoke-Migrations.ps1`** | No | Apply only `004_Migrations` with `SchemaMigrationHistory` (production-safe incremental path). |
| **`Unlock-Logins.ps1`** | No | Clear lockouts; repair demo admin accounts (runs SQL 030/031). |
| **`Reset-E2eDatabase.ps1`** | **YES — DROP DATABASE** | **E2E / CI only.** Disposable `*_E2E` catalog. |
| **`Restore-E2eWebConfig.ps1`** | No | Undo Web.config changes from E2E reset. |
| **`Verify-Categories.ps1`** | No | Dev check: re-init and print demo category counts. |

## Recommended actions

### Day-to-day dev (keep your data)

```powershell
# Apply new migrations only
.\tools\database\Invoke-Migrations.ps1 -Targets @("YOUR_SERVER|AssetManagementModuleDb")

# Bootstrap school org (NIS) — does not drop DB
.\src\AssetManagement.Runner\bin\Release\net40\AssetManagement.Runner.exe school-org --slug nis --name NIS
```

Use a **non-`_E2E`** database name (e.g. `AssetManagementModuleDb`) for persistent local work.

### Production / staging deploy

1. **`AutoInitializeDatabase=false`** (Release publish).
2. Run **`Initialize-Database.ps1`** once on a new server, **or** **`Invoke-Migrations.ps1`** on existing DBs.
3. **Never** run **`Reset-E2eDatabase.ps1`** against production.
4. Deploy to IIS using standard Release publish — no local IIS setup scripts are required.

### E2E / Playwright (disposable database)

```powershell
# Explicit destructive reset (required flags)
.\tools\database\Reset-E2eDatabase.ps1 -ConfirmDestructive

# Or CI:
$env:ALLOW_E2E_DB_RESET = 'true'
.\tools\database\Reset-E2eDatabase.ps1
```

Playwright `global-setup.ts` passes `-ConfirmDestructive`.  
`e2e/scripts/start-app.ps1` **does not** reset the DB unless you pass **`-ResetDatabase`**.

### Locked out of demo accounts

```powershell
.\tools\database\Unlock-Logins.ps1
```

## Destructive script guards (`Reset-E2eDatabase.ps1`)

Blocked unless **all** of the following are true:

1. Database name ends with **`_E2E`**
2. You pass **`-ConfirmDestructive`** **or** set **`ALLOW_E2E_DB_RESET=true`**

This prevents accidental wipe of `AssetManagementModuleDb` or production catalogs.

## App startup (by design)

| Mechanism | Role |
|-----------|------|
| Visual Studio / IIS Express F5 | Local dev — optional `AutoInitializeDatabase=true` in Debug |
| App `DatabaseConfig.Configure()` | Optional full init when `AutoInitializeDatabase=true` (Debug only) |
| Release publish to IIS | Production — run scripts in this folder explicitly; no auto-init on startup |

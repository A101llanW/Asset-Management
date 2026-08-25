# SQL Server Configuration Guidance

## Recommended Local Setup

- SQL Server Express (`.\\SQLEXPRESS`) recommended
- LocalDB (`(localdb)\\MSSQLLocalDB`) optional for lightweight local-only development
- Authentication: Windows Integrated (for local dev)
- Ensure user has create DB rights

## Connection String

Default (`src/AssetManagement.Web/Web.config`):

```xml
<add name="AssetManagementConnection"
     connectionString="Data Source=.\\SQLEXPRESS;Initial Catalog=AssetManagementModuleDb;Integrated Security=True;MultipleActiveResultSets=True"
     providerName="System.Data.SqlClient" />
```

## Creating/Updating Database

The ASP.NET web app applies SQL scripts from `database/scripts/` on startup when `AutoInitializeDatabase=true` (Debug only). For explicit control, use scripts in **`tools/database/`** (see [`tools/database/README.md`](../tools/database/README.md)). Schema and seed scripts are idempotent (`IF NOT EXISTS` / `IF OBJECT_ID IS NULL`).

### Automatic (Debug builds only)

On startup, the web app runs all scripts when `AutoInitializeDatabase` is `true` (`Web.Debug.config`). **Release/production keeps this `false`.** Restart IIS Express / the app pool after pulling schema changes.

### Manual (recommended for production and persistent dev DBs)

From the repository root:

```powershell
.\tools\database\Invoke-Migrations.ps1
```

This applies `database/scripts/004_Migrations` via the ASP.NET `SqlDatabaseInitializer` (tracks `SchemaMigrationHistory`). Optional targets:

```powershell
.\tools\database\Invoke-Migrations.ps1 -Targets @("localhost\SQLEXPRESS|AssetManagementModuleDb")
```

Full schema + seed (new database; **does not DROP** existing DB):

```powershell
.\tools\database\Initialize-Database.ps1
```

### E2E only (DESTRUCTIVE — DROP DATABASE)

```powershell
.\tools\database\Reset-E2eDatabase.ps1 -ConfirmDestructive
```

Only allowed for databases named `*_E2E`. Never run against production.

## Recommended SQL Indexes

Already modeled in EF:
- Unique `AssetTag`
- Unique `Permission.Code`
- Unique `Department.Code`
- Composite unique `RolePermission(RoleId, PermissionId)`

Migration hook includes filtered unique index for serial number when not null:
- `IX_Asset_SerialNumber_NotNull`

## Backup and Restore

- Take daily full backups for production.
- Include file storage root for uploaded documents in backup policy.

## Performance Baseline Tips

- Add nonclustered indexes for common report filters:
  - `Asset(CurrentStatus, DepartmentId, CategoryId)`
  - `AuditLog(Timestamp, Action, EntityType)`
  - `AssetCustodyEvent(AssetId, ActionDate)`
- Use read-only report replicas for large deployments.

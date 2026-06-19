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

The ASP.NET web app applies SQL scripts from `database/scripts/` on startup (or via `initialize-database.ps1`). Schema and seed scripts are idempotent (`IF NOT EXISTS` / `IF OBJECT_ID IS NULL`).

### Automatic (recommended for local dev)

On startup, the web app runs all scripts when `AutoInitializeDatabase` is `true` in `Web.config` (default). Restart IIS Express / the app pool after pulling schema changes.

### Manual

From the repository root:

```powershell
.\initialize-database.ps1
```

Optional parameters:

```powershell
.\initialize-database.ps1 -ServerInstance ".\SQLEXPRESS" -Database "AssetManagementModuleDb"
```

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

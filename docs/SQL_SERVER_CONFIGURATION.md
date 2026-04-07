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

Using Visual Studio Package Manager Console:

1. Set startup project: `AssetManagement.Web`
2. Set default project: `AssetManagement.Infrastructure`
3. Run:

```powershell
Update-Database
```

If no migration exists in your environment, scaffold one:

```powershell
Add-Migration InitialCreate
Update-Database
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

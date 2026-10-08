# roleperm-org-fix-2026-10-08

31 RolePermission rows were tagged OrganizationId = 1 (Nanosoft) but belong to roles of other orgs:

| role org | roles | rows |
|---|---|---|
| 2 Demo Organization B | 4 Facilities Manager, 5 Procurement Manager | 13 (from 060_SchoolRolesAndPermissions) |
| 9 template 1 | 24 Asset Manager, 26 Finance Officer, 29 Auditor | 9 (from 002_Phase3 permissions) |
| 10 asset-import-template | 33 Asset Manager, 35 Finance Officer, 38 Auditor | 9 (from 002_Phase3 permissions) |

Platform Admin (role 1, Roles.OrganizationId NULL, 9 rows tagged org 1) is intentionally left alone.

Cause: 002_Phase3.sql and 060_SchoolRolesAndPermissions.sql inserted RolePermission without an
OrganizationId; the 005/006 catch-all (`UPDATE ... SET OrganizationId = @defaultOrgId WHERE OrganizationId IS NULL`)
then stamped org 1. Both migrations now take OrganizationId from the role (JOIN Roles) and skip
existing (RoleId, PermissionId) pairs, which covers (OrganizationId, RoleId, PermissionId) because
of the unique index IX_RolePermission_Role_Permission and one org per role.

## Run order (sqlcmd -S .\SQLEXPRESS -E -d AssetManagementModuleDb -W -b -i <file>)
1. `01_backup.sql`: creates dbo.Bak_RolePermOrg_20261008 (31 rows) and asserts 31. Done 2026-10-08 20:56 EAT.
   CSV export: `Bak_RolePermOrg_20261008.csv`.
2. `02_retag.sql -v COMMIT=0`: dry run, rolls back (output: `02_retag.dryrun.live.out.txt`).
   `02_retag.sql -v COMMIT=1`: apply. Asserts 31 candidates, updated + skipped = 31, 0 duplicates, 0 mismatches left.
3. `03_check_roleperm.sql`: read-only check, expect `tenant_role_mismatches = 0`, `duplicate_triples = 0`.
4. Rollback: `99_rollback.sql -v COMMIT=0` and then `-v COMMIT=1` restores OrganizationId from the backup by Id.

A full COPY_ONLY backup taken before any change is at
`C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS\MSSQL\Backup\RolePerm_preretag_20261008.bak`.

Copies of roles or permissions between orgs must go through the service mapping
(OrganizationService.EnsureTenantRoles), never raw id copies.

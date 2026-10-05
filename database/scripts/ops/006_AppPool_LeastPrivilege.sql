/*
================================================================================
DO-NOT-RUN-UNTIL-APPROVED
Phase B prepare-only (#6) — least-privilege app DB principal
SoT path: database/scripts/ops/006_AppPool_LeastPrivilege.sql
Target DB: AssetManagementModuleDb on .\SQLEXPRESS (or your instance)

Does NOT drop or alter existing elevated logins (db_owner stays until Allan
approves a cutover). Does NOT delete data. Idempotent-safe (IF NOT EXISTS).

Recommended model:
  - App runtime: IIS APPPOOL\AssetManagement → db_datareader + db_datawriter + EXECUTE
  - Migrations / deploy: separate Windows or SQL login with db_ddladmin (or db_owner)
    so the app pool never needs DDL.

After Allan approves:
  1. Run this script (creates login/user/roles if missing).
  2. Keep existing db_owner membership until a soak test passes.
  3. Then separately REMOVE db_owner from the app pool user (not in this script).
================================================================================
*/

USE [master];
GO

DECLARE @login SYSNAME = N'IIS APPPOOL\AssetManagement';

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @login)
BEGIN
    -- Brackets required for names with backslash / spaces.
    DECLARE @sql NVARCHAR(500) = N'CREATE LOGIN ' + QUOTENAME(@login) + N' FROM WINDOWS;';
    EXEC (@sql);
    PRINT N'Created login ' + @login;
END
ELSE
BEGIN
    PRINT N'Login already exists: ' + @login;
END
GO

USE [AssetManagementModuleDb];
GO

DECLARE @login SYSNAME = N'IIS APPPOOL\AssetManagement';
DECLARE @user SYSNAME = N'IIS APPPOOL\AssetManagement';

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @user)
BEGIN
    DECLARE @sql NVARCHAR(500) = N'CREATE USER ' + QUOTENAME(@user) + N' FOR LOGIN ' + QUOTENAME(@login) + N';';
    EXEC (@sql);
    PRINT N'Created database user ' + @user;
END
ELSE
BEGIN
    PRINT N'Database user already exists: ' + @user;
END
GO

-- Role memberships (idempotent)
IF IS_ROLEMEMBER(N'db_datareader', N'IIS APPPOOL\AssetManagement') = 0
    ALTER ROLE [db_datareader] ADD MEMBER [IIS APPPOOL\AssetManagement];
IF IS_ROLEMEMBER(N'db_datawriter', N'IIS APPPOOL\AssetManagement') = 0
    ALTER ROLE [db_datawriter] ADD MEMBER [IIS APPPOOL\AssetManagement];
GO

-- EXECUTE on all existing procedures / functions in dbo (and grant for future via default)
GRANT EXECUTE ON SCHEMA::[dbo] TO [IIS APPPOOL\AssetManagement];
GO

/*
DDL NOTE (migrations):
  In-app SqlDatabaseInitializer.ApplyMigrations and AssetManagement.Runner migrate
  run ALTER TABLE / CREATE INDEX. Those need db_ddladmin (or db_owner).

  RECOMMENDATION: do NOT grant db_ddladmin to the app pool.
  Use a separate deploy login for migrations, for example:

    -- (separate script / operator action — not applied by this file)
    -- CREATE LOGIN [DESKTOP-V47PGAM\allan] ... already sysadmin locally
    -- Or: CREATE LOGIN [AssetManagement_Deploy] ...
    -- USE AssetManagementModuleDb; CREATE USER ...; ALTER ROLE db_ddladmin ADD MEMBER ...

  If you must run migrations under the app identity temporarily:
    -- ALTER ROLE [db_ddladmin] ADD MEMBER [IIS APPPOOL\AssetManagement];
  Remove that after deploy.

NO sysadmin is granted by this script.
*/
PRINT N'006_AppPool_LeastPrivilege: reader/writer/EXECUTE ensured. Review DDL note before removing db_owner.';
GO

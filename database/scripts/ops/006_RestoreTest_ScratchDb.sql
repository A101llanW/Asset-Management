/*
================================================================================
DO-NOT-RUN-UNTIL-APPROVED
Phase B prepare-only (#6) — restore-test to a SCRATCH database only
SoT: database/scripts/ops/006_RestoreTest_ScratchDb.sql

NEVER restore over AssetManagementModuleDb with this script.
Scratch DB name: AssetManagementModuleDb_RestoreTest

Steps:
  1. Set @bakFile below to a real .bak from the nightly backup.
  2. sqlcmd -S .\SQLEXPRESS -E -i "...\006_RestoreTest_ScratchDb.sql"
  3. Spot-check row counts, then: DROP DATABASE [AssetManagementModuleDb_RestoreTest];
================================================================================
*/

USE [master];
GO

-- EDIT ME: path to a real backup file
DECLARE @bakFile NVARCHAR(500) = N'C:\Backup\AssetManagement\EDIT_ME.bak';
DECLARE @scratch SYSNAME = N'AssetManagementModuleDb_RestoreTest';

IF @bakFile LIKE N'%EDIT_ME%'
BEGIN
    RAISERROR(N'Edit @bakFile to a real backup path before running.', 16, 1);
END
ELSE
BEGIN
    IF DB_ID(N'AssetManagementModuleDb_RestoreTest') IS NOT NULL
    BEGIN
        ALTER DATABASE [AssetManagementModuleDb_RestoreTest] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
        DROP DATABASE [AssetManagementModuleDb_RestoreTest];
    END

    RESTORE DATABASE [AssetManagementModuleDb_RestoreTest]
    FROM DISK = @bakFile
    WITH
        MOVE N'AssetManagementModuleDb' TO N'C:\Backup\AssetManagement\AssetManagementModuleDb_RestoreTest.mdf',
        MOVE N'AssetManagementModuleDb_log' TO N'C:\Backup\AssetManagement\AssetManagementModuleDb_RestoreTest_log.ldf',
        REPLACE, RECOVERY, STATS = 10;
    -- If MOVE logical names fail, run: RESTORE FILELISTONLY FROM DISK = '...bak';
    -- and adjust the MOVE logical names accordingly.

    PRINT N'Restore-test complete into AssetManagementModuleDb_RestoreTest.';
END
GO

-- Optional smoke check (runs only if scratch exists)
IF DB_ID(N'AssetManagementModuleDb_RestoreTest') IS NOT NULL
BEGIN
    EXEC(N'USE [AssetManagementModuleDb_RestoreTest]; SELECT DB_NAME() AS DbName, COUNT(*) AS TableCount FROM sys.tables;');
END
GO

/*
================================================================================
DO-NOT-RUN-UNTIL-APPROVED
Phase B prepare-only (#6) — SQL Express nightly backup (no SQL Agent)
SoT path: database/scripts/ops/006_Backup_AssetManagementModuleDb.sql

Usage (sqlcmd), example:
  sqlcmd -S .\SQLEXPRESS -E -i "C:\Users\allan\Documents\Examples\CodexAsset\database\scripts\ops\006_Backup_AssetManagementModuleDb.sql"

Windows Task Scheduler (suggested):
  Program: sqlcmd.exe
  Arguments: -S .\SQLEXPRESS -E -i "C:\Users\allan\Documents\Examples\CodexAsset\database\scripts\ops\006_Backup_AssetManagementModuleDb.sql"
  Start in: C:\Backup\AssetManagement   (create folder first)
  Trigger: Daily 02:00 local (EAT)
  Run as: account that can write the backup folder and has BACKUP rights on the DB
================================================================================
*/

DECLARE @backupDir NVARCHAR(400) = N'C:\Backup\AssetManagement';
DECLARE @db SYSNAME = N'AssetManagementModuleDb';
DECLARE @stamp NVARCHAR(20) = CONVERT(NVARCHAR(8), GETDATE(), 112) + N'_' + REPLACE(CONVERT(NVARCHAR(8), GETDATE(), 108), N':', N'');
DECLARE @file NVARCHAR(500) = @backupDir + N'\' + @db + N'_' + @stamp + N'.bak';
DECLARE @sql NVARCHAR(1000);

-- Ensure directory exists via xp_create_subdir when available (SQL Express).
BEGIN TRY
    EXEC master.dbo.xp_create_subdir @backupDir;
END TRY
BEGIN CATCH
    PRINT N'Could not ensure backup directory (create C:\Backup\AssetManagement manually if needed): ' + ERROR_MESSAGE();
END CATCH

SET @sql = N'BACKUP DATABASE ' + QUOTENAME(@db) + N' TO DISK = N''' + @file + N''' WITH INIT, COMPRESSION, CHECKSUM, STATS = 10;';
-- COMPRESSION may be unsupported on some Express SKUs — fallback without it.
BEGIN TRY
    EXEC (@sql);
    PRINT N'Backup OK: ' + @file;
END TRY
BEGIN CATCH
    PRINT N'Backup with COMPRESSION failed, retrying without: ' + ERROR_MESSAGE();
    SET @sql = N'BACKUP DATABASE ' + QUOTENAME(@db) + N' TO DISK = N''' + @file + N''' WITH INIT, CHECKSUM, STATS = 10;';
    EXEC (@sql);
    PRINT N'Backup OK (no compression): ' + @file;
END CATCH
GO

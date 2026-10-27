-- 00  COPY_ONLY backup of the live database BEFORE anything else.  LIVE_RUN_BY_ALLAN.
-- Read-only for the database (a COPY_ONLY backup does not break the normal backup chain).
-- BEFORE RUNNING: replace the placeholder <BACKUP_FOLDER> with the folder the SQL Server service can write to
-- (the same folder used on 21 Oct for AssetManagementModuleDb_pre_tagseg_20261021.bak), and check the date part.
-- Suggested file name (same pattern as 21 Oct):  AssetManagementModuleDb_pre_sections_20261027.bak
-- The script refuses to run while a placeholder (<...>) is still in the path.
-- Expected result: "BACKUP DATABASE successfully processed ..." then "The backup set on file 1 is valid."
-- If it fails: STOP. Do not run 01+ until a verified backup exists.

DECLARE @file NVARCHAR(400) = N'<BACKUP_FOLDER>\AssetManagementModuleDb_pre_sections_20261027.bak';

IF CHARINDEX(N'<', @file) > 0 OR CHARINDEX(N'>', @file) > 0
BEGIN
    RAISERROR(N'00 NOT RUN: replace <BACKUP_FOLDER> in @file first.', 16, 1);
    RETURN;
END

IF DB_NAME() NOT LIKE N'AssetManagementModuleDb%'
BEGIN
    RAISERROR(N'00 NOT RUN: connect to the AssetManagementModuleDb database first (current: %s).', 16, 1, @@SERVERNAME);
    RETURN;
END

DECLARE @db SYSNAME = DB_NAME();
BACKUP DATABASE @db TO DISK = @file WITH COPY_ONLY, CHECKSUM, INIT, STATS = 10,
    NAME = N'AssetManagementModuleDb COPY_ONLY before 105-107 (sections, heads, print log)';
RESTORE VERIFYONLY FROM DISK = @file WITH CHECKSUM;
SELECT @db AS [database], @file AS backup_file, SYSUTCDATETIME() AS finished_utc;
GO

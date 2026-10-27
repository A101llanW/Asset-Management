-- 00  COPY_ONLY backup of the live database BEFORE anything else.  LIVE_RUN_BY_ALLAN.
-- A COPY_ONLY backup does not change the database or break the normal backup chain.
-- Run in SSMS or sqlcmd only (not the web Sys/Database console).
-- BEFORE RUNNING: replace the placeholder <BACKUP_FOLDER> with the folder the SQL Server service can write to
-- (the same folder used on 21 Oct for AssetManagementModuleDb_pre_tagseg_20261021.bak).
-- Review S2: the file name gets a timestamp (AssetManagementModuleDb_pre_sections_yyyyMMdd_HHmmss.bak, server time)
-- and the script REFUSES if that file already exists. It never overwrites (no INIT; NOINIT as a last safety net).
-- Scripts 02-05 refuse to run unless a COPY_ONLY backup like this one finished in the last 2 hours.
-- Expected result: "BACKUP DATABASE successfully processed ..." then "The backup set on file 1 is valid."
-- If it fails: STOP. Do not run 01+ until a verified backup exists.
SET NOCOUNT ON;
DECLARE @folder NVARCHAR(300) = N'<BACKUP_FOLDER>';

IF CHARINDEX(N'<', @folder) > 0 OR CHARINDEX(N'>', @folder) > 0
BEGIN
    RAISERROR(N'00 NOT RUN: replace <BACKUP_FOLDER> in @folder first.', 16, 1);
    RETURN;
END

IF DB_NAME() NOT LIKE N'AssetManagementModuleDb%'
BEGIN
    DECLARE @current SYSNAME = DB_NAME();
    RAISERROR(N'00 NOT RUN: connect to the AssetManagementModuleDb database first (current database: %s).', 16, 1, @current);
    RETURN;
END

IF RIGHT(@folder, 1) = N'\' SET @folder = LEFT(@folder, LEN(@folder) - 1);
DECLARE @stamp NVARCHAR(20) = REPLACE(REPLACE(REPLACE(CONVERT(NVARCHAR(19), GETDATE(), 120), N'-', N''), N':', N''), N' ', N'_');
DECLARE @file NVARCHAR(400) = @folder + N'\AssetManagementModuleDb_pre_sections_' + @stamp + N'.bak';

-- Refuse if the file exists: (a) msdb already knows a backup written to this exact path, (b) the file is on disk
-- (xp_fileexist; skipped if the login may not call it - the timestamp + NOINIT still prevent any overwrite).
IF EXISTS (SELECT 1 FROM msdb.dbo.backupmediafamily WHERE [physical_device_name] = @file)
BEGIN
    RAISERROR(N'00 NOT RUN: a backup file with this name already exists (msdb): %s', 16, 1, @file);
    RETURN;
END
DECLARE @exists INT = 0;
BEGIN TRY
    DECLARE @fe TABLE ([file_exists] INT, [is_dir] INT, [parent_exists] INT);
    INSERT INTO @fe EXEC master.dbo.xp_fileexist @file;
    SELECT @exists = [file_exists] FROM @fe;
END TRY
BEGIN CATCH
    PRINT N'00: xp_fileexist not available to this login; relying on the timestamped name and NOINIT.';
END CATCH
IF @exists = 1
BEGIN
    RAISERROR(N'00 NOT RUN: the file already exists: %s', 16, 1, @file);
    RETURN;
END

DECLARE @db SYSNAME = DB_NAME();
BACKUP DATABASE @db TO DISK = @file WITH COPY_ONLY, CHECKSUM, NOINIT, NOFORMAT, STATS = 10,
    NAME = N'AssetManagementModuleDb COPY_ONLY before 105-107 (sections, heads, print log)';
RESTORE VERIFYONLY FROM DISK = @file WITH CHECKSUM;
SELECT @db AS [database], @file AS backup_file, GETDATE() AS finished_server_time;
GO

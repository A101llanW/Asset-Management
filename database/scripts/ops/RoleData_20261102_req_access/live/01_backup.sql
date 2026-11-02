-- 01_backup  COPY_ONLY full backup WITH CHECKSUM before the 2026-11-02 requisition access role data.  LIVE.
-- Paste as ONE batch in the web Sys/Database console (or run in SSMS). Changes nothing in the database.
-- Writes to the instance default backup folder; file AssetManagementModuleDb_pre_roledata_yyyyMMdd_HHmmss.bak.
-- Never overwrites (NOINIT, refuses an existing name).
-- Expected: one row  RESULT = BACKUP_OK  with the file name. Anything else: STOP, run nothing else.
SET NOCOUNT ON;
DECLARE @result NVARCHAR(60) = N'FAIL';
DECLARE @detail NVARCHAR(400) = N'';
DECLARE @folder NVARCHAR(300) = CONVERT(NVARCHAR(300), SERVERPROPERTY('InstanceDefaultBackupPath'));
IF @folder IS NULL
BEGIN
    BEGIN TRY
        EXEC master.dbo.xp_instance_regread N'HKEY_LOCAL_MACHINE', N'Software\Microsoft\MSSQLServer\MSSQLServer', N'BackupDirectory', @folder OUTPUT;
    END TRY
    BEGIN CATCH
        SET @folder = NULL;
    END CATCH
END
DECLARE @file NVARCHAR(400) = NULL;
IF DB_NAME() NOT LIKE N'AssetManagementModuleDb%'
    SET @detail = N'FAIL_WRONG_DATABASE: ' + DB_NAME();
ELSE IF @folder IS NULL OR LEN(@folder) = 0
    SET @detail = N'FAIL_NO_DEFAULT_BACKUP_FOLDER';
ELSE
BEGIN
    IF RIGHT(@folder, 1) = N'\' SET @folder = LEFT(@folder, LEN(@folder) - 1);
    DECLARE @stamp NVARCHAR(20) = REPLACE(REPLACE(REPLACE(CONVERT(NVARCHAR(19), GETDATE(), 120), N'-', N''), N':', N''), N' ', N'_');
    SET @file = @folder + N'\' + DB_NAME() + N'_pre_roledata_' + @stamp + N'.bak';
    IF EXISTS (SELECT 1 FROM msdb.dbo.backupmediafamily WHERE [physical_device_name] = @file)
        SET @detail = N'FAIL_FILE_EXISTS: ' + @file;
    ELSE
    BEGIN
        BEGIN TRY
            DECLARE @db SYSNAME = DB_NAME();
            BACKUP DATABASE @db TO DISK = @file WITH COPY_ONLY, CHECKSUM, NOINIT, NOFORMAT,
                NAME = N'AssetManagementModuleDb COPY_ONLY before 2026-11-02 requisition role data';
            RESTORE VERIFYONLY FROM DISK = @file WITH CHECKSUM;
            SET @result = N'BACKUP_OK';
            SET @detail = @file;
        END TRY
        BEGIN CATCH
            SET @detail = N'FAIL_BACKUP: ' + ERROR_MESSAGE();
        END CATCH
    END
END
SELECT @result AS [RESULT], @detail AS backup_file, GETDATE() AS server_time;

-- 109 ROLLBACK - PREPARED, DO NOT RUN. Not part of the migration set; the migration runner never picks this folder up.
-- Purpose: undo 109_HelpVideo.sql. ONLY after a build without the hidden help video page (/{tenant}/HelpVideos) is
--   live again; the current build reads dbo.HelpVideo and the page errors if the table disappears first.
-- Effect: the help video list (titles, topics, uploader, upload time) is LOST. The video files under
--   ~/App_Data/HelpVideos are NOT deleted (remove the folder by hand if wanted). AuditLog rows stay.
--   Take a COPY_ONLY backup first.
-- To run on purpose: sqlcmd -E -S <server> -i this_file.sql -v DbName="<database>" ConfirmRollback="YES"
:on error exit
-- DbName and ConfirmRollback are NOT :setvar'd on purpose (a :setvar would override -v). If either is missing sqlcmd stops
-- with "scripting variable not defined". SSMS (SQLCMD Mode): add  :setvar DbName "..."  and  :setvar ConfirmRollback "YES"  above.
IF N'$(ConfirmRollback)' <> N'YES'
BEGIN
    RAISERROR (N'109 rollback NOT run: ConfirmRollback is not YES. This script is DO NOT RUN unless you mean it.', 16, 1);
    SET NOEXEC ON;
END
GO
USE [$(DbName)];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
-- Guard repeated INSIDE the drop batch: in SSMS without SQLCMD mode the batches above fail (":on error exit", USE [$(DbName)])
-- but this batch would still run in whatever database is current. Unsubstituted variables stay literal "$(...)",
-- never equal YES / DB_NAME(), so the batch stops here. N'$' + N'(' avoids sqlcmd treating the check itself as a variable.
IF LEFT(N'$(DbName)', 2) = N'$' + N'(' OR LEFT(N'$(ConfirmRollback)', 2) = N'$' + N'('
BEGIN
    RAISERROR (N'109 rollback NOT run: sqlcmd variables DbName / ConfirmRollback were not substituted (use sqlcmd -v or SSMS SQLCMD Mode).', 16, 1);
    RETURN;
END
IF N'$(ConfirmRollback)' <> N'YES' OR DB_NAME() <> N'$(DbName)'
BEGIN
    RAISERROR (N'109 rollback NOT run: ConfirmRollback is not YES or the current database is not $(DbName).', 16, 1);
    RETURN;
END
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.HelpVideo', N'U') IS NOT NULL
    DROP TABLE [dbo].[HelpVideo];
DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'109_HelpVideo.sql';
COMMIT TRANSACTION;
PRINT N'109 rollback done: dbo.HelpVideo dropped, SchemaMigrationHistory row removed. App_Data/HelpVideos files left on disk.';
GO

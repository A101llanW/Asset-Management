-- 110 ROLLBACK - PREPARED, DO NOT RUN. Not part of the migration set; the migration runner never picks this folder up.
-- Purpose: undo 110_requisition_target_room_or_person.sql. ONLY after the previous build (without the requisition
--   target room / person columns) is live again; the current build reads these columns and breaks requisitions,
--   purchase orders and receiving if they disappear first.
-- Effect: which requisitions were for a person, the person, the placement (room / company custody) and the person's
--   room are LOST (PurchaseRequest.DepartmentId and everything already received are untouched; assets keep their
--   department / custodian). Take a COPY_ONLY backup first.
-- To run on purpose: sqlcmd -E -S <server> -i this_file.sql -v DbName="<database>" ConfirmRollback="YES"
:on error exit
-- DbName and ConfirmRollback are NOT :setvar'd on purpose (a :setvar would override -v). If either is missing sqlcmd stops
-- with "scripting variable not defined". SSMS (SQLCMD Mode): add  :setvar DbName "..."  and  :setvar ConfirmRollback "YES"  above.
IF N'$(ConfirmRollback)' <> N'YES'
BEGIN
    RAISERROR (N'110 rollback NOT run: ConfirmRollback is not YES. This script is DO NOT RUN unless you mean it.', 16, 1);
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
    RAISERROR (N'110 rollback NOT run: sqlcmd variables DbName / ConfirmRollback were not substituted (use sqlcmd -v or SSMS SQLCMD Mode).', 16, 1);
    RETURN;
END
IF N'$(ConfirmRollback)' <> N'YES' OR DB_NAME() <> N'$(DbName)'
BEGIN
    RAISERROR (N'110 rollback NOT run: ConfirmRollback is not YES or the current database is not $(DbName).', 16, 1);
    RETURN;
END
BEGIN TRANSACTION;
IF COL_LENGTH(N'dbo.PurchaseRequest', N'TargetDepartmentId') IS NOT NULL
    ALTER TABLE [dbo].[PurchaseRequest] DROP COLUMN [TargetDepartmentId];
IF COL_LENGTH(N'dbo.PurchaseRequest', N'TargetPlacement') IS NOT NULL
    ALTER TABLE [dbo].[PurchaseRequest] DROP COLUMN [TargetPlacement];
IF COL_LENGTH(N'dbo.PurchaseRequest', N'TargetUserId') IS NOT NULL
    ALTER TABLE [dbo].[PurchaseRequest] DROP COLUMN [TargetUserId];
IF COL_LENGTH(N'dbo.PurchaseRequest', N'RequisitionTargetKind') IS NOT NULL
    ALTER TABLE [dbo].[PurchaseRequest] DROP COLUMN [RequisitionTargetKind];
IF COL_LENGTH(N'dbo.AssetSubType', N'CreatedVia') IS NOT NULL
    ALTER TABLE [dbo].[AssetSubType] DROP COLUMN [CreatedVia];
IF COL_LENGTH(N'dbo.AssetSubType', N'CreatedById') IS NOT NULL
    ALTER TABLE [dbo].[AssetSubType] DROP COLUMN [CreatedById];
IF COL_LENGTH(N'dbo.AssetType', N'CreatedVia') IS NOT NULL
    ALTER TABLE [dbo].[AssetType] DROP COLUMN [CreatedVia];
IF COL_LENGTH(N'dbo.AssetType', N'CreatedById') IS NOT NULL
    ALTER TABLE [dbo].[AssetType] DROP COLUMN [CreatedById];
IF COL_LENGTH(N'dbo.AssetCategory', N'CreatedVia') IS NOT NULL
    ALTER TABLE [dbo].[AssetCategory] DROP COLUMN [CreatedVia];
IF COL_LENGTH(N'dbo.AssetCategory', N'CreatedById') IS NOT NULL
    ALTER TABLE [dbo].[AssetCategory] DROP COLUMN [CreatedById];
DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'110_requisition_target_room_or_person.sql';
COMMIT TRANSACTION;
PRINT N'110 rollback done.';
GO
SET NOEXEC OFF;

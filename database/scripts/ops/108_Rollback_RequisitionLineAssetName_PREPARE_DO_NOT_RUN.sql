-- 108 ROLLBACK - PREPARED, DO NOT RUN. Not part of the migration set; the migration runner never picks this folder up.
-- Purpose: undo 108_requisition_line_asset_name.sql. ONLY after the previous build (without the ItemName /
--   per-line TargetAssetId columns) is live again; the current build reads these columns and breaks requisitions,
--   purchase orders and receiving if they disappear first.
-- Effect: requisition / PO line item names and per-line asset tags are LOST (header PurchaseRequest.TargetAssetId
--   is untouched). Take a COPY_ONLY backup first.
-- To run on purpose: sqlcmd -E -S <server> -i this_file.sql -v DbName="<database>" ConfirmRollback="YES"
:on error exit
-- DbName and ConfirmRollback are NOT :setvar'd on purpose (a :setvar would override -v). If either is missing sqlcmd stops
-- with "scripting variable not defined". SSMS (SQLCMD Mode): add  :setvar DbName "..."  and  :setvar ConfirmRollback "YES"  above.
IF N'$(ConfirmRollback)' <> N'YES'
BEGIN
    RAISERROR (N'108 rollback NOT run: ConfirmRollback is not YES. This script is DO NOT RUN unless you mean it.', 16, 1);
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
    RAISERROR (N'108 rollback NOT run: sqlcmd variables DbName / ConfirmRollback were not substituted (use sqlcmd -v or SSMS SQLCMD Mode).', 16, 1);
    RETURN;
END
IF N'$(ConfirmRollback)' <> N'YES' OR DB_NAME() <> N'$(DbName)'
BEGIN
    RAISERROR (N'108 rollback NOT run: ConfirmRollback is not YES or the current database is not $(DbName).', 16, 1);
    RETURN;
END
BEGIN TRANSACTION;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'dbo.PurchaseRequestLine') AND [name] = N'IX_PurchaseRequestLine_TargetAssetId')
    DROP INDEX [IX_PurchaseRequestLine_TargetAssetId] ON [dbo].[PurchaseRequestLine];
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [parent_object_id] = OBJECT_ID(N'dbo.PurchaseRequestLine') AND [name] = N'FK_PurchaseRequestLine_TargetAsset')
    ALTER TABLE [dbo].[PurchaseRequestLine] DROP CONSTRAINT [FK_PurchaseRequestLine_TargetAsset];
IF COL_LENGTH(N'dbo.PurchaseRequestLine', N'TargetAssetId') IS NOT NULL
    ALTER TABLE [dbo].[PurchaseRequestLine] DROP COLUMN [TargetAssetId];
IF COL_LENGTH(N'dbo.PurchaseRequestLine', N'ItemName') IS NOT NULL
    ALTER TABLE [dbo].[PurchaseRequestLine] DROP COLUMN [ItemName];
IF COL_LENGTH(N'dbo.PurchaseOrderLine', N'ItemName') IS NOT NULL
    ALTER TABLE [dbo].[PurchaseOrderLine] DROP COLUMN [ItemName];
DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'108_requisition_line_asset_name.sql';
COMMIT TRANSACTION;
PRINT N'108 rollback done.';
GO
SET NOEXEC OFF;

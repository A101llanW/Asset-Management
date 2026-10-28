-- 02  Migration 110_requisition_target_room_or_person.  LIVE_RUN_BY_ALLAN.
-- Source: database/scripts/004_Migrations/110_requisition_target_room_or_person.sql (same statements), plus the guard below.
-- Guard: right database AND a COPY_ONLY full backup of THIS database (script 00) finished in the last 2 hours
-- (msdb.dbo.backupset, taken after the database was created/restored). Otherwise nothing runs.
-- Run in SSMS or sqlcmd only, NOT the web Sys/Database console: the guard relies on GO batches and SET NOEXEC.
-- Expected result: 'Commands completed successfully.'  If it fails: the transaction rolls back by itself; STOP and see README.
SET NOEXEC OFF;
GO
DECLARE @guardMsg NVARCHAR(2000) = NULL;
IF DB_NAME() NOT LIKE N'AssetManagementModuleDb%'
    SET @guardMsg = N'NOT RUN: connect to the AssetManagementModuleDb database first (current database: ' + DB_NAME() + N').';
ELSE IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'108_requisition_line_asset_name.sql')
    SET @guardMsg = N'NOT RUN: migration 108 is not applied on ' + DB_NAME() + N'. 110 needs 108.';
ELSE
BEGIN
    BEGIN TRY
        IF NOT EXISTS (SELECT 1 FROM msdb.dbo.backupset b
                       WHERE b.[database_name] = DB_NAME() AND b.[type] = 'D' AND b.[is_copy_only] = 1
                         AND b.[backup_finish_date] >= DATEADD(HOUR, -2, GETDATE())
                         AND b.[backup_start_date] >= (SELECT d.[create_date] FROM sys.databases d WHERE d.[name] = DB_NAME()))
            SET @guardMsg = N'NOT RUN: no COPY_ONLY full backup of ' + DB_NAME() + N' finished in the last 2 hours (msdb.dbo.backupset). Run 00 first.';
    END TRY
    BEGIN CATCH
        SET @guardMsg = N'NOT RUN: cannot read msdb.dbo.backupset to confirm the 00 backup: ' + ERROR_MESSAGE();
    END CATCH
END
IF @guardMsg IS NOT NULL
BEGIN
    RAISERROR(N'%s', 16, 1, @guardMsg);
    SET NOEXEC ON;
END
GO
-- 110 (requisition target: room or person): records WHO / WHERE a requisition is for, so approval (Details) and
-- receiving can place the received assets in a room, or in company custody held by a person.
-- What it does (additive and idempotent; every new column is NULLable; no existing row is changed; no FK, no index):
--   1. PurchaseRequest.RequisitionTargetKind NVARCHAR(16) NULL   'Room' | 'Person'; NULL = saved before 110 (legacy:
--      the department is the target, behaviour unchanged).
--   2. PurchaseRequest.TargetUserId NVARCHAR(128) NULL           Person target: AspNetUsers.Id of the person (like
--      AssetReceiving.ReceivedById; no FK, same as the other user-id columns on this table).
--   3. PurchaseRequest.TargetPlacement NVARCHAR(24) NULL         Person target: 'PersonRoom' (assets recorded in the
--      person's room / class) | 'CompanyCustody' (no department; the person becomes the custodian at receiving).
--   4. PurchaseRequest.TargetDepartmentId INT NULL               Person target + PersonRoom: the person's room / class
--      (snapshot id, no FK on purpose: it records history and must not block deleting or purging a room).
--   5. AssetCategory / AssetType / AssetSubType .CreatedById NVARCHAR(128) NULL and .CreatedVia NVARCHAR(24) NULL
--      (6 columns): who created a category / type / sub-type inline from the requisition form or the receiving screen
--      ('Requisition' | 'Receiving'); CreatedAt already exists. NULL = created before 110 or on the classification
--      screens. Every inline create also writes an AuditLog row (AssetCategories/AssetTypes/AssetSubTypes.CreateInline).
--   6. SchemaMigrationHistory row.
-- PurchaseRequest.DepartmentId stays the approval / scope department in every mode (for a person target: the
-- requester's locked department, else the person's room / profile department that receives requisitions).
-- Must run BEFORE the matching code is published (the entity maps read the new columns). Needs 108.
-- Idempotent: every step is guarded; re-running is a no-op.
-- Rollback (see database/scripts/ops/110_Rollback_RequisitionTargetRoomOrPerson_PREPARE_DO_NOT_RUN.sql):
--   remove the columns PurchaseRequest.RequisitionTargetKind, TargetUserId, TargetPlacement, TargetDepartmentId and
--   AssetCategory / AssetType / AssetSubType .CreatedById, .CreatedVia
--   (only after redeploying the previous build);
--   DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'110_requisition_target_room_or_person.sql';
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.PurchaseRequest', N'RequisitionTargetKind') IS NULL
BEGIN
    ALTER TABLE [dbo].[PurchaseRequest] ADD [RequisitionTargetKind] NVARCHAR(16) NULL;
END

IF COL_LENGTH(N'dbo.PurchaseRequest', N'TargetUserId') IS NULL
BEGIN
    ALTER TABLE [dbo].[PurchaseRequest] ADD [TargetUserId] NVARCHAR(128) NULL;
END

IF COL_LENGTH(N'dbo.PurchaseRequest', N'TargetPlacement') IS NULL
BEGIN
    ALTER TABLE [dbo].[PurchaseRequest] ADD [TargetPlacement] NVARCHAR(24) NULL;
END

IF COL_LENGTH(N'dbo.PurchaseRequest', N'TargetDepartmentId') IS NULL
BEGIN
    ALTER TABLE [dbo].[PurchaseRequest] ADD [TargetDepartmentId] INT NULL;
END

IF COL_LENGTH(N'dbo.AssetCategory', N'CreatedById') IS NULL
BEGIN
    ALTER TABLE [dbo].[AssetCategory] ADD [CreatedById] NVARCHAR(128) NULL;
END

IF COL_LENGTH(N'dbo.AssetCategory', N'CreatedVia') IS NULL
BEGIN
    ALTER TABLE [dbo].[AssetCategory] ADD [CreatedVia] NVARCHAR(24) NULL;
END

IF COL_LENGTH(N'dbo.AssetType', N'CreatedById') IS NULL
BEGIN
    ALTER TABLE [dbo].[AssetType] ADD [CreatedById] NVARCHAR(128) NULL;
END

IF COL_LENGTH(N'dbo.AssetType', N'CreatedVia') IS NULL
BEGIN
    ALTER TABLE [dbo].[AssetType] ADD [CreatedVia] NVARCHAR(24) NULL;
END

IF COL_LENGTH(N'dbo.AssetSubType', N'CreatedById') IS NULL
BEGIN
    ALTER TABLE [dbo].[AssetSubType] ADD [CreatedById] NVARCHAR(128) NULL;
END

IF COL_LENGTH(N'dbo.AssetSubType', N'CreatedVia') IS NULL
BEGIN
    ALTER TABLE [dbo].[AssetSubType] ADD [CreatedVia] NVARCHAR(24) NULL;
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'110_requisition_target_room_or_person.sql')
BEGIN
    INSERT INTO [dbo].[SchemaMigrationHistory] ([ScriptName], [Checksum], [AppliedAt], [AppliedBy])
    VALUES (N'110_requisition_target_room_or_person.sql', NULL, SYSUTCDATETIME(), SUSER_SNAME());
END

COMMIT TRANSACTION;
GO
SET NOEXEC OFF;
GO

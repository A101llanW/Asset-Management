-- 097: AssetRequest delivery target (additive only; no existing data changed).
-- TargetKind: 0 = a user (the requester / a chosen user, today's behaviour), 1 = a room/location (Class or Room
-- department in TargetDepartmentId). On fulfilment a room target assigns to the room the way Assignments does
-- (person in charge = class teacher / room custodian, or department pool).
-- Every existing request stays TargetKind 0 / TargetDepartmentId NULL, so current behaviour is unchanged.
-- The app tolerates these columns being absent (room targets are hidden/refused), so code and script order is free.
-- Rollback (room targets are lost; every request becomes a user target again):
--   IF OBJECT_ID(N'[dbo].[FK_AssetRequest_TargetDepartment]', N'F') IS NOT NULL ALTER TABLE [dbo].[AssetRequest] DROP CONSTRAINT [FK_AssetRequest_TargetDepartment];
--   IF OBJECT_ID(N'[dbo].[CK_AssetRequest_TargetKind]', N'C') IS NOT NULL ALTER TABLE [dbo].[AssetRequest] DROP CONSTRAINT [CK_AssetRequest_TargetKind];
--   IF OBJECT_ID(N'[dbo].[DF_AssetRequest_TargetKind]', N'D') IS NOT NULL ALTER TABLE [dbo].[AssetRequest] DROP CONSTRAINT [DF_AssetRequest_TargetKind];
--   IF COL_LENGTH(N'dbo.AssetRequest', N'TargetDepartmentId') IS NOT NULL ALTER TABLE [dbo].[AssetRequest] DROP COLUMN [TargetDepartmentId];
--   IF COL_LENGTH(N'dbo.AssetRequest', N'TargetKind') IS NOT NULL ALTER TABLE [dbo].[AssetRequest] DROP COLUMN [TargetKind];
--   DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'097_AssetRequestTarget.sql';

IF COL_LENGTH(N'dbo.AssetRequest', N'TargetKind') IS NULL
BEGIN
    ALTER TABLE [dbo].[AssetRequest] ADD [TargetKind] INT NOT NULL
        CONSTRAINT [DF_AssetRequest_TargetKind] DEFAULT (0);
END
GO

IF COL_LENGTH(N'dbo.AssetRequest', N'TargetDepartmentId') IS NULL
BEGIN
    ALTER TABLE [dbo].[AssetRequest] ADD [TargetDepartmentId] INT NULL;
END
GO

IF OBJECT_ID(N'[dbo].[CK_AssetRequest_TargetKind]', N'C') IS NULL
BEGIN
    ALTER TABLE [dbo].[AssetRequest] WITH CHECK
        ADD CONSTRAINT [CK_AssetRequest_TargetKind] CHECK ([TargetKind] IN (0, 1));
END
GO

IF OBJECT_ID(N'[dbo].[FK_AssetRequest_TargetDepartment]', N'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[AssetRequest] WITH CHECK
        ADD CONSTRAINT [FK_AssetRequest_TargetDepartment] FOREIGN KEY ([TargetDepartmentId]) REFERENCES [dbo].[Department]([Id]);
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'097_AssetRequestTarget.sql')
BEGIN
    INSERT INTO [dbo].[SchemaMigrationHistory] ([ScriptName], [Checksum], [AppliedAt], [AppliedBy])
    VALUES (N'097_AssetRequestTarget.sql', NULL, SYSUTCDATETIME(), SUSER_SNAME());
END
GO

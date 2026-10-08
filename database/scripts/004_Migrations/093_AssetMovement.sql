-- 093: AssetMovement (additive only: new table, indexes and one permission; no existing data changed).
-- One row per change of an asset's department and/or custodian (Edit, bulk Move to department,
-- Move to class/room, Transfer, Assign, Return, Import, Other). OccurredAtUtc is UTC; screens show
-- it in the organization time zone (Africa/Nairobi, EAT).
-- The app tolerates this table being absent (writes are skipped, screens say "not available yet"),
-- so the code may be deployed before or after this script.
-- Rollback (only if no movement history needs to be kept):
--   DELETE rp FROM [RolePermission] rp JOIN [Permission] p ON p.[Id] = rp.[PermissionId] WHERE p.[Code] = N'Assets.Movements.View';
--   DELETE FROM [Permission] WHERE [Code] = N'Assets.Movements.View';
--   DROP TABLE [dbo].[AssetMovement];
--   DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'093_AssetMovement.sql';

IF OBJECT_ID(N'[dbo].[AssetMovement]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AssetMovement] (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_AssetMovement] PRIMARY KEY CLUSTERED,
        [OrganizationId] INT NOT NULL,
        [AssetId] INT NOT NULL,
        [FromDepartmentId] INT NULL,
        [ToDepartmentId] INT NULL,
        [FromCustodianUserId] NVARCHAR(128) NULL,
        [ToCustodianUserId] NVARCHAR(128) NULL,
        [MovementType] INT NOT NULL,
        [BatchId] NVARCHAR(64) NULL,
        [PerformedByUserId] NVARCHAR(128) NULL,
        [OccurredAtUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_AssetMovement_OccurredAtUtc] DEFAULT (SYSUTCDATETIME()),
        [Notes] NVARCHAR(500) NULL,
        [SourceRef] NVARCHAR(100) NULL,
        CONSTRAINT [FK_AssetMovement_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization]([Id]),
        CONSTRAINT [FK_AssetMovement_Asset] FOREIGN KEY ([AssetId]) REFERENCES [dbo].[Asset]([Id]),
        CONSTRAINT [FK_AssetMovement_FromDepartment] FOREIGN KEY ([FromDepartmentId]) REFERENCES [dbo].[Department]([Id]),
        CONSTRAINT [FK_AssetMovement_ToDepartment] FOREIGN KEY ([ToDepartmentId]) REFERENCES [dbo].[Department]([Id]),
        CONSTRAINT [CK_AssetMovement_MovementType] CHECK ([MovementType] BETWEEN 1 AND 9)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[AssetMovement]') AND name = N'IX_AssetMovement_Org_Occurred')
    CREATE NONCLUSTERED INDEX [IX_AssetMovement_Org_Occurred]
        ON [dbo].[AssetMovement] ([OrganizationId], [OccurredAtUtc] DESC, [Id] DESC)
        INCLUDE ([AssetId], [MovementType], [FromDepartmentId], [ToDepartmentId], [BatchId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[AssetMovement]') AND name = N'IX_AssetMovement_Asset')
    CREATE NONCLUSTERED INDEX [IX_AssetMovement_Asset]
        ON [dbo].[AssetMovement] ([AssetId], [OccurredAtUtc] DESC, [Id] DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[AssetMovement]') AND name = N'IX_AssetMovement_Org_Batch')
    CREATE NONCLUSTERED INDEX [IX_AssetMovement_Org_Batch]
        ON [dbo].[AssetMovement] ([OrganizationId], [BatchId]);
GO

DECLARE @now DATETIME = GETUTCDATE();
IF NOT EXISTS (SELECT 1 FROM [Permission] WHERE [Code] = N'Assets.Movements.View')
BEGIN
    INSERT INTO [Permission] ([Name],[Code],[Module],[Description],[CreatedAt],[IsActive])
    VALUES (N'View Asset Movements', N'Assets.Movements.View', N'Assets', N'Can view the asset movement history (department and custodian changes) and export it', @now, 1);
END
GO

-- Grant to every role that can already work with custody (assign / transfer) or read audit logs.
DECLARE @permMovements INT = (SELECT TOP 1 [Id] FROM [Permission] WHERE [Code] = N'Assets.Movements.View' ORDER BY [Id]);
IF @permMovements IS NOT NULL
BEGIN
    INSERT INTO [RolePermission] ([RoleId], [PermissionId], [OrganizationId])
    SELECT r.[Id], @permMovements, r.[OrganizationId]
    FROM [Roles] r
    WHERE (r.[Name] = N'Company Admin'
           OR EXISTS (SELECT 1 FROM [RolePermission] rp0
                      JOIN [Permission] p0 ON p0.[Id] = rp0.[PermissionId]
                      WHERE rp0.[RoleId] = r.[Id]
                        AND p0.[Code] IN (N'Assets.Assign', N'Assets.Transfer', N'AuditLogs.View')))
      AND NOT EXISTS (
          SELECT 1 FROM [RolePermission] rp
          WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = @permMovements
            AND ((rp.[OrganizationId] IS NULL AND r.[OrganizationId] IS NULL) OR rp.[OrganizationId] = r.[OrganizationId]));
END
GO

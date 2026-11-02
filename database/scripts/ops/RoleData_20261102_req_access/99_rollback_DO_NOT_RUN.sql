-- 99  ROLLBACK of 01/02 (2026-11-02 requisition access role data).  PREPARED, DO NOT RUN.
-- Run only if Allan decides to undo 01 or 02, with his individual approval. Never via an agent.
-- Removes ONLY the Finance Officer grants that 01/02 add (Purchases.Edit, Assets.ViewAll, Users.View) for the
-- organisation you set below. The Procurement Purchases.Edit grants are NOT removed: they existed before 01/02
-- (01/02 were a no-op for them on alLAN's copy). If you know 01/02 added them on a database, add them by hand.
-- Set @OrgId/@OrgName to 13/'Test-WF' or 21/'Nairobi International School'. @Confirm must be 1 to write.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @Confirm BIT = 0;
DECLARE @OrgId INT = NULL;                 -- 13 or 21
DECLARE @OrgName NVARCHAR(200) = NULL;     -- N'Test-WF' or N'Nairobi International School'

IF DB_NAME() NOT LIKE N'AssetManagementModuleDb%'
BEGIN
    RAISERROR(N'99 NOT RUN: connect to the AssetManagementModuleDb database first.', 16, 1);
    RETURN;
END
IF @OrgId IS NULL OR NOT EXISTS (SELECT 1 FROM [Organization] WHERE [Id] = @OrgId AND [Name] = @OrgName)
BEGIN
    RAISERROR(N'99 NOT RUN: set @OrgId and @OrgName to a matching organisation first.', 16, 1);
    RETURN;
END

DECLARE @Remove TABLE ([RolePermissionId] INT);
INSERT INTO @Remove
SELECT rp.[Id]
FROM [RolePermission] rp
JOIN [Roles] r ON r.[Id] = rp.[RoleId] AND r.[OrganizationId] = @OrgId AND r.[Name] = N'Finance Officer'
JOIN [Permission] p ON p.[Id] = rp.[PermissionId] AND p.[Code] IN (N'Purchases.Edit', N'Assets.ViewAll', N'Users.View');

SELECT rp.[Id], r.[Name] AS [role], p.[Code], N'to remove' AS [action]
FROM [RolePermission] rp
JOIN @Remove x ON x.[RolePermissionId] = rp.[Id]
JOIN [Roles] r ON r.[Id] = rp.[RoleId]
JOIN [Permission] p ON p.[Id] = rp.[PermissionId];

IF @Confirm <> 1
BEGIN
    PRINT N'99: @Confirm = 0, nothing deleted (report only).';
    RETURN;
END

BEGIN TRANSACTION;
    DELETE rp FROM [RolePermission] rp JOIN @Remove x ON x.[RolePermissionId] = rp.[Id];
    DECLARE @removed INT = @@ROWCOUNT;
COMMIT TRANSACTION;
PRINT N'99: removed ' + CAST(@removed AS NVARCHAR(10)) + N' Finance Officer grant(s).';
GO

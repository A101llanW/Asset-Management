-- 99  ROLLBACK of 01/02/03 (2026-11-02 requisition access role data).  PREPARED, DO NOT RUN.
-- Run only if Allan decides to undo, with his individual approval.
-- Removes ONLY the Finance Officer grants that 01/02 add (Purchases.Edit, Assets.ViewAll, Users.View) for the
-- organisation you set below. Not removed: the Procurement Purchases.Edit grants and the Purchases.Create grants of
-- Department Head / Procurement Officer / Facilities Manager / Company Admin, which existed before 01/02 (no-op on
-- alLAN's copy). If you know 01/02 added one of them on a database, remove it by hand.
-- For Test-WF (13) it also undoes 03: depthead / procofficer go back to no department, but only while they are still
-- on the department 03 set (Academics 1456 / the single Procurement department). @Confirm must be 1 to write.
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

DECLARE @Reset TABLE ([UserId] NVARCHAR(128));
IF @OrgId = 13
BEGIN
    INSERT INTO @Reset
    SELECT u.[Id] FROM [Users] u
    WHERE u.[OrganizationId] = 13
      AND ((u.[Email] = N'depthead.a46138179@asset.local' AND u.[DepartmentId] = 1456)
        OR (u.[Email] = N'procofficer.a46138179@asset.local' AND u.[DepartmentId] IN
              (SELECT d.[Id] FROM [Department] d WHERE d.[OrganizationId] = 13 AND d.[Name] LIKE N'Procurement%')));
END

SELECT rp.[Id], r.[Name] AS [role], p.[Code], N'grant to remove' AS [action]
FROM [RolePermission] rp
JOIN @Remove x ON x.[RolePermissionId] = rp.[Id]
JOIN [Roles] r ON r.[Id] = rp.[RoleId]
JOIN [Permission] p ON p.[Id] = rp.[PermissionId];
SELECT u.[Email], u.[DepartmentId], N'department to clear' AS [action]
FROM [Users] u JOIN @Reset x ON x.[UserId] = u.[Id];

IF @Confirm <> 1
BEGIN
    PRINT N'99: @Confirm = 0, nothing changed (report only).';
    RETURN;
END

BEGIN TRANSACTION;
    DELETE rp FROM [RolePermission] rp JOIN @Remove x ON x.[RolePermissionId] = rp.[Id];
    DECLARE @removed INT = @@ROWCOUNT;
    UPDATE u SET [DepartmentId] = NULL, [UpdatedAt] = GETDATE() FROM [Users] u JOIN @Reset x ON x.[UserId] = u.[Id];
    DECLARE @cleared INT = @@ROWCOUNT;
COMMIT TRANSACTION;
PRINT N'99: removed ' + CAST(@removed AS NVARCHAR(10)) + N' Finance Officer grant(s); cleared ' + CAST(@cleared AS NVARCHAR(10)) + N' department(s).';
GO

-- 99  ROLLBACK of 01/02/03 (2026-11-02 requisition access role data).  PREPARED, DO NOT RUN.
-- Run only if Allan decides to undo, with his individual approval.
-- Removes ONLY the Finance Officer grants that 01/02 add (Purchases.Edit, Assets.ViewAll, Users.View) for the
-- organisation you set below. Not removed: the Procurement Purchases.Edit grants and the Purchases.Create grants of
-- Department Head / Procurement Officer / Facilities Manager / Company Admin, which existed before 01/02 (no-op on
-- alLAN's copy). If you know 01/02 added one of them on a database, remove it by hand.
-- For Test-WF (13) it also undoes 03:
--   * depthead / procofficer go back to no department, but only while they are still on the department 03 set
--     (Academics 1456 / the Procurement department);
--   * the Procurement department is DELETED only if it is the one 03 created (org 13, Name 'Procurement',
--     Code 'PROCUREMENT', with a script-written 'Departments.Create' audit row, ActorUserId NULL) AND, after the two
--     users are cleared, nothing references it (no user, sub-department, room/asset, request, role, invitation,
--     movement, transfer, approval stage). Otherwise it is left in place and reported IN_USE (deactivate by hand).
--     A 'Departments.Delete' audit row is written when it is deleted.
-- @Confirm must be 1 to write.
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

-- The Procurement department 03 created (Test-WF only).
DECLARE @ProcDeptId INT = NULL;
IF @OrgId = 13
BEGIN
    SET @ProcDeptId = (SELECT TOP 1 d.[Id] FROM [Department] d
        WHERE d.[OrganizationId] = 13 AND d.[Name] = N'Procurement' AND d.[Code] = N'PROCUREMENT'
          AND EXISTS (SELECT 1 FROM [AuditLog] a WHERE a.[Action] = N'Departments.Create' AND a.[EntityType] = N'Department'
                      AND a.[EntityId] = CONVERT(NVARCHAR(20), d.[Id]) AND a.[ActorUserId] IS NULL AND a.[OrganizationId] = 13));
END

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

-- References to the Procurement department other than the two users being cleared: every foreign key to Department,
-- plus Users.DepartmentId (no foreign key) for anyone else.
DECLARE @refs INT = 0;
IF @ProcDeptId IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql = @sql + N' + (SELECT COUNT(*) FROM ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + N'.'
        + QUOTENAME(OBJECT_NAME(fk.parent_object_id)) + N' WHERE ' + QUOTENAME(COL_NAME(fkc.parent_object_id, fkc.parent_column_id))
        + N' = @id)'
    FROM sys.foreign_keys fk
    JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
    WHERE fk.referenced_object_id = OBJECT_ID(N'dbo.Department');
    SET @sql = N'SELECT @n = 0' + @sql + N';';
    EXEC sp_executesql @sql, N'@id INT, @n INT OUTPUT', @id = @ProcDeptId, @n = @refs OUTPUT;
    SET @refs = @refs + (SELECT COUNT(*) FROM [Users] u WHERE u.[DepartmentId] = @ProcDeptId
                                                     AND u.[Id] NOT IN (SELECT [UserId] FROM @Reset));
END

SELECT rp.[Id], r.[Name] AS [role], p.[Code], N'grant to remove' AS [action]
FROM [RolePermission] rp
JOIN @Remove x ON x.[RolePermissionId] = rp.[Id]
JOIN [Roles] r ON r.[Id] = rp.[RoleId]
JOIN [Permission] p ON p.[Id] = rp.[PermissionId];
SELECT u.[Email], u.[DepartmentId], N'department to clear' AS [action]
FROM [Users] u JOIN @Reset x ON x.[UserId] = u.[Id];
SELECT @ProcDeptId AS procurement_department_id, @refs AS other_references,
    CASE WHEN @ProcDeptId IS NULL THEN N'none created by 03 (nothing to delete)'
         WHEN @refs = 0 THEN N'department to delete'
         ELSE N'IN_USE: left in place (deactivate by hand)' END AS [action];

IF @Confirm <> 1
BEGIN
    PRINT N'99: @Confirm = 0, nothing changed (report only).';
    RETURN;
END

DECLARE @deptDeleted INT = 0, @nowUtc DATETIME = GETUTCDATE();
BEGIN TRANSACTION;
    DELETE rp FROM [RolePermission] rp JOIN @Remove x ON x.[RolePermissionId] = rp.[Id];
    DECLARE @removed INT = @@ROWCOUNT;
    UPDATE u SET [DepartmentId] = NULL, [UpdatedAt] = GETDATE() FROM [Users] u JOIN @Reset x ON x.[UserId] = u.[Id];
    DECLARE @cleared INT = @@ROWCOUNT;
    IF @ProcDeptId IS NOT NULL AND @refs = 0
       AND NOT EXISTS (SELECT 1 FROM [Users] WHERE [DepartmentId] = @ProcDeptId)
    BEGIN
        DELETE FROM [Department] WHERE [Id] = @ProcDeptId AND [OrganizationId] = 13 AND [Code] = N'PROCUREMENT';
        SET @deptDeleted = @@ROWCOUNT;
        IF @deptDeleted = 1
            INSERT INTO [AuditLog] ([ActorUserId], [Action], [EntityType], [EntityId], [OldValues], [NewValues],
                                    [Timestamp], [IPAddress], [CreatedAt], [IsActive], [OrganizationId])
            VALUES (NULL, N'Departments.Delete', N'Department', CONVERT(NVARCHAR(20), @ProcDeptId), N'Procurement', NULL,
                    @nowUtc, NULL, @nowUtc, 1, 13);
    END
COMMIT TRANSACTION;
PRINT N'99: removed ' + CAST(@removed AS NVARCHAR(10)) + N' Finance Officer grant(s); cleared ' + CAST(@cleared AS NVARCHAR(10))
    + N' department(s); deleted ' + CAST(@deptDeleted AS NVARCHAR(10)) + N' Procurement department.';
GO

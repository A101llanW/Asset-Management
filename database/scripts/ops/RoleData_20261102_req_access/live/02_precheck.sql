-- 02_precheck  READ-ONLY.  2026-11-02 requisition access role data.  LIVE.
-- Paste as ONE batch. Only SELECTs. Set @OrgName below to the organisation on this database (one variable).
-- Expected: one row  RESULT = READY  with to_add = N (pass N to 03 as @ExpectedToAdd), or ALREADY_APPLIED (to_add = 0:
-- run 04 only). FAIL_* = STOP: an organisation, role or permission is missing / not unique, or no backup from 01.
SET NOCOUNT ON;
DECLARE @OrgName NVARCHAR(200) = N'Nairobi International School';  -- the ONE variable: the organisation on this database
DECLARE @OrgCount INT = (SELECT COUNT(*) FROM [dbo].[Organization] WHERE [Name] = @OrgName);
DECLARE @OrgId INT = CASE WHEN @OrgCount = 1 THEN (SELECT [Id] FROM [dbo].[Organization] WHERE [Name] = @OrgName) END;
DECLARE @Wanted TABLE ([RoleName] NVARCHAR(100) NOT NULL, [Code] NVARCHAR(100) NOT NULL);
INSERT INTO @Wanted ([RoleName], [Code]) VALUES
    (N'Finance Officer', N'Purchases.Edit'),
    (N'Finance Officer', N'Assets.ViewAll'),
    (N'Finance Officer', N'Users.View'),
    (N'Procurement Manager', N'Purchases.Edit'),
    (N'Procurement Officer', N'Purchases.Edit'),
    (N'Department Head', N'Purchases.Create'),
    (N'Procurement Officer', N'Purchases.Create'),
    (N'Facilities Manager', N'Purchases.Create'),
    (N'Company Admin', N'Purchases.Create');
DECLARE @backupOk BIT = 0;
BEGIN TRY
    IF EXISTS (SELECT 1 FROM msdb.dbo.backupset b
               WHERE b.[database_name] = DB_NAME() AND b.[type] = 'D' AND b.[is_copy_only] = 1
                 AND b.[backup_finish_date] >= DATEADD(HOUR, -2, GETDATE()))
        SET @backupOk = 1;
END TRY
BEGIN CATCH
    SET @backupOk = 0;
END CATCH
DECLARE @badRows INT = (SELECT COUNT(*) FROM @Wanted w
    WHERE (SELECT COUNT(*) FROM [dbo].[Roles] r WHERE r.[OrganizationId] = @OrgId AND r.[Name] = w.[RoleName] AND r.[IsActive] = 1) <> 1
       OR (SELECT COUNT(*) FROM [dbo].[Permission] p WHERE p.[Code] = w.[Code] AND p.[IsActive] = 1) <> 1);
DECLARE @toAdd INT = (SELECT COUNT(*) FROM @Wanted w
    JOIN [dbo].[Roles] r ON r.[OrganizationId] = @OrgId AND r.[Name] = w.[RoleName] AND r.[IsActive] = 1
    JOIN [dbo].[Permission] p ON p.[Code] = w.[Code] AND p.[IsActive] = 1
    WHERE NOT EXISTS (SELECT 1 FROM [dbo].[RolePermission] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = p.[Id]));
SELECT CASE
    WHEN DB_NAME() NOT LIKE N'AssetManagementModuleDb%' THEN N'FAIL_WRONG_DATABASE'
    WHEN @OrgId IS NULL THEN N'FAIL_ORGANISATION_NOT_FOUND_OR_NOT_UNIQUE'
    WHEN @badRows > 0 THEN N'FAIL_ROLE_OR_PERMISSION_MISSING'
    WHEN @toAdd = 0 THEN N'ALREADY_APPLIED'
    WHEN @backupOk = 0 THEN N'FAIL_NO_BACKUP_LAST_2_HOURS'
    ELSE N'READY' END AS [RESULT],
    @OrgId AS organisation_id, @toAdd AS to_add, @badRows AS missing_role_or_permission_rows,
    (SELECT STUFF((SELECT N'; ' + w.[RoleName] + N' + ' + w.[Code] FROM @Wanted w
        JOIN [dbo].[Roles] r ON r.[OrganizationId] = @OrgId AND r.[Name] = w.[RoleName] AND r.[IsActive] = 1
        JOIN [dbo].[Permission] p ON p.[Code] = w.[Code] AND p.[IsActive] = 1
        WHERE NOT EXISTS (SELECT 1 FROM [dbo].[RolePermission] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = p.[Id])
        ORDER BY w.[RoleName], w.[Code] FOR XML PATH('')), 1, 2, N'')) AS grants_to_add;

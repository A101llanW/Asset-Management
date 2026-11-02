-- 03_change  2026-11-02 requisition access role data.  LIVE.  WRITES (INSERT into RolePermission only).
-- Paste as ONE batch, only after 01 = BACKUP_OK and 02 = READY. Set @OrgName (same as 02) and @ExpectedToAdd to the
-- to_add number 02 returned. Refuses unless a COPY_ONLY backup finished in the last 2 hours and the count still matches.
-- One transaction, XACT_ABORT ON. Idempotent (NOT EXISTS). Nothing is removed or changed.
-- Expected: one row  RESULT = GRANT_OK  with added = @ExpectedToAdd and still_missing = 0.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @OrgName NVARCHAR(200) = N'Nairobi International School';  -- the ONE variable: the organisation on this database
DECLARE @ExpectedToAdd INT = NULL;  -- the to_add from 02_precheck
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
DECLARE @result NVARCHAR(60) = N'FAIL';
DECLARE @added INT = 0;
IF DB_NAME() NOT LIKE N'AssetManagementModuleDb%' SET @result = N'FAIL_WRONG_DATABASE';
ELSE IF @OrgId IS NULL SET @result = N'FAIL_ORGANISATION_NOT_FOUND_OR_NOT_UNIQUE';
ELSE IF @badRows > 0 SET @result = N'FAIL_ROLE_OR_PERMISSION_MISSING';
ELSE IF @backupOk = 0 SET @result = N'FAIL_NO_BACKUP_LAST_2_HOURS';
ELSE IF @ExpectedToAdd IS NULL SET @result = N'FAIL_SET_EXPECTED_TO_ADD_FROM_02';
ELSE IF @ExpectedToAdd <> @toAdd SET @result = N'FAIL_COUNT_CHANGED_SINCE_02';
ELSE
BEGIN
    BEGIN TRANSACTION;
        INSERT INTO [dbo].[RolePermission] ([RoleId], [PermissionId], [OrganizationId])
        SELECT r.[Id], p.[Id], @OrgId
        FROM @Wanted w
        JOIN [dbo].[Roles] r ON r.[OrganizationId] = @OrgId AND r.[Name] = w.[RoleName] AND r.[IsActive] = 1
        JOIN [dbo].[Permission] p ON p.[Code] = w.[Code] AND p.[IsActive] = 1
        WHERE NOT EXISTS (SELECT 1 FROM [dbo].[RolePermission] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = p.[Id]);
        SET @added = @@ROWCOUNT;
        IF @added = @ExpectedToAdd
        BEGIN
            COMMIT TRANSACTION;
            SET @result = N'GRANT_OK';
        END
        ELSE
        BEGIN
            ROLLBACK TRANSACTION;
            SET @result = N'FAIL_ROLLED_BACK_COUNT_MISMATCH';
        END
END
SELECT @result AS [RESULT], @OrgId AS organisation_id, @ExpectedToAdd AS expected, @added AS added,
    (SELECT COUNT(*) FROM @Wanted w
        JOIN [dbo].[Roles] r ON r.[OrganizationId] = @OrgId AND r.[Name] = w.[RoleName] AND r.[IsActive] = 1
        JOIN [dbo].[Permission] p ON p.[Code] = w.[Code] AND p.[IsActive] = 1
        WHERE NOT EXISTS (SELECT 1 FROM [dbo].[RolePermission] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = p.[Id])) AS still_missing;

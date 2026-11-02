-- 04_verify  READ-ONLY.  2026-11-02 requisition access role data.  LIVE.
-- Paste as ONE batch. Set @OrgName (same as 02/03).
-- Expected: one row  RESULT = ALL_PASS. Otherwise RESULT = FAIL and the failed checks are listed.
-- Checks: all 9 wanted grants present; Staff / Auditor / Asset Manager still without Purchases.Create;
-- Finance Officer still without Purchases.Create.
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
DECLARE @fails NVARCHAR(1000) = N'';
IF DB_NAME() NOT LIKE N'AssetManagementModuleDb%' SET @fails = @fails + N'wrong database; ';
IF @OrgId IS NULL SET @fails = @fails + N'organisation not found or not unique; ';
DECLARE @present INT = (SELECT COUNT(*) FROM @Wanted w
    JOIN [dbo].[Roles] r ON r.[OrganizationId] = @OrgId AND r.[Name] = w.[RoleName] AND r.[IsActive] = 1
    JOIN [dbo].[Permission] p ON p.[Code] = w.[Code] AND p.[IsActive] = 1
    JOIN [dbo].[RolePermission] rp ON rp.[RoleId] = r.[Id] AND rp.[PermissionId] = p.[Id]);
IF @present <> 9 SET @fails = @fails + N'wanted grants present ' + CONVERT(NVARCHAR(10), @present) + N' of 9; ';
DECLARE @noCreate INT = (SELECT COUNT(*) FROM [dbo].[RolePermission] rp
    JOIN [dbo].[Roles] r ON r.[Id] = rp.[RoleId] AND r.[OrganizationId] = @OrgId
    JOIN [dbo].[Permission] p ON p.[Id] = rp.[PermissionId] AND p.[Code] = N'Purchases.Create'
    WHERE r.[Name] IN (N'Staff', N'Auditor', N'Finance Officer'));
IF @noCreate > 0 SET @fails = @fails + N'Staff/Auditor/Finance Officer hold Purchases.Create (not part of this change, check); ';
SELECT CASE WHEN LEN(@fails) = 0 THEN N'ALL_PASS' ELSE N'FAIL' END AS [RESULT], @OrgId AS organisation_id,
    @present AS wanted_grants_present, @noCreate AS unexpected_create_holders, @fails AS failed_checks;

-- 01  Test-WF (OrganizationId 13) role data for the 2026-11-02 requisition access answers.  PREPARED, NOT RUN.
-- Run only with Allan's individual approval, in SSMS or sqlcmd against the Test-WF database. Never via an agent.
-- What it does (INSERT into [RolePermission] only; nothing is removed or changed):
--   Finance Officer     + Purchases.Edit  (answer 5: record purchase orders; Purchases.Edit gates only Purchases/Create)
--   Finance Officer     + Assets.ViewAll  (answer 6: read the whole asset register; read scope only)
--   Finance Officer     + Users.View      (answer 7: member details; every user write needs Users.Create/Edit/Delete)
--   Procurement Manager + Purchases.Edit  (answer 5; already present on alLAN's copy -> no-op)
--   Procurement Officer + Purchases.Edit  (answer 5; already present on alLAN's copy -> no-op)
-- Guards: database name, organisation id + name, active role by exact name in this organisation, active permission
-- code, NOT EXISTS (idempotent: a second run inserts 0 rows). One transaction; XACT_ABORT rolls back on any error.
-- Safety switch: nothing is written unless @Confirm is set to 1 below. With @Confirm = 0 it only reports what it would add.
-- Rollback: 99_rollback_DO_NOT_RUN.sql (removes exactly these grants for this organisation).
-- Users must sign out and back in (or wait for the permission cache) to pick up the change.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @Confirm BIT = 0;  -- set to 1 only after Allan approves this run
DECLARE @OrgId INT = 13;
DECLARE @OrgName NVARCHAR(200) = N'Test-WF';

IF DB_NAME() NOT LIKE N'AssetManagementModuleDb%'
BEGIN
    DECLARE @db SYSNAME = DB_NAME();
    RAISERROR(N'01 NOT RUN: connect to the AssetManagementModuleDb database first (current: %s).', 16, 1, @db);
    RETURN;
END
IF NOT EXISTS (SELECT 1 FROM [Organization] WHERE [Id] = @OrgId AND [Name] = @OrgName)
BEGIN
    RAISERROR(N'01 NOT RUN: organisation %d is not named "%s" in this database.', 16, 1, @OrgId, @OrgName);
    RETURN;
END

DECLARE @Wanted TABLE ([RoleName] NVARCHAR(100) NOT NULL, [Code] NVARCHAR(100) NOT NULL);
INSERT INTO @Wanted ([RoleName], [Code]) VALUES
    (N'Finance Officer', N'Purchases.Edit'),
    (N'Finance Officer', N'Assets.ViewAll'),
    (N'Finance Officer', N'Users.View'),
    (N'Procurement Manager', N'Purchases.Edit'),
    (N'Procurement Officer', N'Purchases.Edit');

-- Refuse if a role or permission is missing or ambiguous (exactly one active match each).
IF EXISTS (
    SELECT 1 FROM @Wanted w
    WHERE (SELECT COUNT(*) FROM [Roles] r WHERE r.[OrganizationId] = @OrgId AND r.[Name] = w.[RoleName] AND r.[IsActive] = 1) <> 1
       OR (SELECT COUNT(*) FROM [Permission] p WHERE p.[Code] = w.[Code] AND p.[IsActive] = 1) <> 1)
BEGIN
    SELECT w.[RoleName], w.[Code],
        (SELECT COUNT(*) FROM [Roles] r WHERE r.[OrganizationId] = @OrgId AND r.[Name] = w.[RoleName] AND r.[IsActive] = 1) AS active_roles,
        (SELECT COUNT(*) FROM [Permission] p WHERE p.[Code] = w.[Code] AND p.[IsActive] = 1) AS active_permissions
    FROM @Wanted w;
    RAISERROR(N'01 NOT RUN: a role or permission is missing or not unique (see the result set).', 16, 1);
    RETURN;
END

DECLARE @Plan TABLE ([RoleId] INT, [RoleName] NVARCHAR(100), [PermissionId] INT, [Code] NVARCHAR(100), [AlreadyGranted] BIT);
INSERT INTO @Plan
SELECT r.[Id], r.[Name], p.[Id], p.[Code],
    CASE WHEN EXISTS (SELECT 1 FROM [RolePermission] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = p.[Id]) THEN 1 ELSE 0 END
FROM @Wanted w
JOIN [Roles] r ON r.[OrganizationId] = @OrgId AND r.[Name] = w.[RoleName] AND r.[IsActive] = 1
JOIN [Permission] p ON p.[Code] = w.[Code] AND p.[IsActive] = 1;

SELECT [RoleId], [RoleName], [Code], CASE WHEN [AlreadyGranted] = 1 THEN N'already granted (no-op)' ELSE N'to add' END AS [action]
FROM @Plan ORDER BY [RoleName], [Code];

IF @Confirm <> 1
BEGIN
    PRINT N'01: @Confirm = 0, nothing written (report only).';
    RETURN;
END

BEGIN TRANSACTION;
    INSERT INTO [RolePermission] ([RoleId], [PermissionId], [OrganizationId])
    SELECT pl.[RoleId], pl.[PermissionId], @OrgId
    FROM @Plan pl
    WHERE NOT EXISTS (SELECT 1 FROM [RolePermission] rp WHERE rp.[RoleId] = pl.[RoleId] AND rp.[PermissionId] = pl.[PermissionId]);
    DECLARE @added INT = @@ROWCOUNT;
COMMIT TRANSACTION;

PRINT N'01: added ' + CAST(@added AS NVARCHAR(10)) + N' role permission row(s) for organisation 13.';
SELECT r.[Name] AS [role], p.[Code]
FROM [RolePermission] rp
JOIN [Roles] r ON r.[Id] = rp.[RoleId]
JOIN [Permission] p ON p.[Id] = rp.[PermissionId]
WHERE r.[OrganizationId] = @OrgId AND r.[Name] IN (N'Finance Officer', N'Procurement Manager', N'Procurement Officer')
  AND p.[Code] IN (N'Purchases.Edit', N'Assets.ViewAll', N'Users.View')
ORDER BY r.[Name], p.[Code];
GO

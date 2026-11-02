-- 03  Test-WF (OrganizationId 13) department data for the 2026-11-02 correction 3 (department model).  LOCAL TEST DATA.
-- Replaces optional_assign_department_depthead_procofficer_PREPARE.sql.
-- What it does (UPDATE [Users].[DepartmentId] for two named test users only):
--   depthead.a46138179@asset.local    -> Academics (Id 1456, top-level, active): the department this head heads.
--                                        A head heads one department (Users.DepartmentId is single); a department may
--                                        have several heads (every active Department Head holder in it is an approver).
--   procofficer.a46138179@asset.local -> the Procurement department of organisation 13, IF exactly one active
--                                        department named like "Procurement" exists. alLAN's copy (2026-11-02) has
--                                        NONE: the script reports PROCUREMENT_DEPARTMENT_MISSING and leaves procofficer
--                                        unchanged (no department = organisation-wide; it is never invented here).
-- Guards: database name, organisation id + name, exact user e-mails in org 13 with the expected role, target department
-- id + name + organisation + active, and the user's department must be NULL or already the target (never moves a user
-- who was placed elsewhere). Idempotent: a second run updates 0 rows. @Confirm = 0 (default) only reports.
-- Rollback: 99_rollback_DO_NOT_RUN.sql (sets them back to NULL only while they are still on these departments).
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @Confirm BIT = 0;  -- set to 1 only for the approved run
DECLARE @OrgId INT = 13;
DECLARE @OrgName NVARCHAR(200) = N'Test-WF';
DECLARE @HeadEmail NVARCHAR(256) = N'depthead.a46138179@asset.local';
DECLARE @HeadDeptId INT = 1456;
DECLARE @HeadDeptName NVARCHAR(200) = N'Academics';
DECLARE @ProcEmail NVARCHAR(256) = N'procofficer.a46138179@asset.local';

IF DB_NAME() NOT LIKE N'AssetManagementModuleDb%'
BEGIN
    DECLARE @db SYSNAME = DB_NAME();
    RAISERROR(N'03 NOT RUN: connect to the AssetManagementModuleDb database first (current: %s).', 16, 1, @db);
    RETURN;
END
IF NOT EXISTS (SELECT 1 FROM [Organization] WHERE [Id] = @OrgId AND [Name] = @OrgName)
BEGIN
    RAISERROR(N'03 NOT RUN: organisation %d is not named "%s" in this database.', 16, 1, @OrgId, @OrgName);
    RETURN;
END

DECLARE @HeadUserId NVARCHAR(128) = (SELECT TOP 1 u.[Id] FROM [Users] u JOIN [Roles] r ON r.[Id] = u.[RoleId]
    WHERE u.[Email] = @HeadEmail AND u.[OrganizationId] = @OrgId AND r.[Name] = N'Department Head');
DECLARE @ProcUserId NVARCHAR(128) = (SELECT TOP 1 u.[Id] FROM [Users] u JOIN [Roles] r ON r.[Id] = u.[RoleId]
    WHERE u.[Email] = @ProcEmail AND u.[OrganizationId] = @OrgId AND r.[Name] = N'Procurement Officer');
IF @HeadUserId IS NULL OR @ProcUserId IS NULL
   OR (SELECT COUNT(*) FROM [Users] WHERE [Email] IN (@HeadEmail, @ProcEmail)) <> 2
BEGIN
    RAISERROR(N'03 NOT RUN: the depthead / procofficer test users (with Department Head / Procurement Officer) are not exactly as expected.', 16, 1);
    RETURN;
END
IF NOT EXISTS (SELECT 1 FROM [Department] WHERE [Id] = @HeadDeptId AND [Name] = @HeadDeptName AND [OrganizationId] = @OrgId AND [IsActive] = 1)
BEGIN
    RAISERROR(N'03 NOT RUN: department %d is not the active "%s" of organisation 13.', 16, 1, @HeadDeptId, @HeadDeptName);
    RETURN;
END

DECLARE @ProcDeptCount INT = (SELECT COUNT(*) FROM [Department] WHERE [OrganizationId] = @OrgId AND [IsActive] = 1 AND [Name] LIKE N'Procurement%');
DECLARE @ProcDeptId INT = CASE WHEN @ProcDeptCount = 1
    THEN (SELECT [Id] FROM [Department] WHERE [OrganizationId] = @OrgId AND [IsActive] = 1 AND [Name] LIKE N'Procurement%') END;

DECLARE @HeadCurrent INT = (SELECT [DepartmentId] FROM [Users] WHERE [Id] = @HeadUserId);
DECLARE @ProcCurrent INT = (SELECT [DepartmentId] FROM [Users] WHERE [Id] = @ProcUserId);

SELECT N'depthead' AS [user], @HeadUserId AS user_id, @HeadCurrent AS current_department_id, @HeadDeptId AS target_department_id,
    CASE WHEN @HeadCurrent = @HeadDeptId THEN N'already set (no-op)'
         WHEN @HeadCurrent IS NULL THEN N'to set'
         ELSE N'REFUSED: already in another department' END AS [action]
UNION ALL
SELECT N'procofficer', @ProcUserId, @ProcCurrent, @ProcDeptId,
    CASE WHEN @ProcDeptCount = 0 THEN N'SKIPPED: no Procurement department in organisation 13 (not invented)'
         WHEN @ProcDeptCount > 1 THEN N'SKIPPED: more than one Procurement department (choose by hand)'
         WHEN @ProcCurrent = @ProcDeptId THEN N'already set (no-op)'
         WHEN @ProcCurrent IS NULL THEN N'to set'
         ELSE N'REFUSED: already in another department' END;

IF @HeadCurrent IS NOT NULL AND @HeadCurrent <> @HeadDeptId
BEGIN
    RAISERROR(N'03 NOT RUN: depthead is already in department %d; not moved.', 16, 1, @HeadCurrent);
    RETURN;
END

IF @Confirm <> 1
BEGIN
    PRINT N'03: @Confirm = 0, nothing written (report only).';
    SELECT N'REPORT_ONLY' AS [RESULT], CASE WHEN @ProcDeptCount = 1 THEN N'procurement department found' ELSE N'PROCUREMENT_DEPARTMENT_MISSING' END AS procurement;
    RETURN;
END

DECLARE @headSet INT = 0, @procSet INT = 0;
BEGIN TRANSACTION;
    UPDATE [Users] SET [DepartmentId] = @HeadDeptId, [UpdatedAt] = GETDATE()
    WHERE [Id] = @HeadUserId AND [OrganizationId] = @OrgId AND [DepartmentId] IS NULL;
    SET @headSet = @@ROWCOUNT;
    IF @ProcDeptId IS NOT NULL
    BEGIN
        UPDATE [Users] SET [DepartmentId] = @ProcDeptId, [UpdatedAt] = GETDATE()
        WHERE [Id] = @ProcUserId AND [OrganizationId] = @OrgId AND [DepartmentId] IS NULL;
        SET @procSet = @@ROWCOUNT;
    END
COMMIT TRANSACTION;

SELECT u.[Email], u.[DepartmentId], d.[Name] AS department
FROM [Users] u LEFT JOIN [Department] d ON d.[Id] = u.[DepartmentId]
WHERE u.[Id] IN (@HeadUserId, @ProcUserId);
SELECT N'DEPARTMENTS_OK' AS [RESULT], @headSet AS depthead_set, @procSet AS procofficer_set,
    CASE WHEN @ProcDeptId IS NULL THEN N'PROCUREMENT_DEPARTMENT_MISSING' ELSE CONVERT(NVARCHAR(20), @ProcDeptId) END AS procurement_department;
GO

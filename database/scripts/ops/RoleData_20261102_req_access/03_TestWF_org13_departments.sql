-- 03  Test-WF (OrganizationId 13) department data for the 2026-11-02 correction 3 (department model).  LOCAL TEST DATA.
-- Replaces optional_assign_department_depthead_procofficer_PREPARE.sql.
-- Updated 2026-11-02 15:03 EAT (Allan): create a Procurement department in Test-WF ONLY and put procofficer in it.
-- What it does (organisation 13 only; NIS / any other organisation gets nothing):
--   1. Procurement department: if organisation 13 has NO department named like "Procurement" (active or not) and the
--      code PROCUREMENT is free, it creates one exactly like Departments > Create (normal / administrative) does:
--      Name 'Procurement', Code 'PROCUREMENT', Description 'Procurement department', top level (no parent),
--      DepartmentKind 0 (Administrative), IsRequisitionTarget 1 (the create form default), Section 0, IsActive 1,
--      CreatedAt = UTC now (DepartmentService uses DateTime.UtcNow), every other column on its table default; and it
--      writes the same AuditLog row the app writes (Action 'Departments.Create', EntityType 'Department', EntityId = new
--      id, NewValues = name, OrganizationId 13, ActorUserId NULL = script).
--      If exactly one ACTIVE department named like "Procurement" already exists it is reused (no second one).
--      If one exists but is inactive, or more than one exists, or the code is taken by another department: REFUSED.
--   2. depthead.a46138179@asset.local    -> Academics (Id 1456, top-level, active): the department this head heads.
--                                        A head heads one department (Users.DepartmentId is single); a department may
--                                        have several heads (every active Department Head holder in it is an approver).
--   3. procofficer.a46138179@asset.local -> the Procurement department from step 1.
-- Guards: database name, organisation id + name, exact user e-mails in org 13 with the expected role, target department
-- id + name + organisation + active, and a user's department must be NULL or already the target (never moves a user
-- who was placed elsewhere). Idempotent: a second run creates nothing and updates 0 rows. @Confirm = 0 (default) only reports.
-- Rollback: 99_rollback_DO_NOT_RUN.sql (users back to NULL while still on these departments; the Procurement department
-- is deleted only if it is the one 03 created and nothing else uses it).
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @Confirm BIT = 0;  -- set to 1 only for the approved run
DECLARE @OrgId INT = 13;
DECLARE @OrgName NVARCHAR(200) = N'Test-WF';
DECLARE @HeadEmail NVARCHAR(256) = N'depthead.a46138179@asset.local';
DECLARE @HeadDeptId INT = 1456;
DECLARE @HeadDeptName NVARCHAR(200) = N'Academics';
DECLARE @ProcEmail NVARCHAR(256) = N'procofficer.a46138179@asset.local';
DECLARE @ProcDeptName NVARCHAR(200) = N'Procurement';
DECLARE @ProcDeptCode NVARCHAR(40) = N'PROCUREMENT';
DECLARE @ProcDeptDescription NVARCHAR(500) = N'Procurement department';

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

-- Procurement department of organisation 13: reuse the single active one, else create (only when none exists at all).
DECLARE @ProcAnyCount INT = (SELECT COUNT(*) FROM [Department] WHERE [OrganizationId] = @OrgId AND [Name] LIKE N'Procurement%');
DECLARE @ProcActiveCount INT = (SELECT COUNT(*) FROM [Department] WHERE [OrganizationId] = @OrgId AND [IsActive] = 1 AND [Name] LIKE N'Procurement%');
DECLARE @CodeTakenBy INT = (SELECT TOP 1 [Id] FROM [Department] WHERE [OrganizationId] = @OrgId AND [Code] = @ProcDeptCode);
DECLARE @ProcDeptId INT = CASE WHEN @ProcActiveCount = 1 AND @ProcAnyCount = 1
    THEN (SELECT [Id] FROM [Department] WHERE [OrganizationId] = @OrgId AND [IsActive] = 1 AND [Name] LIKE N'Procurement%') END;
DECLARE @ProcDeptAction NVARCHAR(200) = CASE
    WHEN @ProcDeptId IS NOT NULL THEN N'exists (reused, no-op)'
    WHEN @ProcAnyCount = 0 AND @CodeTakenBy IS NULL THEN N'to create'
    WHEN @ProcAnyCount = 0 THEN N'REFUSED: code PROCUREMENT is used by another department'
    WHEN @ProcAnyCount > 1 THEN N'REFUSED: more than one Procurement department (choose by hand)'
    ELSE N'REFUSED: the Procurement department is inactive (reactivate by hand)' END;

DECLARE @HeadCurrent INT = (SELECT [DepartmentId] FROM [Users] WHERE [Id] = @HeadUserId);
DECLARE @ProcCurrent INT = (SELECT [DepartmentId] FROM [Users] WHERE [Id] = @ProcUserId);

SELECT N'Procurement department' AS [item], CONVERT(NVARCHAR(128), @ProcDeptId) AS id, @ProcAnyCount AS existing_named_procurement,
    CONVERT(NVARCHAR(20), @CodeTakenBy) AS code_procurement_used_by, @ProcDeptAction AS [action]
UNION ALL
SELECT N'depthead', @HeadUserId, @HeadCurrent, CONVERT(NVARCHAR(20), @HeadDeptId),
    CASE WHEN @HeadCurrent = @HeadDeptId THEN N'already set (no-op)'
         WHEN @HeadCurrent IS NULL THEN N'to set'
         ELSE N'REFUSED: already in another department' END
UNION ALL
SELECT N'procofficer', @ProcUserId, @ProcCurrent, CONVERT(NVARCHAR(20), @ProcDeptId),
    CASE WHEN @ProcDeptAction LIKE N'REFUSED%' THEN N'REFUSED: no usable Procurement department'
         WHEN @ProcDeptId IS NOT NULL AND @ProcCurrent = @ProcDeptId THEN N'already set (no-op)'
         WHEN @ProcCurrent IS NULL THEN N'to set'
         ELSE N'REFUSED: already in another department' END;

IF @HeadCurrent IS NOT NULL AND @HeadCurrent <> @HeadDeptId
BEGIN
    RAISERROR(N'03 NOT RUN: depthead is already in department %d; not moved.', 16, 1, @HeadCurrent);
    RETURN;
END
IF @ProcDeptAction LIKE N'REFUSED%'
BEGIN
    RAISERROR(N'03 NOT RUN: Procurement department check refused (see the action column).', 16, 1);
    RETURN;
END
IF @ProcCurrent IS NOT NULL AND (@ProcDeptId IS NULL OR @ProcCurrent <> @ProcDeptId)
BEGIN
    RAISERROR(N'03 NOT RUN: procofficer is already in department %d; not moved.', 16, 1, @ProcCurrent);
    RETURN;
END

IF @Confirm <> 1
BEGIN
    PRINT N'03: @Confirm = 0, nothing written (report only).';
    SELECT N'REPORT_ONLY' AS [RESULT], @ProcDeptAction AS procurement_department;
    RETURN;
END

DECLARE @deptCreated INT = 0, @headSet INT = 0, @procSet INT = 0, @now DATETIME = GETUTCDATE();
BEGIN TRANSACTION;
    IF @ProcDeptId IS NULL
    BEGIN
        INSERT INTO [Department] ([Name], [Code], [Description], [CreatedAt], [IsActive], [OrganizationId],
                                  [ParentDepartmentId], [DepartmentKind], [IsRequisitionTarget], [Section])
        SELECT @ProcDeptName, @ProcDeptCode, @ProcDeptDescription, @now, 1, @OrgId, NULL, 0, 1, 0
        WHERE NOT EXISTS (SELECT 1 FROM [Department] WHERE [OrganizationId] = @OrgId AND ([Name] LIKE N'Procurement%' OR [Code] = @ProcDeptCode));
        SET @deptCreated = @@ROWCOUNT;
        IF @deptCreated = 1
        BEGIN
            SET @ProcDeptId = CONVERT(INT, SCOPE_IDENTITY());
            INSERT INTO [AuditLog] ([ActorUserId], [Action], [EntityType], [EntityId], [OldValues], [NewValues],
                                    [Timestamp], [IPAddress], [CreatedAt], [IsActive], [OrganizationId])
            VALUES (NULL, N'Departments.Create', N'Department', CONVERT(NVARCHAR(20), @ProcDeptId), NULL, @ProcDeptName,
                    @now, NULL, @now, 1, @OrgId);
        END
        ELSE
        BEGIN
            THROW 50003, N'03: a Procurement department appeared during the run; nothing written.', 1;
        END
    END
    UPDATE [Users] SET [DepartmentId] = @HeadDeptId, [UpdatedAt] = GETDATE()
    WHERE [Id] = @HeadUserId AND [OrganizationId] = @OrgId AND [DepartmentId] IS NULL;
    SET @headSet = @@ROWCOUNT;
    UPDATE [Users] SET [DepartmentId] = @ProcDeptId, [UpdatedAt] = GETDATE()
    WHERE [Id] = @ProcUserId AND [OrganizationId] = @OrgId AND [DepartmentId] IS NULL;
    SET @procSet = @@ROWCOUNT;
COMMIT TRANSACTION;

SELECT [Id], [Name], [Code], [Description], [OrganizationId], [ParentDepartmentId], [DepartmentKind], [IsRequisitionTarget],
       [Section], [IsActive], [CreatedAt]
FROM [Department] WHERE [Id] = @ProcDeptId;
SELECT u.[Email], u.[DepartmentId], d.[Name] AS department
FROM [Users] u LEFT JOIN [Department] d ON d.[Id] = u.[DepartmentId]
WHERE u.[Id] IN (@HeadUserId, @ProcUserId);
SELECT N'DEPARTMENTS_OK' AS [RESULT], @deptCreated AS procurement_created, @ProcDeptId AS procurement_department_id,
    @headSet AS depthead_set, @procSet AS procofficer_set;
GO

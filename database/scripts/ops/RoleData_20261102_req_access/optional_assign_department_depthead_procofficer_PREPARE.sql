-- OPTIONAL  Give Test-WF depthead / procofficer a department so they can raise requisitions again.  PREPARED, DO NOT RUN.
-- Why: after answer 1 (2026-11-02) a user with no department and no Purchases.CreateForAnyDepartment cannot raise.
--   On alLAN's copy of Test-WF (org 13) both of these have DepartmentId = NULL:
--     depthead.a46138179@asset.local   (Department Head, role 63)
--     procofficer.a46138179@asset.local (Procurement Officer, role 61)
-- The department choice is Allan's. Nothing here runs until you replace the placeholders AND set @Confirm = 1.
-- Alternative with no data change to users: a Company Admin assigns the department in Users > Details (Assign department),
-- which is audited. Prefer that in the app; use this only if Allan asks for a script.
-- They can then raise for that department and the active rooms under it.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @Confirm BIT = 0;
DECLARE @OrgId INT = 13;
DECLARE @DeptHeadDepartmentId INT = NULL;   -- <DEPARTMENT_ID_FOR_DEPTHEAD>, chosen by Allan
DECLARE @ProcOfficerDepartmentId INT = NULL; -- <DEPARTMENT_ID_FOR_PROCOFFICER>, chosen by Allan

IF DB_NAME() NOT LIKE N'AssetManagementModuleDb%' OR NOT EXISTS (SELECT 1 FROM [Organization] WHERE [Id] = @OrgId AND [Name] = N'Test-WF')
BEGIN
    RAISERROR(N'OPTIONAL NOT RUN: wrong database or organisation 13 is not Test-WF.', 16, 1);
    RETURN;
END
IF @DeptHeadDepartmentId IS NULL OR @ProcOfficerDepartmentId IS NULL
BEGIN
    RAISERROR(N'OPTIONAL NOT RUN: Allan has not chosen the departments yet (placeholders are NULL).', 16, 1);
    RETURN;
END
IF NOT EXISTS (SELECT 1 FROM [Department] WHERE [Id] = @DeptHeadDepartmentId AND [OrganizationId] = @OrgId AND [IsActive] = 1)
   OR NOT EXISTS (SELECT 1 FROM [Department] WHERE [Id] = @ProcOfficerDepartmentId AND [OrganizationId] = @OrgId AND [IsActive] = 1)
BEGIN
    RAISERROR(N'OPTIONAL NOT RUN: a chosen department is not an active Test-WF department.', 16, 1);
    RETURN;
END

SELECT [UserName], [DepartmentId] AS [current_department] FROM [Users]
WHERE [OrganizationId] = @OrgId AND [UserName] IN (N'depthead.a46138179@asset.local', N'procofficer.a46138179@asset.local');

IF @Confirm <> 1
BEGIN
    PRINT N'OPTIONAL: @Confirm = 0, nothing written (report only).';
    RETURN;
END

BEGIN TRANSACTION;
    -- Only fills an empty department; never moves a user who already has one.
    UPDATE [Users] SET [DepartmentId] = @DeptHeadDepartmentId
    WHERE [OrganizationId] = @OrgId AND [UserName] = N'depthead.a46138179@asset.local' AND [DepartmentId] IS NULL;
    UPDATE [Users] SET [DepartmentId] = @ProcOfficerDepartmentId
    WHERE [OrganizationId] = @OrgId AND [UserName] = N'procofficer.a46138179@asset.local' AND [DepartmentId] IS NULL;
COMMIT TRANSACTION;
-- Undo: set [DepartmentId] = NULL for these two user names in organisation 13.
GO

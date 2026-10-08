/*
================================================================================
 PROPOSAL - NOT APPLIED. DO NOT RUN WITHOUT ALLAN'S EXPLICIT GO.
 DB-level same-company guard for Users.RoleId

 Context
   Role assignment lives only in dbo.Users.RoleId (int, nullable, NO foreign
   key today). dbo.IdentityUserRoles / dbo.IdentityRoles are empty and unused,
   so this guard targets Users.RoleId + Users.OrganizationId.
   The application already checks same-org in UserService.AssignRole and the
   new OrgReferenceGuard/OrgReferenceValidator (branch xorg-guard); this adds a
   last line of defence for raw SQL scripts and future code paths.

 Design (additive only; no data is changed)
   1. Unique index UX_Roles_OrganizationId_Id on dbo.Roles (OrganizationId, Id).
      Roles.Id is already the PK, so this can never fail on existing data; it
      only exists so a composite FK can point at (OrganizationId, Id).
   2. Composite FK FK_Users_Role_SameOrg:
         dbo.Users (OrganizationId, RoleId) -> dbo.Roles (OrganizationId, Id)
      SQL Server skips a composite FK check when ANY of its columns is NULL, so:
        * users with RoleId NULL are allowed (unchanged behaviour);
        * platform admins (Users.OrganizationId NULL) are allowed with any role
          (unchanged behaviour - platform admins are not company-scoped);
        * a company user can only hold a role whose Roles.OrganizationId is the
          SAME company. A role with OrganizationId NULL can no longer be given
          to a company user (pre-flight shows 0 such users on live, 8 Oct 2026).
      Created WITH CHECK so it is trusted; the pre-flight below aborts if any
      existing row would violate it.

 Pre-flight on live, 8 Oct 2026 (read-only): 51 users, 0 violations; no
 triggers on Users/Roles; Users has only FK_Users_Organization; Roles has
 PK(Id) and unique IX_Roles_Org_Name (OrganizationId, Name).

 Pros
   * Cross-company role assignment becomes impossible, including raw SQL and
     one-off scripts (the 16 Sep incident class), with no code change.
   * Additive, metadata-only, tiny tables (Roles/Users): instant to apply and
     to roll back. Also gives the optimizer a trusted constraint.
   * Doubles as the template for later same-org FKs (Users.DepartmentId ->
     Department, UserInvitation.RoleId, DepartmentApprovalStage.RoleId, approval
     action RoleId columns, and RolePermission once branch roleperm-org-fix has
     re-tagged its 31 rows).
 Cons / risks
   * Deleting a role still in use now fails (same as any FK). No application
     service deletes Role rows today (roles are deactivated), and the org purge
     (OrganizationPurgeSql) already NULLs Users.RoleId and deletes Users before
     Roles, so expected impact is nil.
   * Moving a user to another company must change RoleId in the SAME UPDATE
     as OrganizationId (otherwise the FK rejects the intermediate state).
   * Does NOT cover the CSV columns (*StageRoleIds / *StageUserIds): a CSV
     cannot carry an FK. Those stay guarded in the app (OrgReferenceGuard) and
     by the integrity check; the long-term fix is the ApprovalStage child table
     (docs/design/approval-stage-child-table.md).
   * Platform admins remain unconstrained (by design).
   * Any code path that inserts Users with OrganizationId set and a role from
     another company will start throwing SqlException 547 instead of saving -
     that is the intent, but QA should run user invite/create/role-change and
     company provisioning after applying.

 Apply: run sections 0-2 in a maintenance window (or any time; both objects
 are tiny). Rollback: section R.
================================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- 0) Pre-flight (read-only). Aborts if any company user holds a role from another company.
DECLARE @violations int =
(
    SELECT COUNT(*)
    FROM dbo.Users u
    LEFT JOIN dbo.Roles r ON r.Id = u.RoleId
    WHERE u.RoleId IS NOT NULL
      AND u.OrganizationId IS NOT NULL
      AND (r.Id IS NULL OR r.OrganizationId IS NULL OR r.OrganizationId <> u.OrganizationId)
);
IF @violations > 0
BEGIN
    SELECT u.Id AS UserId, u.OrganizationId AS UserOrg, u.RoleId, r.OrganizationId AS RoleOrg
    FROM dbo.Users u
    LEFT JOIN dbo.Roles r ON r.Id = u.RoleId
    WHERE u.RoleId IS NOT NULL
      AND u.OrganizationId IS NOT NULL
      AND (r.Id IS NULL OR r.OrganizationId IS NULL OR r.OrganizationId <> u.OrganizationId);
    RAISERROR('Users.RoleId same-org guard NOT applied: %d user(s) hold a role from another company (listed above). Fix them first.', 16, 1, @violations);
    RETURN;
END;
GO

BEGIN TRANSACTION;

-- 1) Target key for the composite FK.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Roles') AND name = N'UX_Roles_OrganizationId_Id')
    CREATE UNIQUE NONCLUSTERED INDEX UX_Roles_OrganizationId_Id ON dbo.Roles (OrganizationId, Id);

-- 2) Same-company FK, trusted (WITH CHECK).
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.Users') AND name = N'FK_Users_Role_SameOrg')
BEGIN
    ALTER TABLE dbo.Users WITH CHECK
        ADD CONSTRAINT FK_Users_Role_SameOrg
        FOREIGN KEY (OrganizationId, RoleId) REFERENCES dbo.Roles (OrganizationId, Id);
END;

COMMIT TRANSACTION;

-- Verify: should return 1 row with is_not_trusted = 0.
SELECT name, is_disabled, is_not_trusted FROM sys.foreign_keys WHERE name = N'FK_Users_Role_SameOrg';
GO

/*
-- R) ROLLBACK (restores today's schema exactly; no data involved)
BEGIN TRANSACTION;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.Users') AND name = N'FK_Users_Role_SameOrg')
    ALTER TABLE dbo.Users DROP CONSTRAINT FK_Users_Role_SameOrg;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Roles') AND name = N'UX_Roles_OrganizationId_Id')
    DROP INDEX UX_Roles_OrganizationId_Id ON dbo.Roles;
COMMIT TRANSACTION;
*/

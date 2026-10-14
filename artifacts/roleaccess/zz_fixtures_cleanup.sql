-- Removes the role-access scratch fixtures (org 13 Test-WF). AuditLog rows are kept (history), as in rel1009i proofs.
SET NOCOUNT ON; SET XACT_ABORT ON;
BEGIN TRAN;
DELETE FROM Users WHERE Email LIKE N'zz-%@roleaccess.invalid';
SELECT @@ROWCOUNT AS UsersDeleted;
DELETE rp FROM RolePermission rp JOIN Roles r ON r.Id=rp.RoleId WHERE r.OrganizationId=13 AND r.Name=N'ZZ Test Facilities Manager';
SELECT @@ROWCOUNT AS RolePermissionsDeleted;
DELETE FROM Roles WHERE OrganizationId=13 AND Name=N'ZZ Test Facilities Manager';
SELECT @@ROWCOUNT AS RolesDeleted;
COMMIT;

-- TEST FIXTURES (scratch) for role-access fix. Org 13 (Test-WF). Removed by zz_fixtures_cleanup.sql
SET NOCOUNT ON; SET XACT_ABORT ON;
BEGIN TRAN;
IF NOT EXISTS (SELECT 1 FROM Roles WHERE OrganizationId=13 AND Name=N'ZZ Test Facilities Manager')
BEGIN
  INSERT INTO Roles(Name,Description,IsSystemRole,CreatedAt,IsActive,OrganizationId) VALUES(N'ZZ Test Facilities Manager',N'roleaccess scratch',0,GETUTCDATE(),1,13);
  DECLARE @rid INT = SCOPE_IDENTITY();
  INSERT INTO RolePermission(RoleId,PermissionId,OrganizationId) SELECT @rid,PermissionId,13 FROM RolePermission WHERE RoleId=57;
END
DECLARE @role INT = (SELECT Id FROM Roles WHERE OrganizationId=13 AND Name=N'ZZ Test Facilities Manager');
DECLARE @src NVARCHAR(128) = (SELECT Id FROM Users WHERE Email='a46138179@asset.local');
DECLARE @t TABLE(email NVARCHAR(256), dept INT NULL, roleId INT);
INSERT @t VALUES(N'zz-fm-dept@roleaccess.invalid',268,@role),(N'zz-fm-nodept@roleaccess.invalid',NULL,@role),(N'zz-ca@roleaccess.invalid',NULL,59);
DECLARE @cols NVARCHAR(MAX) = (SELECT STRING_AGG(QUOTENAME(COLUMN_NAME),',') FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Users' AND COLUMN_NAME NOT IN ('Id','Email','UserName','DepartmentId','RoleId','AccessToken','FirstName','LastName','EmployeeNumber','CreatedAt'));
DECLARE @e NVARCHAR(256), @d INT, @r INT;
DECLARE c CURSOR LOCAL FOR SELECT email,dept,roleId FROM @t WHERE email NOT IN (SELECT Email FROM Users);
OPEN c; FETCH NEXT FROM c INTO @e,@d,@r;
WHILE @@FETCH_STATUS=0 BEGIN
  DECLARE @sql NVARCHAR(MAX) = N'INSERT INTO Users(Id,Email,UserName,DepartmentId,RoleId,AccessToken,FirstName,LastName,EmployeeNumber,CreatedAt,'+@cols+N') SELECT CONVERT(NVARCHAR(128),NEWID()),@e,@e,@d,@r,CONVERT(NVARCHAR(128),NEWID()),N''ZZ'',N''RoleAccess'',NULL,GETUTCDATE(),'+@cols+N' FROM Users WHERE Id=@src';
  EXEC sp_executesql @sql,N'@e NVARCHAR(256),@d INT,@r INT,@src NVARCHAR(128)',@e,@d,@r,@src;
  FETCH NEXT FROM c INTO @e,@d,@r;
END
CLOSE c;
COMMIT;
SELECT u.Email,u.DepartmentId,r.Name FROM Users u JOIN Roles r ON r.Id=u.RoleId WHERE u.Email LIKE N'zz-%@roleaccess.invalid';

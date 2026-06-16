-- Rename primary demo tenant to Nanosoft and ensure platform views can list it with a license.

UPDATE [Organization]
SET [Name] = N'Nanosoft',
    [Code] = N'NANOSOFT',
    [Slug] = N'nanosoft',
    [Email] = N'admin@nanosoft.asset.local',
    [Status] = N'Active',
    [IsActive] = 1
WHERE [Slug] IN (N'default', N'nanosoft')
   OR [Name] IN (N'Default Organization', N'Nanosoft');
GO

DECLARE @now DATETIME = GETUTCDATE();
DECLARE @passwordHash NVARCHAR(MAX) = N'ALJwzw5r970vW+fpNg4Ivw5nutwiP9Omge0gCdgtDVM2h6NFmycZ2GwSH5fyBqDTaw==';
DECLARE @securityStamp NVARCHAR(64) = N'00000000000000000000000000000001';
DECLARE @nanosoftOrgId INT = (SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] = N'nanosoft' ORDER BY [Id]);
DECLARE @companyAdminRoleId INT = (
    SELECT TOP 1 [Id]
    FROM [Roles]
    WHERE [OrganizationId] = @nanosoftOrgId AND [Name] = N'Company Admin'
    ORDER BY [Id]
);
DECLARE @deptId INT = (
    SELECT TOP 1 [Id]
    FROM [Department]
    WHERE [OrganizationId] = @nanosoftOrgId
    ORDER BY [Id]
);
DECLARE @canonicalAdminId NVARCHAR(128);

IF @nanosoftOrgId IS NOT NULL AND @companyAdminRoleId IS NOT NULL
BEGIN
    SET @canonicalAdminId = (
        SELECT TOP 1 u.[Id]
        FROM [Users] u
        WHERE u.[OrganizationId] = @nanosoftOrgId
        ORDER BY
            CASE WHEN LOWER(LTRIM(RTRIM(u.[Email]))) = N'nanosoft@asset.local' THEN 0 ELSE 1 END,
            CASE WHEN u.[Id] = N'seed-user-001' THEN 0 ELSE 1 END,
            CASE WHEN u.[RoleId] = @companyAdminRoleId THEN 0 ELSE 1 END,
            u.[Id]
    );

    IF @canonicalAdminId IS NULL
    BEGIN
        SET @canonicalAdminId = N'seed-user-001';
        INSERT INTO [Users]
            ([Id],[Email],[EmailConfirmed],[PasswordHash],[SecurityStamp],[PhoneNumberConfirmed],[TwoFactorEnabled],[LockoutEnabled],[AccessFailedCount],[UserName],
             [EmployeeNumber],[FirstName],[LastName],[IsActive],[RoleId],[OrganizationId],[DepartmentId],[PositionTitle],[CreatedAt])
        VALUES
            (@canonicalAdminId, N'nanosoft@asset.local', 1, @passwordHash, @securityStamp, 0, 0, 0, 0, N'nanosoft@asset.local',
             N'EMP-0001', N'Company', N'Admin', 1, @companyAdminRoleId, @nanosoftOrgId, @deptId, N'Company Administrator', @now);
    END
    ELSE
    BEGIN
        UPDATE [Users]
        SET [Email] = N'nanosoft@asset.local',
            [UserName] = N'nanosoft@asset.local',
            [PasswordHash] = @passwordHash,
            [IsActive] = 1,
            [TwoFactorEnabled] = 0,
            [RoleId] = @companyAdminRoleId,
            [OrganizationId] = @nanosoftOrgId,
            [DepartmentId] = COALESCE([DepartmentId], @deptId),
            [PositionTitle] = COALESCE(NULLIF(LTRIM(RTRIM([PositionTitle])), N''), N'Company Administrator')
        WHERE [Id] = @canonicalAdminId;
    END

    DELETE u
    FROM [Users] u
    WHERE u.[OrganizationId] = @nanosoftOrgId
      AND u.[Id] <> @canonicalAdminId
      AND u.[RoleId] = @companyAdminRoleId
      AND LOWER(LTRIM(RTRIM(u.[Email]))) IN (
          N'default@asset.local',
          N'superadmin@asset.local',
          N'companyadmin@asset.local',
          N'platform@asset.local',
          N'nanosoft@asset.local'
      );

    INSERT INTO [OrganizationLicense]
        ([OrganizationId],[PlanCode],[PlanName],[Status],[StartDate],[ExpiryDate],[MaxUsers],[CreatedAt],[IsActive])
    SELECT @nanosoftOrgId, N'Standard', N'Standard', N'Active', @now, DATEADD(MONTH, 12, @now), NULL, @now, 1
    WHERE NOT EXISTS (SELECT 1 FROM [OrganizationLicense] ol WHERE ol.[OrganizationId] = @nanosoftOrgId);

    INSERT INTO [OrganizationLicense]
        ([OrganizationId],[PlanCode],[PlanName],[Status],[StartDate],[ExpiryDate],[MaxUsers],[CreatedAt],[IsActive])
    SELECT o.[Id], N'Standard', N'Standard', N'Active', @now, DATEADD(MONTH, 12, @now), NULL, @now, 1
    FROM [Organization] o
    WHERE o.[IsActive] = 1
      AND NOT EXISTS (SELECT 1 FROM [OrganizationLicense] ol WHERE ol.[OrganizationId] = o.[Id]);
END
GO

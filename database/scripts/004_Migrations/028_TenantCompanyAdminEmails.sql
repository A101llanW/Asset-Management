-- Reserve superadmin@asset.local for platform admin; tenant company admins use {slug}@asset.local (demo-b -> demo@).

UPDATE u
SET [Email] = e.[TargetEmail],
    [UserName] = e.[TargetEmail]
FROM [Users] u
INNER JOIN [Organization] o ON o.[Id] = u.[OrganizationId]
CROSS APPLY (
    SELECT CASE
        WHEN LOWER(LTRIM(RTRIM(o.[Slug]))) = N'demo-b' THEN N'demo@asset.local'
        ELSE LOWER(LTRIM(RTRIM(o.[Slug]))) + N'@asset.local'
    END AS [TargetEmail]
) e
WHERE u.[OrganizationId] IS NOT NULL
  AND LOWER(LTRIM(RTRIM(u.[Email]))) IN (
      N'superadmin@asset.local',
      N'platform@asset.local',
      N'companyadmin@asset.local',
      N'companyadmin@demo-b.asset.local'
  )
  AND LOWER(LTRIM(RTRIM(u.[Email]))) <> LOWER(e.[TargetEmail])
  AND NOT EXISTS (
      SELECT 1
      FROM [Users] x
      WHERE x.[OrganizationId] = u.[OrganizationId]
        AND LOWER(LTRIM(RTRIM(x.[Email]))) = LOWER(e.[TargetEmail])
        AND x.[Id] <> u.[Id]
  );
GO

DELETE u
FROM [Users] u
INNER JOIN [Organization] o ON o.[Id] = u.[OrganizationId]
CROSS APPLY (
    SELECT CASE
        WHEN LOWER(LTRIM(RTRIM(o.[Slug]))) = N'demo-b' THEN N'demo@asset.local'
        ELSE LOWER(LTRIM(RTRIM(o.[Slug]))) + N'@asset.local'
    END AS [TargetEmail]
) e
WHERE u.[OrganizationId] IS NOT NULL
  AND LOWER(LTRIM(RTRIM(u.[Email]))) IN (
      N'superadmin@asset.local',
      N'platform@asset.local',
      N'companyadmin@asset.local',
      N'companyadmin@demo-b.asset.local'
  )
  AND EXISTS (
      SELECT 1
      FROM [Users] x
      WHERE x.[OrganizationId] = u.[OrganizationId]
        AND LOWER(LTRIM(RTRIM(x.[Email]))) = LOWER(e.[TargetEmail])
        AND x.[Id] <> u.[Id]
  );
GO

DECLARE @passwordHash NVARCHAR(MAX) = N'ALJwzw5r970vW+fpNg4Ivw5nutwiP9Omge0gCdgtDVM2h6NFmycZ2GwSH5fyBqDTaw==';
DECLARE @securityStamp NVARCHAR(64) = N'00000000000000000000000000000002';

IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Email] = N'superadmin@asset.local' AND [OrganizationId] IS NULL)
BEGIN
    DECLARE @platformRoleId INT = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Platform Admin' AND [OrganizationId] IS NULL ORDER BY [Id]);

    IF @platformRoleId IS NOT NULL
    BEGIN
        INSERT INTO [Users]
            ([Id],[Email],[EmailConfirmed],[PasswordHash],[SecurityStamp],[PhoneNumberConfirmed],[TwoFactorEnabled],[LockoutEnabled],[AccessFailedCount],[UserName],
             [EmployeeNumber],[FirstName],[LastName],[IsActive],[RoleId],[OrganizationId],[CreatedAt])
        VALUES
            (N'seed-user-platform', N'superadmin@asset.local', 1, @passwordHash, @securityStamp, 0, 0, 0, 0, N'superadmin@asset.local',
             N'EMP-PLATFORM', N'Platform', N'Admin', 1, @platformRoleId, NULL, GETUTCDATE());
    END
END
GO

DECLARE @passwordHash NVARCHAR(MAX) = N'ALJwzw5r970vW+fpNg4Ivw5nutwiP9Omge0gCdgtDVM2h6NFmycZ2GwSH5fyBqDTaw==';
DECLARE @securityStamp NVARCHAR(64) = N'00000000000000000000000000000002';

-- Ensure each tenant has a company admin at its slug-based login email.
DECLARE @orgId INT;
DECLARE @orgSlug NVARCHAR(128);
DECLARE @targetEmail NVARCHAR(256);
DECLARE @companyAdminRoleId INT;

DECLARE org_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT o.[Id], o.[Slug]
    FROM [Organization] o
    WHERE o.[IsActive] = 1;

OPEN org_cursor;
FETCH NEXT FROM org_cursor INTO @orgId, @orgSlug;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @targetEmail = CASE
        WHEN LOWER(LTRIM(RTRIM(@orgSlug))) = N'demo-b' THEN N'demo@asset.local'
        ELSE LOWER(LTRIM(RTRIM(@orgSlug))) + N'@asset.local'
    END;

    SET @companyAdminRoleId = (
        SELECT TOP 1 [Id]
        FROM [Roles]
        WHERE [OrganizationId] = @orgId AND [Name] = N'Company Admin'
        ORDER BY [Id]
    );

    IF @companyAdminRoleId IS NOT NULL
       AND NOT EXISTS (
           SELECT 1
           FROM [Users] u
           INNER JOIN [Roles] r ON r.[Id] = u.[RoleId]
           WHERE u.[OrganizationId] = @orgId
             AND r.[Name] = N'Company Admin'
             AND u.[IsActive] = 1
       )
       AND NOT EXISTS (
           SELECT 1 FROM [Users] WHERE [OrganizationId] = @orgId AND LOWER(LTRIM(RTRIM([Email]))) = LOWER(@targetEmail)
       )
    BEGIN
        DECLARE @deptId INT = (
            SELECT TOP 1 [Id]
            FROM [Department]
            WHERE [OrganizationId] = @orgId
            ORDER BY [Id]
        );

        INSERT INTO [Users]
            ([Id],[Email],[EmailConfirmed],[PasswordHash],[SecurityStamp],[PhoneNumberConfirmed],[TwoFactorEnabled],[LockoutEnabled],[AccessFailedCount],[UserName],
             [EmployeeNumber],[FirstName],[LastName],[IsActive],[RoleId],[OrganizationId],[DepartmentId],[PositionTitle],[CreatedAt])
        VALUES
            (N'seed-company-admin-' + CAST(@orgId AS NVARCHAR(20)), @targetEmail, 1, @passwordHash, @securityStamp, 0, 0, 0, 0, @targetEmail,
             N'EMP-ADMIN-' + CAST(@orgId AS NVARCHAR(20)), N'Company', N'Admin', 1, @companyAdminRoleId, @orgId, @deptId, N'Company Administrator', GETUTCDATE());
    END

    FETCH NEXT FROM org_cursor INTO @orgId, @orgSlug;
END

CLOSE org_cursor;
DEALLOCATE org_cursor;
GO

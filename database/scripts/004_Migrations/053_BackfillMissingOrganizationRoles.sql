-- Backfill tenant roles for organizations that never received role clones.
-- Migration 013 looked up template org by Slug = 'default'; after rename to 'nanosoft'
-- Demo Organization B (and any similar orgs) were left without Roles.

DECLARE @templateOrgId INT = (
    SELECT TOP 1 r.[OrganizationId]
    FROM [Roles] r
    WHERE r.[OrganizationId] IS NOT NULL
      AND r.[IsActive] = 1
      AND r.[Name] <> N'Platform Admin'
    GROUP BY r.[OrganizationId]
    HAVING COUNT(1) > 0
    ORDER BY r.[OrganizationId]
);

IF @templateOrgId IS NULL
BEGIN
    RETURN;
END

DECLARE @targetOrgId INT;
DECLARE @now DATETIME = GETUTCDATE();

DECLARE org_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT o.[Id]
    FROM [Organization] o
    WHERE o.[IsActive] = 1
      AND o.[Id] <> @templateOrgId
      AND NOT EXISTS (
          SELECT 1
          FROM [Roles] r
          WHERE r.[OrganizationId] = o.[Id]
            AND r.[IsActive] = 1
            AND r.[Name] <> N'Platform Admin'
      );

OPEN org_cursor;
FETCH NEXT FROM org_cursor INTO @targetOrgId;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @roleMap TABLE (TemplateRoleId INT NOT NULL, NewRoleId INT NOT NULL);
    DELETE FROM @roleMap;

    DECLARE @templateRoleId INT;
    DECLARE @newRoleId INT;
    DECLARE @roleName NVARCHAR(120);
    DECLARE @roleDescription NVARCHAR(500);
    DECLARE @isSystemRole BIT;

    DECLARE role_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT [Id], [Name], [Description], [IsSystemRole]
        FROM [Roles]
        WHERE [OrganizationId] = @templateOrgId
          AND [IsActive] = 1
          AND [Name] <> N'Platform Admin';

    OPEN role_cursor;
    FETCH NEXT FROM role_cursor INTO @templateRoleId, @roleName, @roleDescription, @isSystemRole;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF NOT EXISTS (
            SELECT 1 FROM [Roles]
            WHERE [OrganizationId] = @targetOrgId AND [Name] = @roleName
        )
        BEGIN
            INSERT INTO [Roles] ([Name],[Description],[IsSystemRole],[OrganizationId],[CreatedAt],[IsActive])
            VALUES (@roleName, @roleDescription, @isSystemRole, @targetOrgId, @now, 1);
            SET @newRoleId = SCOPE_IDENTITY();
            INSERT INTO @roleMap (TemplateRoleId, NewRoleId) VALUES (@templateRoleId, @newRoleId);

            INSERT INTO [RolePermission] ([RoleId],[PermissionId],[OrganizationId])
            SELECT @newRoleId, rp.[PermissionId], @targetOrgId
            FROM [RolePermission] rp
            WHERE rp.[RoleId] = @templateRoleId
              AND (rp.[OrganizationId] = @templateOrgId OR rp.[OrganizationId] IS NULL);
        END

        FETCH NEXT FROM role_cursor INTO @templateRoleId, @roleName, @roleDescription, @isSystemRole;
    END
    CLOSE role_cursor;
    DEALLOCATE role_cursor;

    FETCH NEXT FROM org_cursor INTO @targetOrgId;
END
CLOSE org_cursor;
DEALLOCATE org_cursor;
GO

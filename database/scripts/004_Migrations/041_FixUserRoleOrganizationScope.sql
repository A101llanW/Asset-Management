-- Fix users whose RoleId points at a role row from another organization (shows wrong security role in lists)
DECLARE @now DATETIME = GETUTCDATE();

UPDATE u
SET u.[RoleId] = matched.[Id],
    u.[UpdatedAt] = @now
FROM [Users] u
INNER JOIN [Roles] assignedRole ON assignedRole.[Id] = u.[RoleId]
INNER JOIN [Roles] matched ON matched.[OrganizationId] = u.[OrganizationId]
    AND matched.[Name] = assignedRole.[Name]
    AND matched.[IsActive] = 1
WHERE u.[OrganizationId] IS NOT NULL
  AND assignedRole.[OrganizationId] IS NOT NULL
  AND assignedRole.[OrganizationId] <> u.[OrganizationId]
  AND matched.[Id] <> u.[RoleId];

GO

-- Browser-only application: retire unused REST API permissions.
IF EXISTS (SELECT 1 FROM [Permission] WHERE [Code] LIKE N'Api.%')
BEGIN
    DELETE rp
    FROM [RolePermission] rp
    INNER JOIN [Permission] p ON p.[Id] = rp.[PermissionId]
    WHERE p.[Code] LIKE N'Api.%';

    UPDATE [Permission]
    SET [IsActive] = 0
    WHERE [Code] LIKE N'Api.%';
END
GO

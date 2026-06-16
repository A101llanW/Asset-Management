-- Phase 2: Platform support operate permission for impersonation elevation

DECLARE @now datetime = GETUTCDATE();



IF NOT EXISTS (SELECT 1 FROM [Permission] WHERE [Code] = N'Platform.Support.Operate')

BEGIN

    INSERT INTO [Permission] ([Name], [Code], [Module], [Description], [CreatedAt], [IsActive])

    VALUES (N'Operate Tenant Support', N'Platform.Support.Operate', N'Platform', N'Activate approved impersonation sessions', @now, 1);

END



DECLARE @operatePermissionId int = (SELECT [Id] FROM [Permission] WHERE [Code] = N'Platform.Support.Operate');

DECLARE @platformAdminRoleId int = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Platform Admin' AND [IsSystemRole] = 1 ORDER BY [Id]);



IF @operatePermissionId IS NOT NULL AND @platformAdminRoleId IS NOT NULL

    AND NOT EXISTS (

        SELECT 1 FROM [RolePermission]

        WHERE [RoleId] = @platformAdminRoleId AND [PermissionId] = @operatePermissionId)

BEGIN

    INSERT INTO [RolePermission] ([RoleId], [PermissionId], [OrganizationId])

    VALUES (@platformAdminRoleId, @operatePermissionId, NULL);

END



IF NOT EXISTS (SELECT 1 FROM [SystemSetting] WHERE [SettingKey] = N'Settings.AllowAdminSelfApproval')

BEGIN

    DECLARE @defaultOrgId INT = (SELECT TOP 1 [Id] FROM [Organization] ORDER BY [Id]);

    INSERT INTO [SystemSetting] ([SettingKey], [SettingValue], [Description], [CreatedAt], [IsActive], [OrganizationId])

    VALUES (N'Settings.AllowAdminSelfApproval', N'false', N'Allow company administrators to approve their own workflow requests', @now, 1, @defaultOrgId);

END



-- Align approval stage role ids with canonical role names, backfill depreciation defaults, and seed starter suppliers.

DECLARE @now DATETIME = GETUTCDATE();

DECLARE @orgId INT;
DECLARE org_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT [Id] FROM [Organization] WHERE [IsActive] = 1;

OPEN org_cursor;
FETCH NEXT FROM org_cursor INTO @orgId;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @companyAdminRoleId INT = (
        SELECT TOP 1 [Id] FROM [Roles]
        WHERE [OrganizationId] = @orgId AND [IsActive] = 1 AND [Name] = N'Company Admin'
        ORDER BY [Id]);
    DECLARE @departmentHeadRoleId INT = (
        SELECT TOP 1 [Id] FROM [Roles]
        WHERE [OrganizationId] = @orgId AND [IsActive] = 1 AND [Name] = N'Department Head'
        ORDER BY [Id]);
    DECLARE @procurementRoleId INT = (
        SELECT TOP 1 [Id] FROM [Roles]
        WHERE [OrganizationId] = @orgId AND [IsActive] = 1 AND [Name] = N'Procurement Officer'
        ORDER BY [Id]);

    IF @departmentHeadRoleId IS NOT NULL
    BEGIN
        UPDATE [SystemSetting]
        SET [SettingValue] = CAST(@departmentHeadRoleId AS NVARCHAR(20)),
            [UpdatedAt] = CASE WHEN COL_LENGTH(N'[SystemSetting]', N'UpdatedAt') IS NOT NULL THEN @now ELSE [UpdatedAt] END
        WHERE [OrganizationId] = @orgId
          AND [SettingKey] = N'Approval.Process.Transfer.StageRoleIds';
    END

    IF @companyAdminRoleId IS NOT NULL
    BEGIN
        UPDATE [SystemSetting]
        SET [SettingValue] = CAST(@companyAdminRoleId AS NVARCHAR(20)),
            [UpdatedAt] = CASE WHEN COL_LENGTH(N'[SystemSetting]', N'UpdatedAt') IS NOT NULL THEN @now ELSE [UpdatedAt] END
        WHERE [OrganizationId] = @orgId
          AND [SettingKey] = N'Approval.Process.Disposal.StageRoleIds';
    END

    IF @procurementRoleId IS NOT NULL
    BEGIN
        UPDATE [SystemSetting]
        SET [SettingValue] = CAST(@procurementRoleId AS NVARCHAR(20)),
            [UpdatedAt] = CASE WHEN COL_LENGTH(N'[SystemSetting]', N'UpdatedAt') IS NOT NULL THEN @now ELSE [UpdatedAt] END
        WHERE [OrganizationId] = @orgId
          AND [SettingKey] = N'Approval.Process.Purchase.StageRoleIds';
    END

    UPDATE [SystemSetting]
    SET [SettingValue] = N'',
        [UpdatedAt] = CASE WHEN COL_LENGTH(N'[SystemSetting]', N'UpdatedAt') IS NOT NULL THEN @now ELSE [UpdatedAt] END
    WHERE [OrganizationId] = @orgId
      AND [SettingKey] IN (
          N'Approval.Process.Transfer.StageUserIds',
          N'Approval.Process.Disposal.StageUserIds',
          N'Approval.Process.Purchase.StageUserIds');

    IF COL_LENGTH(N'[AssetCategory]', N'DefaultDepreciationLifeMonths') IS NOT NULL
    BEGIN
        UPDATE [AssetCategory]
        SET [DefaultDepreciationLifeMonths] = 48, [DefaultDepreciationRatePercent] = 25.00
        WHERE [OrganizationId] = @orgId AND [Name] = N'IT Equipment'
          AND [DefaultDepreciationLifeMonths] IS NULL;

        UPDATE [AssetCategory]
        SET [DefaultDepreciationLifeMonths] = 60, [DefaultDepreciationRatePercent] = 20.00
        WHERE [OrganizationId] = @orgId AND [Name] = N'Office Equipment'
          AND [DefaultDepreciationLifeMonths] IS NULL;

        UPDATE [AssetCategory]
        SET [DefaultDepreciationLifeMonths] = 84, [DefaultDepreciationRatePercent] = 14.29
        WHERE [OrganizationId] = @orgId AND [Name] = N'Furniture'
          AND [DefaultDepreciationLifeMonths] IS NULL;

        UPDATE [AssetCategory]
        SET [DefaultDepreciationLifeMonths] = 60, [DefaultDepreciationRatePercent] = 20.00
        WHERE [OrganizationId] = @orgId AND [Name] = N'Networking'
          AND [DefaultDepreciationLifeMonths] IS NULL;

        UPDATE [AssetCategory]
        SET [DefaultDepreciationLifeMonths] = 84, [DefaultDepreciationRatePercent] = 14.29
        WHERE [OrganizationId] = @orgId AND [Name] = N'Medical/Lab Equipment'
          AND [DefaultDepreciationLifeMonths] IS NULL;

        UPDATE [AssetCategory]
        SET [DefaultDepreciationLifeMonths] = 60, [DefaultDepreciationRatePercent] = 20.00
        WHERE [OrganizationId] = @orgId AND [Name] = N'Vehicles'
          AND [DefaultDepreciationLifeMonths] IS NULL;
    END

    FETCH NEXT FROM org_cursor INTO @orgId;
END
CLOSE org_cursor;
DEALLOCATE org_cursor;
GO

-- Align default requisition approval stage and role descriptions with department-head + procurement model
DECLARE @now DATETIME = GETUTCDATE();

IF COL_LENGTH(N'[Roles]', N'OrganizationId') IS NOT NULL
BEGIN
    UPDATE r
    SET r.[Description] = v.[Description]
    FROM [Roles] r
    INNER JOIN (VALUES
        (N'Company Admin', N'Full organization configuration including approval matrix and roles.'),
        (N'Department Head', N'Submits requisitions and in-store asset requests for their department.'),
        (N'Procurement Officer', N'Approves requisitions org-wide; manages suppliers, catalog, and purchase orders.'),
        (N'Asset Manager', N'Manages assets; approves in-store requests and receives goods against POs.'),
        (N'Finance Officer', N'Financial reporting and depreciation; not the default requisition approver.'),
        (N'Staff', N'Assignee-only profile — register employees without assigning this login role.'),
        (N'Auditor', N'Read-only access to reports and audit data.')
    ) AS v([Name], [Description])
        ON r.[Name] = v.[Name]
    WHERE r.[Description] IS NULL
       OR r.[Description] LIKE N'%seeded demo role%';
END
ELSE
BEGIN
    UPDATE r
    SET r.[Description] = v.[Description]
    FROM [Roles] r
    INNER JOIN (VALUES
        (N'Company Admin', N'Full organization configuration including approval matrix and roles.'),
        (N'Department Head', N'Submits requisitions and in-store asset requests for their department.'),
        (N'Procurement Officer', N'Approves requisitions org-wide; manages suppliers, catalog, and purchase orders.'),
        (N'Asset Manager', N'Manages assets; approves in-store requests and receives goods against POs.'),
        (N'Finance Officer', N'Financial reporting and depreciation; not the default requisition approver.'),
        (N'Staff', N'Assignee-only profile — register employees without assigning this login role.'),
        (N'Auditor', N'Read-only access to reports and audit data.')
    ) AS v([Name], [Description])
        ON r.[Name] = v.[Name]
    WHERE r.[Description] IS NULL
       OR r.[Description] LIKE N'%seeded demo role%';
END

IF COL_LENGTH(N'[SystemSetting]', N'OrganizationId') IS NOT NULL
BEGIN
    DECLARE @orgId INT;
    DECLARE org_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT DISTINCT [OrganizationId] FROM [SystemSetting] WHERE [OrganizationId] IS NOT NULL;

    OPEN org_cursor;
    FETCH NEXT FROM org_cursor INTO @orgId;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        DECLARE @companyAdminRoleId INT = (
            SELECT TOP 1 [Id] FROM [Roles] WHERE [OrganizationId] = @orgId AND [Name] = N'Company Admin' ORDER BY [Id]);
        DECLARE @procurementRoleId INT = (
            SELECT TOP 1 [Id] FROM [Roles] WHERE [OrganizationId] = @orgId AND [Name] = N'Procurement Officer' ORDER BY [Id]);

        IF @companyAdminRoleId IS NOT NULL AND @procurementRoleId IS NOT NULL
        BEGIN
            UPDATE [SystemSetting]
            SET [SettingValue] = CAST(@procurementRoleId AS NVARCHAR(20)),
                [Description] = N'Stage 1 approver role ids for requisitions.',
                [UpdatedAt] = @now
            WHERE [OrganizationId] = @orgId
              AND [SettingKey] = N'Approval.Process.Purchase.StageRoleIds'
              AND [SettingValue] = CAST(@companyAdminRoleId AS NVARCHAR(20));
        END

        FETCH NEXT FROM org_cursor INTO @orgId;
    END

    CLOSE org_cursor;
    DEALLOCATE org_cursor;
END
ELSE
BEGIN
    DECLARE @companyAdminRoleIdGlobal INT = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Company Admin' ORDER BY [Id]);
    DECLARE @procurementRoleIdGlobal INT = (SELECT TOP 1 [Id] FROM [Roles] WHERE [Name] = N'Procurement Officer' ORDER BY [Id]);

    IF @companyAdminRoleIdGlobal IS NOT NULL AND @procurementRoleIdGlobal IS NOT NULL
    BEGIN
        UPDATE [SystemSetting]
        SET [SettingValue] = CAST(@procurementRoleIdGlobal AS NVARCHAR(20)),
            [Description] = N'Stage 1 approver role ids for requisitions.',
            [UpdatedAt] = @now
        WHERE [SettingKey] = N'Approval.Process.Purchase.StageRoleIds'
          AND [SettingValue] = CAST(@companyAdminRoleIdGlobal AS NVARCHAR(20));
    END
END

GO

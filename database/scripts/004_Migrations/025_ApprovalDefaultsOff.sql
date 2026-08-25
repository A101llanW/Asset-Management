-- Approval is opt-in: Requires approval unchecked by default for settings and new assets.

UPDATE [SystemSetting]
SET [SettingValue] = N'false'
WHERE [SettingKey] IN (
    N'Approval.RequireDisposalApproval',
    N'Approval.RequireTransferApproval',
    N'Approval.RequirePurchaseApproval',
    N'Approval.Process.Transfer.Enabled',
    N'Approval.Process.Disposal.Enabled',
    N'Approval.Process.Purchase.Enabled'
)
AND [SettingValue] = N'true';
GO

IF COL_LENGTH(N'[SystemSetting]', N'UpdatedAt') IS NOT NULL
BEGIN
    UPDATE [SystemSetting]
    SET [UpdatedAt] = GETUTCDATE()
    WHERE [SettingKey] IN (
        N'Approval.RequireDisposalApproval',
        N'Approval.RequireTransferApproval',
        N'Approval.RequirePurchaseApproval',
        N'Approval.Process.Transfer.Enabled',
        N'Approval.Process.Disposal.Enabled',
        N'Approval.Process.Purchase.Enabled'
    )
    AND [SettingValue] = N'false';
END
GO

IF COL_LENGTH(N'[Asset]', N'RequireTransferApproval') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_Asset_RequireTransferApproval')
        ALTER TABLE [Asset] DROP CONSTRAINT [DF_Asset_RequireTransferApproval];
    ALTER TABLE [Asset] ADD CONSTRAINT [DF_Asset_RequireTransferApproval] DEFAULT(0) FOR [RequireTransferApproval];
END
GO

IF COL_LENGTH(N'[Asset]', N'RequireDisposalApproval') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_Asset_RequireDisposalApproval')
        ALTER TABLE [Asset] DROP CONSTRAINT [DF_Asset_RequireDisposalApproval];
    ALTER TABLE [Asset] ADD CONSTRAINT [DF_Asset_RequireDisposalApproval] DEFAULT(0) FOR [RequireDisposalApproval];
END
GO

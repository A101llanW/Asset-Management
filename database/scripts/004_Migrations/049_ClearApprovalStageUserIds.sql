-- Approval paths are role-based; clear legacy per-user stage assignments so any user with the stage role can act.
DECLARE @now DATETIME = GETUTCDATE();

UPDATE [SystemSetting]
SET [SettingValue] = N'',
    [Description] = N'Legacy per-user approver ids (unused; approval is role-based).',
    [UpdatedAt] = @now
WHERE [SettingKey] IN (
    N'Approval.Process.Transfer.StageUserIds',
    N'Approval.Process.Disposal.StageUserIds',
    N'Approval.Process.Purchase.StageUserIds'
)
  AND [SettingValue] IS NOT NULL
  AND LTRIM(RTRIM([SettingValue])) <> N'';

UPDATE [Asset]
SET [TransferApprovalStageUserIds] = NULL,
    [DisposalApprovalStageUserIds] = NULL,
    [UpdatedAt] = @now
WHERE ([TransferApprovalStageUserIds] IS NOT NULL AND LTRIM(RTRIM([TransferApprovalStageUserIds])) <> N'')
   OR ([DisposalApprovalStageUserIds] IS NOT NULL AND LTRIM(RTRIM([DisposalApprovalStageUserIds])) <> N'');

IF COL_LENGTH(N'[AssetTransfer]', N'ApprovalStageUserIds') IS NOT NULL
BEGIN
    UPDATE [AssetTransfer]
    SET [ApprovalStageUserIds] = NULL,
        [UpdatedAt] = @now
    WHERE [ApprovalStatus] = 1
      AND [ApprovalStageUserIds] IS NOT NULL
      AND LTRIM(RTRIM([ApprovalStageUserIds])) <> N'';
END

IF COL_LENGTH(N'[DisposalRecord]', N'ApprovalStageUserIds') IS NOT NULL
BEGIN
    UPDATE [DisposalRecord]
    SET [ApprovalStageUserIds] = NULL,
        [UpdatedAt] = @now
    WHERE [ApprovalStatus] = 1
      AND [ApprovalStageUserIds] IS NOT NULL
      AND LTRIM(RTRIM([ApprovalStageUserIds])) <> N'';
END

IF COL_LENGTH(N'[PurchaseRequest]', N'ApprovalStageUserIds') IS NOT NULL
BEGIN
    UPDATE [PurchaseRequest]
    SET [ApprovalStageUserIds] = NULL,
        [UpdatedAt] = @now
    WHERE [ApprovalStatus] = 1
      AND [ApprovalStageUserIds] IS NOT NULL
      AND LTRIM(RTRIM([ApprovalStageUserIds])) <> N'';
END

IF COL_LENGTH(N'dbo.Users', N'IsEmailVerified') IS NOT NULL
BEGIN
    UPDATE dbo.Users
    SET IsEmailVerified = 1
    WHERE IsActive = 1 AND IsEmailVerified = 0;
END

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

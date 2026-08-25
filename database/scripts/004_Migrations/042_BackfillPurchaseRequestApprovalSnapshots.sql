-- Pending requisitions submitted before staged approval snapshots were stored cannot be approved until
-- ApprovalStageRoleIds is populated from the current approval matrix.
UPDATE pr
SET
    pr.[ApprovalStageRoleIds] = ss.[SettingValue],
    pr.[CurrentApprovalStage] = CASE WHEN pr.[CurrentApprovalStage] <= 0 THEN 1 ELSE pr.[CurrentApprovalStage] END,
    pr.[UpdatedAt] = GETUTCDATE()
FROM [PurchaseRequest] pr
CROSS APPLY (
    SELECT TOP 1 s.[SettingValue]
    FROM [SystemSetting] s
    WHERE s.[SettingKey] = N'Approval.Process.Purchase.StageRoleIds'
      AND LTRIM(RTRIM(ISNULL(s.[SettingValue], N''))) <> N''
      AND (
          (COL_LENGTH(N'[SystemSetting]', N'OrganizationId') IS NOT NULL AND s.[OrganizationId] = pr.[OrganizationId])
          OR (COL_LENGTH(N'[SystemSetting]', N'OrganizationId') IS NULL)
      )
    ORDER BY CASE
        WHEN COL_LENGTH(N'[SystemSetting]', N'OrganizationId') IS NOT NULL AND s.[OrganizationId] = pr.[OrganizationId] THEN 0
        ELSE 1
    END
) ss
WHERE pr.[ApprovalStatus] = 1
  AND pr.[IsActive] = 1
  AND (pr.[ApprovalStageRoleIds] IS NULL OR LTRIM(RTRIM(pr.[ApprovalStageRoleIds])) = N'');

GO

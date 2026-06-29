-- Repair legacy workflow snapshot data (migration 007 defaulted AssetTransfer.OriginalAssetStatus to 2 / AwaitingApproval)
-- AssetStatus: AwaitingApproval = 2, Assigned = 6, InStore = 5, Disposed = 13
-- ApprovalStatus: Pending = 1, Approved = 2, Rejected = 3, Cancelled = 4

-- 1) Assets stuck in AwaitingApproval with no open transfer/disposal workflow
UPDATE a
SET
    a.[CurrentStatus] = CASE WHEN a.[CurrentCustodianId] IS NOT NULL THEN 6 ELSE 5 END,
    a.[UpdatedAt] = GETUTCDATE()
FROM [Asset] a
WHERE a.[CurrentStatus] = 2
  AND a.[IsActive] = 1
  AND NOT EXISTS (
      SELECT 1 FROM [AssetTransfer] t
      WHERE t.[AssetId] = a.[Id]
        AND t.[IsActive] = 1
        AND t.[ApprovalStatus] = 1
  )
  AND NOT EXISTS (
      SELECT 1 FROM [DisposalRecord] d
      WHERE d.[AssetId] = a.[Id]
        AND d.[IsActive] = 1
        AND d.[ApprovalStatus] = 1
  );
GO

-- 2) Closed transfers: infer pre-approval status from current asset state (rejected/cancelled) or Assigned (approved)
UPDATE t
SET t.[OriginalAssetStatus] = CASE
        WHEN t.[ApprovalStatus] IN (3, 4) AND a.[CurrentStatus] <> 2 THEN a.[CurrentStatus]
        WHEN t.[ApprovalStatus] = 2 THEN 6
        ELSE CASE WHEN a.[CurrentCustodianId] IS NOT NULL THEN 6 ELSE 5 END
    END
FROM [AssetTransfer] t
INNER JOIN [Asset] a ON a.[Id] = t.[AssetId]
WHERE t.[IsActive] = 1
  AND t.[ApprovalStatus] IN (2, 3, 4)
  AND t.[OriginalAssetStatus] = 2;
GO

-- 3) Closed disposals: same repair for bad snapshots (including rows that incorrectly stored AwaitingApproval)
UPDATE d
SET d.[OriginalAssetStatus] = CASE
        WHEN d.[ApprovalStatus] IN (3, 4) AND a.[CurrentStatus] <> 2 THEN a.[CurrentStatus]
        WHEN d.[ApprovalStatus] = 2 THEN 6
        ELSE CASE WHEN a.[CurrentCustodianId] IS NOT NULL THEN 6 ELSE 5 END
    END
FROM [DisposalRecord] d
INNER JOIN [Asset] a ON a.[Id] = d.[AssetId]
WHERE d.[IsActive] = 1
  AND d.[ApprovalStatus] IN (2, 3, 4)
  AND d.[OriginalAssetStatus] = 2;
GO

-- 4) Rejected/cancelled workflows where asset is still AwaitingApproval after snapshot repair
UPDATE a
SET
    a.[CurrentStatus] = CASE WHEN a.[CurrentCustodianId] IS NOT NULL THEN 6 ELSE 5 END,
    a.[UpdatedAt] = GETUTCDATE()
FROM [Asset] a
WHERE a.[CurrentStatus] = 2
  AND a.[IsActive] = 1
  AND NOT EXISTS (
      SELECT 1 FROM [AssetTransfer] t
      WHERE t.[AssetId] = a.[Id] AND t.[IsActive] = 1 AND t.[ApprovalStatus] = 1
  )
  AND NOT EXISTS (
      SELECT 1 FROM [DisposalRecord] d
      WHERE d.[AssetId] = a.[Id] AND d.[IsActive] = 1 AND d.[ApprovalStatus] = 1
  )
  AND (
      EXISTS (
          SELECT 1 FROM [AssetTransfer] t
          WHERE t.[AssetId] = a.[Id]
            AND t.[IsActive] = 1
            AND t.[ApprovalStatus] IN (3, 4)
      )
      OR EXISTS (
          SELECT 1 FROM [DisposalRecord] d
          WHERE d.[AssetId] = a.[Id]
            AND d.[IsActive] = 1
            AND d.[ApprovalStatus] IN (3, 4)
      )
  );
GO

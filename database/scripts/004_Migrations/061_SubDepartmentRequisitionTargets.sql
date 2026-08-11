-- Backfill requisition targets: parents with children are not leaf targets.

UPDATE [Department]
SET [IsRequisitionTarget] = 0
WHERE [IsActive] = 1
  AND [DepartmentKind] IN (0, 1)
  AND EXISTS (
      SELECT 1
      FROM [Department] AS [Child]
      WHERE [Child].[ParentDepartmentId] = [Department].[Id]
        AND [Child].[IsActive] = 1
  );
GO

UPDATE [Department]
SET [IsRequisitionTarget] = 1
WHERE [IsActive] = 1
  AND [DepartmentKind] IN (2, 3)
  AND NOT EXISTS (
      SELECT 1
      FROM [Department] AS [Child]
      WHERE [Child].[ParentDepartmentId] = [Department].[Id]
        AND [Child].[IsActive] = 1
  );
GO

UPDATE [Department]
SET [IsRequisitionTarget] = 1
WHERE [IsActive] = 1
  AND [DepartmentKind] = 0
  AND [ParentDepartmentId] IS NULL
  AND NOT EXISTS (
      SELECT 1
      FROM [Department] AS [Child]
      WHERE [Child].[ParentDepartmentId] = [Department].[Id]
        AND [Child].[IsActive] = 1
  );
GO

-- OPTIONAL (B6): idempotent backfill of AssetMovement from the existing AssetCustodyEvent history.
-- Not a migration; run by hand after 093 if historic movements should appear on the Movements page.
-- Safe to re-run: rows are keyed by SourceRef = 'custody:<AssetCustodyEvent.Id>'.
-- OccurredAtUtc takes the custody event's ActionDate (UTC for app-written events; older Assign/Return
-- rows may carry a date-only value from the form, i.e. midnight). New rows written by the app always use UtcNow.
-- Optional: limit to one org by setting @OrgId.
SET NOCOUNT ON;
IF OBJECT_ID(N'[dbo].[AssetMovement]', N'U') IS NULL BEGIN RAISERROR(N'Apply migration 093 first.', 16, 1); RETURN; END
DECLARE @OrgId INT = NULL;
-- Custody events written after the app started recording movements already have their own movement row.
DECLARE @Cutoff DATETIME2(3) = ISNULL((SELECT MIN([OccurredAtUtc]) FROM [dbo].[AssetMovement] WHERE [SourceRef] IS NULL OR [SourceRef] NOT LIKE N'custody:%'), '9999-12-31');

INSERT INTO [dbo].[AssetMovement] ([OrganizationId],[AssetId],[FromDepartmentId],[ToDepartmentId],[FromCustodianUserId],[ToCustodianUserId],
                                   [MovementType],[BatchId],[PerformedByUserId],[OccurredAtUtc],[Notes],[SourceRef])
SELECT a.[OrganizationId], ce.[AssetId],
       CASE WHEN fd.[Id] IS NULL THEN NULL ELSE ce.[FromDepartmentId] END,
       CASE WHEN td.[Id] IS NULL THEN NULL ELSE ce.[ToDepartmentId] END,
       ce.[FromUserId], ce.[ToUserId],
       CASE
           WHEN ce.[ActionType] = 1 THEN 6                                         -- Assigned  -> Assign
           WHEN ce.[ActionType] = 3 THEN 7                                         -- Returned  -> Return
           WHEN ce.[ActionType] = 2 AND ce.[Reason] = N'Relocated between classes' THEN 3
           WHEN ce.[ActionType] = 2 AND ce.[Reason] = N'Relocated between rooms' THEN 4
           WHEN ce.[ActionType] = 2 AND ce.[Reason] = N'Department changed (bulk move)' THEN 2
           WHEN ce.[ActionType] = 2 AND ce.[Reason] = N'Department changed' THEN 1
           WHEN ce.[ActionType] = 2 THEN 5                                         -- Transferred -> Transfer
           ELSE 9                                                                  -- Recovered / Disposed -> Other
       END,
       NULL, ce.[ApprovedById], ce.[ActionDate],
       LEFT(LTRIM(RTRIM(ISNULL(ce.[Reason], N'') + CASE WHEN ce.[Notes] IS NULL THEN N'' ELSE N' - ' + ce.[Notes] END)), 500),
       N'custody:' + CAST(ce.[Id] AS NVARCHAR(20))
FROM [dbo].[AssetCustodyEvent] ce
JOIN [dbo].[Asset] a ON a.[Id] = ce.[AssetId]
LEFT JOIN [dbo].[Department] fd ON fd.[Id] = ce.[FromDepartmentId]
LEFT JOIN [dbo].[Department] td ON td.[Id] = ce.[ToDepartmentId]
WHERE a.[OrganizationId] IS NOT NULL
  AND (@OrgId IS NULL OR a.[OrganizationId] = @OrgId)
  AND ISNULL(ce.[CreatedAt], ce.[ActionDate]) < @Cutoff
  AND NOT EXISTS (SELECT 1 FROM [dbo].[AssetMovement] m WHERE m.[SourceRef] = N'custody:' + CAST(ce.[Id] AS NVARCHAR(20)));

SELECT @@ROWCOUNT AS BackfilledRows;

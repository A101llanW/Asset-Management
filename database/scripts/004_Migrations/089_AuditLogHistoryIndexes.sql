-- 089: AuditLog history read indexes (schema only, idempotent, no data changes).
-- Supports grant-free AuditLogQueryRepository.GetLogs:
--   * IX_AuditLog_Org_Timestamp_Id: newest-first org lists (platform org details recent audit,
--     AuditLogs/Index) are read in index order -> no sort / memory grant, TOP n reads a page.
--   * IX_AuditLog_Org_Entity: per-entity history (Assets/Details audit tab) seeks by
--     OrganizationId + EntityType + EntityId instead of scanning the whole table; NewValues
--     (short id strings) is included so the related-asset NewValues match reads only the
--     org's slice of this narrow index rather than the full clustered table.
-- Rollback:
--   DROP INDEX [IX_AuditLog_Org_Timestamp_Id] ON [dbo].[AuditLog];
--   DROP INDEX [IX_AuditLog_Org_Entity] ON [dbo].[AuditLog];
--   DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'089_AuditLogHistoryIndexes.sql';  -- if recorded

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'[dbo].[AuditLog]') AND name = N'IX_AuditLog_Org_Timestamp_Id')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditLog_Org_Timestamp_Id]
        ON [dbo].[AuditLog] ([OrganizationId], [Timestamp] DESC, [Id] DESC)
        INCLUDE ([ActorUserId], [Action], [EntityType], [EntityId], [IPAddress]);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'[dbo].[AuditLog]') AND name = N'IX_AuditLog_Org_Entity')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditLog_Org_Entity]
        ON [dbo].[AuditLog] ([OrganizationId], [EntityType], [EntityId])
        INCLUDE ([Timestamp], [NewValues]);
END
ELSE IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes i
    INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE i.object_id = OBJECT_ID(N'[dbo].[AuditLog]') AND i.name = N'IX_AuditLog_Org_Entity'
      AND c.name = N'NewValues' AND ic.is_included_column = 1)
BEGIN
    -- Early local build of this index lacked NewValues; rebuild it with the include.
    CREATE NONCLUSTERED INDEX [IX_AuditLog_Org_Entity]
        ON [dbo].[AuditLog] ([OrganizationId], [EntityType], [EntityId])
        INCLUDE ([Timestamp], [NewValues])
        WITH (DROP_EXISTING = ON);
END
GO

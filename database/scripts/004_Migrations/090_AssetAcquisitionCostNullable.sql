-- 090: Asset.AcquisitionCost becomes NULLable so an asset can have no price of its own ("Not set").
-- Allan (2026-10-08): "If an asset has no price of its own, leave it blank (Not set) and never copy
-- the sub-type price." NULL = Not set; 0.00 stays a real, explicit price.
-- SCHEMA ONLY: no UPDATE of any row. Existing values (including 0.00, 0.01 and the 1.00 QA set on
-- assets 23161/23297/23341) are kept exactly as they are.
-- Indexes that carry AcquisitionCost (key or INCLUDE) block ALTER COLUMN, so they are scripted from the
-- catalog, dropped, and recreated with the same definition (keys, order, INCLUDE list, filter, uniqueness).
-- Idempotent: does nothing once the column is already NULLable.
SET XACT_ABORT ON;
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Asset') AND name = N'AcquisitionCost' AND is_nullable = 0)
BEGIN
    DECLARE @indexes TABLE (IndexName SYSNAME NOT NULL, CreateSql NVARCHAR(MAX) NOT NULL);

    INSERT INTO @indexes (IndexName, CreateSql)
    SELECT
        i.name,
        N'CREATE ' + CASE WHEN i.is_unique = 1 THEN N'UNIQUE ' ELSE N'' END
            + N'NONCLUSTERED INDEX ' + QUOTENAME(i.name) + N' ON [dbo].[Asset] ('
            + STUFF((
                SELECT N', ' + QUOTENAME(c.name) + CASE WHEN ic.is_descending_key = 1 THEN N' DESC' ELSE N'' END
                FROM sys.index_columns ic
                JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
                ORDER BY ic.key_ordinal
                FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 2, N'')
            + N')'
            + ISNULL(N' INCLUDE (' + STUFF((
                SELECT N', ' + QUOTENAME(c.name)
                FROM sys.index_columns ic
                JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1
                ORDER BY ic.index_column_id
                FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 2, N'') + N')', N'')
            + CASE WHEN i.has_filter = 1 THEN N' WHERE ' + i.filter_definition ELSE N'' END
            + N';'
    FROM sys.indexes i
    WHERE i.object_id = OBJECT_ID(N'dbo.Asset')
      AND i.type = 2                 -- nonclustered rowstore only
      AND i.is_primary_key = 0
      AND i.is_unique_constraint = 0
      AND EXISTS (
          SELECT 1 FROM sys.index_columns ic
          JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
          WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = N'AcquisitionCost');

    -- Keep a record of what is recreated (shown in the migration output).
    SELECT IndexName, CreateSql FROM @indexes;

    BEGIN TRANSACTION;

    DECLARE @name SYSNAME, @sql NVARCHAR(MAX);
    DECLARE drop_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT IndexName FROM @indexes;
    OPEN drop_cursor;
    FETCH NEXT FROM drop_cursor INTO @name;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = N'DROP INDEX ' + QUOTENAME(@name) + N' ON [dbo].[Asset];';
        EXEC sys.sp_executesql @sql;
        FETCH NEXT FROM drop_cursor INTO @name;
    END
    CLOSE drop_cursor;
    DEALLOCATE drop_cursor;

    ALTER TABLE [dbo].[Asset] ALTER COLUMN [AcquisitionCost] DECIMAL(18,2) NULL;

    DECLARE create_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT CreateSql FROM @indexes;
    OPEN create_cursor;
    FETCH NEXT FROM create_cursor INTO @sql;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        EXEC sys.sp_executesql @sql;
        FETCH NEXT FROM create_cursor INTO @sql;
    END
    CLOSE create_cursor;
    DEALLOCATE create_cursor;

    COMMIT TRANSACTION;
END
GO

-- Rollback (only safe while no asset has a NULL price; deploy the previous DLLs first):
--   Script and drop the indexes that include AcquisitionCost (IX_Asset_Org_Active_ProductGroup,
--   IX_Asset_Org_Active_Dept_DashboardKpi), then
--   ALTER TABLE [dbo].[Asset] ALTER COLUMN [AcquisitionCost] DECIMAL(18,2) NOT NULL;
--   and recreate the indexes (see 084 / 086).

using System;
using AssetManagement.Application.Contracts;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    /// <summary>
    /// SQL-backed tag counter (AssetTagSequence). Runs on its own connection (never the unit-of-work transaction)
    /// and commits at once. The existing-tag scan uses READUNCOMMITTED so it neither blocks on nor is blocked by an
    /// open import/receive transaction; an uncommitted tag can only push the number up, never cause a repeat.
    /// </summary>
    public class AssetTagSequenceStore : IAssetTagSequenceStore
    {
        // Highest numeric suffix already used with PREFIX- (any asset of the tenant, active or not).
        private const string MaxExistingSql = @"
SELECT @max = ISNULL(MAX(TRY_CONVERT(INT, SUBSTRING(t.[Tag], LEN(@Prefix) + 2, 20))), 0)
FROM (SELECT a.[AssetTag] AS [Tag] FROM [dbo].[Asset] a WITH (READUNCOMMITTED)
      WHERE a.[OrganizationId] = @OrganizationId AND a.[AssetTag] LIKE @Prefix + N'-%'
      UNION ALL
      -- rel1013b: active old-tag aliases also hold their number, so a retired tag is never handed out again.
      SELECT al.[AliasTag] FROM [dbo].[AssetTagAlias] al WITH (READUNCOMMITTED)
      WHERE al.[OrganizationId] = @OrganizationId AND al.[IsActive] = 1 AND al.[AliasTag] LIKE @Prefix + N'-%') t
WHERE LEN(t.[Tag]) > LEN(@Prefix) + 1
  AND SUBSTRING(t.[Tag], LEN(@Prefix) + 2, 20) NOT LIKE N'%[^0-9]%';";

        private const string AllocateSql = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @max INT, @last INT, @next INT;
BEGIN TRANSACTION;
SELECT @last = [LastValue] FROM [dbo].[AssetTagSequence] WITH (UPDLOCK, HOLDLOCK)
WHERE [OrganizationId] = @OrganizationId AND [Prefix] = @Prefix;
" + MaxExistingSql + @"
SET @next = (CASE WHEN ISNULL(@last, 0) > @max THEN ISNULL(@last, 0) ELSE @max END) + 1;
IF @last IS NULL
    INSERT INTO [dbo].[AssetTagSequence] ([OrganizationId], [Prefix], [LastValue], [UpdatedAt]) VALUES (@OrganizationId, @Prefix, @next, SYSUTCDATETIME());
ELSE
    UPDATE [dbo].[AssetTagSequence] SET [LastValue] = @next, [UpdatedAt] = SYSUTCDATETIME() WHERE [OrganizationId] = @OrganizationId AND [Prefix] = @Prefix;
COMMIT TRANSACTION;
SELECT @next;";

        private const string PeekSql = @"
SET NOCOUNT ON;
DECLARE @max INT, @last INT;
SELECT @last = [LastValue] FROM [dbo].[AssetTagSequence] WHERE [OrganizationId] = @OrganizationId AND [Prefix] = @Prefix;
" + MaxExistingSql + @"
SELECT (CASE WHEN ISNULL(@last, 0) > @max THEN ISNULL(@last, 0) ELSE @max END) + 1;";

        private const int MaxRetries = 3;
        private readonly ISqlConnectionFactory _connectionFactory;

        public AssetTagSequenceStore(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public int AllocateNext(int organizationId, string prefix)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return Execute(AllocateSql, organizationId, prefix);
                }
                catch (System.Data.SqlClient.SqlException ex) when (attempt < MaxRetries && (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 1205))
                {
                    // Two first-ever allocations for the same prefix raced on the INSERT (PK) or a deadlock victim: retry.
                }
            }
        }

        public int PeekNext(int organizationId, string prefix)
        {
            return Execute(PeekSql, organizationId, prefix);
        }

        private int Execute(string sql, int organizationId, string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                throw new ArgumentException("Prefix is required.", "prefix");
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    var p = command.Parameters.Add("@Prefix", System.Data.SqlDbType.NVarChar, 20);
                    p.Value = prefix;
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }
    }
}
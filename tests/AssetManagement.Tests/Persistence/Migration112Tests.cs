using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace AssetManagement.Tests.Persistence
{
    /// <summary>Migration 112 (Organisation section heads): file rules, plus a database check when GROKFIX_PURGE_DB points at a scratch copy.</summary>
    [TestFixture]
    public class Migration112Tests
    {
        private const string ScriptName = "112_OrganizationSectionHead.sql";

        private static string ScriptsDir()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "database", "scripts");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            Assert.Inconclusive("database/scripts not found.");
            return null;
        }

        private static string Migration()
        {
            return File.ReadAllText(Path.Combine(ScriptsDir(), "004_Migrations", ScriptName));
        }

        private static string Rollback()
        {
            return File.ReadAllText(Path.Combine(ScriptsDir(), "ops", "112_Rollback_OrganizationSectionHead_PREPARE_DO_NOT_RUN.sql"));
        }

        private static string Executable(string sql)
        {
            return string.Join("\n", sql.Split('\n').Where(x => !x.TrimStart().StartsWith("--", StringComparison.Ordinal)));
        }

        [Test]
        public void MigrationNumber_IsUniqueInTheMigrationsFolder()
        {
            var files = Directory.GetFiles(Path.Combine(ScriptsDir(), "004_Migrations"), "112_*.sql");
            Assert.AreEqual(1, files.Length);
        }

        [Test]
        public void Migration_IsAdditiveGuardedTransactional_AndDependsOn106_107_111()
        {
            var sql = Migration();

            StringAssert.Contains("COL_LENGTH(N'dbo.Department', N'Section') IS NULL", sql);
            StringAssert.Contains("COL_LENGTH(N'dbo.Department', N'PrepHeadUserId') IS NULL", sql);
            StringAssert.Contains("COL_LENGTH(N'dbo.Department', N'KgHeadUserId') IS NULL", sql);
            StringAssert.Contains("OBJECT_ID(N'dbo.OrganizationSectionHead', N'U') IS NULL", sql);
            StringAssert.Contains("CHECK ([Section] IN (0, 1, 2))", sql);
            StringAssert.Contains("UX_OrganizationSectionHead_Org_Section", sql);
            StringAssert.Contains("WITH NOCHECK ADD CONSTRAINT [FK_OrganizationSectionHead_Organization]", sql);
            StringAssert.Contains("WITH NOCHECK ADD CONSTRAINT [FK_OrganizationSectionHead_HeadUser]", sql);
            StringAssert.Contains("IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'" + ScriptName + "')", sql);
            StringAssert.Contains("SET XACT_ABORT ON;", sql);
            StringAssert.Contains("BEGIN TRANSACTION;", sql);
            StringAssert.Contains("COMMIT TRANSACTION;", sql);
            StringAssert.Contains("DistinctCount] = 1", sql);

            var executable = Executable(sql);
            StringAssert.DoesNotContain("DROP COLUMN", executable);
            StringAssert.DoesNotContain("DROP TABLE", executable);
            StringAssert.DoesNotContain("TRUNCATE", executable);
            StringAssert.DoesNotContain("\nGO", executable);
            // Soft backfill may UPDATE OrganizationSectionHead only; Department head columns must stay untouched.
            StringAssert.DoesNotContain("UPDATE [dbo].[Department]", executable);
            StringAssert.DoesNotContain("DELETE FROM [dbo].[Department]", executable);
        }

        [Test]
        public void Rollback_IsInOps_ConfirmGuarded_AndLeaves107_111Untouched()
        {
            var sql = Rollback();

            StringAssert.Contains("DO NOT RUN", sql);
            StringAssert.Contains("DECLARE @Confirm NVARCHAR(10) = N'NO';", sql);
            StringAssert.Contains("@Confirm <> N'YES'", sql);
            StringAssert.Contains("DROP TABLE [dbo].[OrganizationSectionHead]", sql);
            StringAssert.Contains("FK_OrganizationSectionHead_HeadUser", sql);
            StringAssert.Contains(ScriptName, sql);
            StringAssert.Contains("107/111", sql);
            StringAssert.DoesNotContain("\nGO", Executable(sql));
            StringAssert.DoesNotContain("DROP COLUMN", Executable(sql));
            StringAssert.DoesNotContain("PrepHeadUserId", Executable(sql));
            StringAssert.DoesNotContain("KgHeadUserId", Executable(sql));
        }

        [Test]
        public void ScratchDatabase_HasOrganizationSectionHead_When112Applied()
        {
            var connectionString = Environment.GetEnvironmentVariable("GROKFIX_PURGE_DB");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Assert.Ignore("GROKFIX_PURGE_DB not set.");
            }

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                if (Scalar(connection, "SELECT COUNT(*) FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'" + ScriptName + "'") == 0)
                {
                    Assert.Ignore("Migration 112 has not been applied to this database.");
                }

                Assert.AreEqual(1, Scalar(connection, "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.OrganizationSectionHead')"));
                Assert.AreEqual(1, Scalar(connection, "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OrganizationSectionHead') AND name = N'UX_OrganizationSectionHead_Org_Section'"));
                Assert.AreEqual(1, Scalar(connection, "SELECT COUNT(*) FROM sys.foreign_keys WHERE name = N'FK_OrganizationSectionHead_Organization'"));
                Assert.AreEqual(1, Scalar(connection, "SELECT COUNT(*) FROM sys.foreign_keys WHERE name = N'FK_OrganizationSectionHead_HeadUser'"));
                Assert.AreEqual(0, Scalar(connection, "SELECT COUNT(*) FROM [dbo].[OrganizationSectionHead] WHERE [Section] NOT IN (0, 1, 2)"));
                // Every org has exactly the three section rows.
                Assert.AreEqual(0, Scalar(connection, @"
SELECT COUNT(*) FROM [dbo].[Organization] o
WHERE (SELECT COUNT(*) FROM [dbo].[OrganizationSectionHead] h WHERE h.[OrganizationId] = o.[Id]) <> 3"));
            }
        }

        private static int Scalar(SqlConnection connection, string sql)
        {
            using (var command = new SqlCommand(sql, connection))
            {
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }
    }
}
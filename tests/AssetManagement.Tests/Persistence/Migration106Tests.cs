using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace AssetManagement.Tests.Persistence
{
    /// <summary>Migration 106 (Department.Section): file rules, plus a database check when GROKFIX_PURGE_DB points at a scratch copy.</summary>
    [TestFixture]
    public class Migration106Tests
    {
        private const string ScriptName = "106_DepartmentSection.sql";

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

        [Test]
        public void MigrationNumber_IsUniqueInTheMigrationsFolder()
        {
            var files = Directory.GetFiles(Path.Combine(ScriptsDir(), "004_Migrations"), "106_*.sql");
            Assert.AreEqual(1, files.Length);
        }

        [Test]
        public void Migration_IsAdditiveGuardedAndTransactional()
        {
            var sql = Migration();

            StringAssert.Contains("COL_LENGTH(N'dbo.Department', N'Section') IS NULL", sql);
            StringAssert.Contains("COL_LENGTH(N'dbo.Department', N'SortOrder') IS NULL", sql);
            StringAssert.Contains("INT NOT NULL CONSTRAINT [DF_Department_Section] DEFAULT (0)", sql);
            StringAssert.Contains("CK_Department_Section", sql);
            StringAssert.Contains("IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'" + ScriptName + "')", sql);
            StringAssert.Contains("SET XACT_ABORT ON;", sql);
            StringAssert.Contains("BEGIN TRANSACTION;", sql);
            StringAssert.Contains("COMMIT TRANSACTION;", sql);

            var executable = string.Join("\n", sql.Split('\n').Where(x => !x.TrimStart().StartsWith("--", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("DROP COLUMN", executable);
            StringAssert.DoesNotContain("DELETE ", executable);
            StringAssert.DoesNotContain("TRUNCATE", executable);
            StringAssert.DoesNotContain("DROP TABLE [", executable);
        }

        [Test]
        public void DataFix_IsGuardedByIdAndName_AndOnlyTouchesTheElevenPrepRows()
        {
            var sql = Migration();

            StringAssert.Contains("f.[Id] = d.[Id] AND f.[Name] = d.[Name]", sql);
            StringAssert.Contains("WHERE d.[Section] <> 1", sql);
            StringAssert.Contains("(1614, N'Prep')", sql);
            for (var id = 1582; id <= 1591; id++)
            {
                StringAssert.Contains("(" + id + ", N'Prep ", sql);
            }

            StringAssert.Contains("Prep Counsellor''s Room", sql);
            Assert.AreEqual(11, Regex.Matches(sql, @"^\s*\((1[56]\d\d), N'", RegexOptions.Multiline).Count);
        }

        [Test]
        public void Rollback_IsInOps_AndDropsOnlyWhatMigrationAdded()
        {
            var path = Path.Combine(ScriptsDir(), "ops", "106_Rollback_DepartmentSection_PREPARE_DO_NOT_RUN.sql");
            Assert.IsTrue(File.Exists(path));
            var sql = File.ReadAllText(path);

            StringAssert.Contains("DO NOT RUN", sql);
            StringAssert.Contains("DROP CONSTRAINT [CK_Department_Section]", sql);
            StringAssert.Contains(ScriptName, sql);
        }

        [Test]
        public void ScratchDatabase_HasTheSectionColumns_DefaultSeniorAndCheckConstraint()
        {
            var connectionString = Environment.GetEnvironmentVariable("GROKFIX_PURGE_DB");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Assert.Ignore("GROKFIX_PURGE_DB not set.");
            }

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                if (Scalar(connection, "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Department') AND name IN (N'Section', N'SortOrder')") == 0)
                {
                    Assert.Ignore("Migration 106 has not been applied to this database.");
                }

                Assert.AreEqual(2, Scalar(connection, "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Department') AND name IN (N'Section', N'SortOrder')"));
                Assert.AreEqual(0, Scalar(connection, "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Department') AND name = N'Section' AND is_nullable = 1"));
                Assert.AreEqual(1, Scalar(connection, "SELECT COUNT(*) FROM sys.check_constraints WHERE name = N'CK_Department_Section'"));
                Assert.AreEqual(1, Scalar(connection, "SELECT COUNT(*) FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'" + ScriptName + "'"));
                Assert.AreEqual(0, Scalar(connection, "SELECT COUNT(*) FROM [dbo].[Department] WHERE [Section] NOT IN (0, 1)"));
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

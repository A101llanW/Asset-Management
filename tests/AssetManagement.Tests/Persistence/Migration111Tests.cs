using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace AssetManagement.Tests.Persistence
{
    /// <summary>Migration 111 (Section KG + KG head): file rules, plus a database check when GROKFIX_PURGE_DB points at a scratch copy.</summary>
    [TestFixture]
    public class Migration111Tests
    {
        private const string ScriptName = "111_DepartmentSectionKG.sql";

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
            return File.ReadAllText(Path.Combine(ScriptsDir(), "ops", "111_Rollback_DepartmentSectionKG_PREPARE_DO_NOT_RUN.sql"));
        }

        private static string Executable(string sql)
        {
            return string.Join("\n", sql.Split('\n').Where(x => !x.TrimStart().StartsWith("--", StringComparison.Ordinal)));
        }

        [Test]
        public void MigrationNumber_IsUniqueInTheMigrationsFolder()
        {
            var files = Directory.GetFiles(Path.Combine(ScriptsDir(), "004_Migrations"), "111_*.sql");
            Assert.AreEqual(1, files.Length);
        }

        [Test]
        public void Migration_IsAdditiveGuardedAndTransactional()
        {
            var sql = Migration();

            StringAssert.Contains("CHECK ([Section] IN (0, 1, 2))", sql);
            StringAssert.Contains("COL_LENGTH(N'dbo.Department', N'KgHeadUserId') IS NULL", sql);
            StringAssert.Contains("WITH NOCHECK ADD CONSTRAINT [FK_Department_KgHeadUser] FOREIGN KEY ([KgHeadUserId]) REFERENCES [dbo].[Users]([Id])", sql);
            StringAssert.Contains("IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'" + ScriptName + "')", sql);
            StringAssert.Contains("SET XACT_ABORT ON;", sql);
            StringAssert.Contains("BEGIN TRANSACTION;", sql);
            StringAssert.Contains("COMMIT TRANSACTION;", sql);

            var executable = Executable(sql);
            StringAssert.DoesNotContain("DROP COLUMN", executable);
            StringAssert.DoesNotContain("DELETE ", executable);
            StringAssert.DoesNotContain("UPDATE ", executable);
            StringAssert.DoesNotContain("TRUNCATE", executable);
            StringAssert.DoesNotContain("DROP TABLE", executable);
            StringAssert.DoesNotContain("INSERT INTO [dbo].[Department]", executable);
            StringAssert.DoesNotContain("\nGO", executable);
        }

        [Test]
        public void Rollback_IsInOps_ConfirmGuarded_AndRefusesWhileKgIsUsed()
        {
            var sql = Rollback();

            StringAssert.Contains("DO NOT RUN", sql);
            StringAssert.Contains("DECLARE @Confirm NVARCHAR(10) = N'NO';", sql);
            StringAssert.Contains("@Confirm <> N'YES'", sql);
            StringAssert.Contains("WHERE [Section] = 2", sql);
            StringAssert.Contains("[KgHeadUserId] IS NOT NULL", sql);
            StringAssert.Contains("DROP CONSTRAINT [FK_Department_KgHeadUser]", sql);
            StringAssert.Contains("CHECK ([Section] IN (0, 1))", sql);
            StringAssert.Contains(ScriptName, sql);
            StringAssert.DoesNotContain("\nGO", Executable(sql));
        }

        [Test]
        public void ScratchDatabase_AllowsKg_AndHasTheKgHeadColumn()
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
                    Assert.Ignore("Migration 111 has not been applied to this database.");
                }

                Assert.AreEqual(1, Scalar(connection, "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Department') AND name = N'KgHeadUserId' AND is_nullable = 1"));
                Assert.AreEqual(1, Scalar(connection, "SELECT COUNT(*) FROM sys.foreign_keys WHERE name = N'FK_Department_KgHeadUser'"));
                Assert.AreEqual(1, Scalar(connection, "SELECT COUNT(*) FROM sys.check_constraints WHERE name = N'CK_Department_Section' AND REPLACE(definition, N' ', N'') LIKE N'%[[]Section]=(2)%'"));
                Assert.AreEqual(0, Scalar(connection, "SELECT COUNT(*) FROM [dbo].[Department] WHERE [Section] NOT IN (0, 1, 2)"));
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

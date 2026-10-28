using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace AssetManagement.Tests.Requisitions
{
    /// <summary>Migration 108 / its rollback and the C3 scripts: file rules (nothing here touches a database).</summary>
    [TestFixture]
    public class RequisitionLineAssetNameFileTests
    {
        private static readonly string[] ReceiptTrailColumns =
        {
            "ClassificationOverriddenById", "ClassificationOverriddenAt",
            "OriginalAssetCategoryId", "OriginalAssetTypeId", "OriginalAssetSubTypeId",
            "NewAssetCategoryId", "NewAssetTypeId", "NewAssetSubTypeId"
        };

        private static readonly string[] LineTrailColumns =
        {
            "ClassificationOverriddenById", "ClassificationOverriddenAt",
            "OriginalAssetCategoryId", "OriginalAssetTypeId", "OriginalAssetSubTypeId"
        };

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "src", "AssetManagement.Application"))
                    && Directory.Exists(Path.Combine(directory.FullName, "database", "scripts")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            Assert.Inconclusive("Repository root not found.");
            return null;
        }

        private static string Read(params string[] parts)
        {
            return File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));
        }

        private static string Migration()
        {
            return Read("database", "scripts", "004_Migrations", "108_requisition_line_asset_name.sql");
        }

        private static string Rollback()
        {
            return Read("database", "scripts", "ops", "108_Rollback_RequisitionLineAssetName_PREPARE_DO_NOT_RUN.sql");
        }

        [Test]
        public void Migration_AddsEveryTrailColumn_Guarded_Nullable_WithoutForeignKeys()
        {
            var sql = Migration();
            foreach (var table in new[] { "PurchaseOrderLine", "PurchaseOrderLineReceipt" })
            {
                foreach (var column in table == "PurchaseOrderLine" ? LineTrailColumns : ReceiptTrailColumns)
                {
                    StringAssert.Contains("IF COL_LENGTH(N'dbo." + table + "', N'" + column + "') IS NULL", sql, table + "." + column);
                    Assert.IsTrue(Regex.IsMatch(sql, @"ADD \[" + column + @"\] [A-Z]+(\(\d+\))? NULL;"), table + "." + column + " is NULLable");
                    Assert.IsFalse(Regex.IsMatch(sql, @"FOREIGN KEY \(\[" + column + @"\]\)"), column + " has no FK");
                }
            }

            StringAssert.DoesNotContain("COL_LENGTH(N'dbo.PurchaseOrderLine', N'NewAssetTypeId')", sql, "a PO line keeps its own classification columns");
        }

        [Test]
        public void Rollback_DropsEveryColumnTheMigrationAdds()
        {
            var added = Regex.Matches(Migration(), @"ALTER TABLE \[dbo\]\.\[(\w+)\] ADD \[(\w+)\]")
                .Cast<Match>().Select(x => x.Groups[1].Value + "." + x.Groups[2].Value).ToList();
            var dropped = Regex.Matches(Rollback(), @"IF COL_LENGTH\(N'dbo\.(\w+)', N'(\w+)'\) IS NOT NULL\s+ALTER TABLE \[dbo\]\.\[\1\] DROP COLUMN \[\2\]")
                .Cast<Match>().Select(x => x.Groups[1].Value + "." + x.Groups[2].Value).ToList();
            Assert.AreEqual(16, added.Count);
            CollectionAssert.AreEquivalent(added, dropped);
        }

        [TestCase("receive-order.js")]
        [TestCase("purchase-order-lines.js")]
        public void Scripts_DoNotUseClassListToggleWithForce_Ie11(string file)
        {
            var script = Read("src", "AssetManagement.Web", "Scripts", "app", file);
            Assert.IsFalse(Regex.IsMatch(script, @"classList\.toggle\([^)]*,"), file + ": IE11 ignores the force argument");
        }
    }
}
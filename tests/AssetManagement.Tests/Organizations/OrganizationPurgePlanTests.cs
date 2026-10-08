using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using AssetManagement.Domain.Common;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Services;
using NUnit.Framework;

namespace AssetManagement.Tests.Organizations
{
    /// <summary>
    /// Pure checks on the single purge plan (OrganizationPurgeSql) that OrganizationPurgeService runs.
    /// </summary>
    [TestFixture]
    public class OrganizationPurgePlanTests
    {
        [Test]
        public void Plan_IsTheServiceStatementList_OneToOne()
        {
            var steps = OrganizationPurgeSql.GetSteps();
            var statements = OrganizationPurgeSql.GetOrderedStatements();
            Assert.AreEqual(steps.Count, statements.Length);
            for (var i = 0; i < steps.Count; i++)
            {
                Assert.AreEqual(steps[i].ToSql(), statements[i]);
            }
        }

        [Test]
        public void EveryTable_IsDeletedOnce()
        {
            var tables = OrganizationPurgeSql.GetDeletedTableNames();
            var duplicates = tables.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            CollectionAssert.IsEmpty(duplicates, "Tables deleted twice: " + string.Join(", ", duplicates));
        }

        [Test]
        public void EveryStatementExceptTheLast_IsGuardedAndOrgFiltered()
        {
            var statements = OrganizationPurgeSql.GetOrderedStatements();
            for (var i = 0; i < statements.Length - 1; i++)
            {
                var sql = statements[i];
                Assert.IsTrue(sql.StartsWith("IF OBJECT_ID(", StringComparison.Ordinal) || sql.StartsWith("IF COL_LENGTH(", StringComparison.Ordinal), "Unguarded: " + sql);
                StringAssert.Contains("WHERE [OrganizationId] = @OrgId", sql);
            }
        }

        [Test]
        public void MissingTablesFromTheOldList_AreNowPurged()
        {
            var tables = OrganizationPurgeSql.GetDeletedTableNames();
            foreach (var expected in new[] { "UserInvitation", "DepartmentApprovalStage", "PurchaseRequestLine" })
            {
                CollectionAssert.Contains(tables, expected);
            }
        }

        [TestCase("UserInvitation", "Roles")]
        [TestCase("UserInvitation", "Department")]
        [TestCase("DepartmentApprovalStage", "Department")]
        [TestCase("PurchaseRequestLine", "PurchaseRequest")]
        [TestCase("PurchaseApprovalAction", "PurchaseRequest")]
        [TestCase("InsuranceClaim", "AssetIncident")]
        [TestCase("InsuranceClaim", "InsurancePolicy")]
        [TestCase("AssetDocument", "Asset")]
        [TestCase("AssetReceiving", "PurchaseRecord")]
        [TestCase("PurchaseRequest", "Asset")]
        [TestCase("AssetRequest", "Asset")]
        [TestCase("AssetSubType", "AssetType")]
        [TestCase("AssetType", "AssetCategory")]
        [TestCase("RoleTemplate", "Roles")]
        [TestCase("Users", "Roles")]
        [TestCase("Roles", "Department")]
        public void ChildIsDeletedBeforeParent(string child, string parent)
        {
            var tables = OrganizationPurgeSql.GetDeletedTableNames().ToList();
            Assert.Less(tables.IndexOf(child), tables.IndexOf(parent), child + " must be deleted before " + parent);
            Assert.GreaterOrEqual(tables.IndexOf(child), 0);
        }

        [TestCase("AssetDocument", "RequirementId", "AssetDocumentRequirement")]
        [TestCase("AssetDocumentRequirement", "DocumentId", "AssetDocument")]
        [TestCase("Department", "ClassTeacherUserId", "Users")]
        [TestCase("Department", "RoomCustodianUserId", "Users")]
        [TestCase("Users", "DepartmentId", "Department")]
        [TestCase("SupplierCatalogItem", "TaggedAssetId", "Asset")]
        public void BackwardFkColumn_IsNulledBeforeItsTargetIsDeleted(string table, string column, string target)
        {
            var steps = OrganizationPurgeSql.GetSteps();
            var nullIdx = IndexOf(steps, s => s.Kind == OrganizationPurgeSql.StepKind.NullColumnByOrganization && s.Table == table && s.Column == column);
            var deleteIdx = IndexOf(steps, s => s.Kind == OrganizationPurgeSql.StepKind.DeleteByOrganization && s.Table == target);
            Assert.GreaterOrEqual(nullIdx, 0, table + "." + column + " is never nulled");
            Assert.Less(nullIdx, deleteIdx);
        }

        [Test]
        public void BuildSafeNullColumn_UsesColLengthGuard_AndParameter()
        {
            var sql = OrganizationPurgeSql.BuildSafeNullColumn("Department", "ClassTeacherUserId");
            StringAssert.StartsWith("IF COL_LENGTH(N'Department', N'ClassTeacherUserId') IS NOT NULL", sql);
            StringAssert.Contains("UPDATE [Department] SET [ClassTeacherUserId] = NULL WHERE [OrganizationId] = @OrgId", sql);
            StringAssert.Contains("N'@OrgId int', @OrgId = @OrgId", sql);
        }

        [TestCase("Asset]; DROP TABLE [Users")]
        [TestCase("Asset'")]
        [TestCase("Asset Name")]
        public void Builders_RejectNonIdentifierNames(string name)
        {
            Assert.Throws<ArgumentException>(() => OrganizationPurgeSql.BuildSafeDelete(name));
            Assert.Throws<ArgumentException>(() => OrganizationPurgeSql.BuildSafeNullColumn("Asset", name));
        }

        /// <summary>
        /// Drift guard: every tenant entity the ORM knows about (ITenantEntity in EntityMapRegistry)
        /// must be purged or explicitly excluded.
        /// </summary>
        [Test]
        public void EveryRegisteredTenantEntityTable_IsPurgedOrExcluded()
        {
            var deleted = new HashSet<string>(OrganizationPurgeSql.GetDeletedTableNames(), StringComparer.OrdinalIgnoreCase);
            var missing = new List<string>();
            foreach (var type in typeof(ITenantEntity).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(ITenantEntity).IsAssignableFrom(t)))
            {
                string table;
                try
                {
                    table = EntityMapRegistry.GetMap(type).TableName;
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                if (!deleted.Contains(table) && !OrganizationPurgeSql.IsExcludedTable(table))
                {
                    missing.Add(type.Name + " -> [" + table + "]");
                }
            }

            CollectionAssert.IsEmpty(missing, "Tenant tables not purged: " + string.Join(", ", missing));
        }

        [Test]
        public void ServiceGuard_IntMinValue_DoesNotOpenConnection()
        {
            var service = new OrganizationPurgeService(new NeverConnectionFactory());
            var result = service.DeleteOrganizationAndData(int.MinValue);
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("Invalid organization id.", result.Message);
        }

        private sealed class NeverConnectionFactory : ISqlConnectionFactory
        {
            public SqlConnection CreateConnection()
            {
                throw new InvalidOperationException("must not connect");
            }
        }

        private static int IndexOf(IList<OrganizationPurgeSql.PurgeStep> steps, Func<OrganizationPurgeSql.PurgeStep, bool> match)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                if (match(steps[i]))
                {
                    return i;
                }
            }

            return -1;
        }
    }

    /// <summary>
    /// Schema-driven checks against a real database. Set GROKFIX_PURGE_DB to a connection string
    /// (read-only use). The destructive test additionally needs GROKFIX_PURGE_DESTRUCTIVE_ORG and only
    /// runs when DB_NAME() starts with "GrokScratch" (a throwaway restore), never on the live DB.
    /// </summary>
    [TestFixture]
    public class OrganizationPurgeSchemaTests
    {
        private string _connectionString;

        [SetUp]
        public void SetUp()
        {
            _connectionString = Environment.GetEnvironmentVariable("GROKFIX_PURGE_DB");
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                Assert.Ignore("GROKFIX_PURGE_DB not set.");
            }
        }

        [Test]
        public void EveryTableWithOrganizationIdColumn_IsPurgedOrExcluded()
        {
            var deleted = new HashSet<string>(OrganizationPurgeSql.GetDeletedTableNames(), StringComparer.OrdinalIgnoreCase);
            var missing = Query("SELECT t.name FROM sys.tables t JOIN sys.columns c ON c.object_id = t.object_id AND c.name = 'OrganizationId'")
                .Select(r => r[0])
                .Where(t => !deleted.Contains(t) && !OrganizationPurgeSql.IsExcludedTable(t))
                .ToList();
            CollectionAssert.IsEmpty(missing, "Tables with OrganizationId not purged: " + string.Join(", ", missing));
        }

        [Test]
        public void EveryLiveForeignKeyIntoAPurgedTable_IsHandledBeforeTheParentGoes()
        {
            var problems = PurgePlanForeignKeyChecker.FindUnhandled(LiveEdges());
            CollectionAssert.IsEmpty(problems, "FKs that would block the purge: " + string.Join("; ", problems));
        }

        [Test]
        public void LiveForeignKeys_MatchTheCheckedInSnapshot()
        {
            var live = new HashSet<string>(LiveEdges().Select(Key));
            var snapshot = new HashSet<string>(PurgeForeignKeySnapshot.Edges.Select(Key));
            var added = live.Except(snapshot).OrderBy(x => x).ToList();
            var removed = snapshot.Except(live).OrderBy(x => x).ToList();
            Assert.IsTrue(added.Count == 0 && removed.Count == 0,
                "Schema FKs drifted from PurgeForeignKeySnapshot (update the snapshot and the purge plan). Added: "
                + string.Join("; ", added) + " Removed: " + string.Join("; ", removed));
        }

        private static string Key(PurgeForeignKeySnapshot.ForeignKeyEdge e)
        {
            return e.Name + "|" + e.Child + "." + e.Column + "->" + e.Parent + "|" + e.DeleteAction;
        }

        private List<PurgeForeignKeySnapshot.ForeignKeyEdge> LiveEdges()
        {
            return Query(@"SELECT fk.name, OBJECT_NAME(fk.parent_object_id), COL_NAME(fkc.parent_object_id, fkc.parent_column_id),
OBJECT_NAME(fk.referenced_object_id), fk.delete_referential_action_desc
FROM sys.foreign_keys fk JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id")
                .Select(r => new PurgeForeignKeySnapshot.ForeignKeyEdge(r[0], r[1], r[2], r[3], r[4]))
                .ToList();
        }

        [Test]
        public void Purge_OnThrowawayDatabase_RemovesEveryOrgRow_AndLeavesOtherOrgs()
        {
            int orgId;
            if (!int.TryParse(Environment.GetEnvironmentVariable("GROKFIX_PURGE_DESTRUCTIVE_ORG"), out orgId) || orgId <= 0)
            {
                Assert.Ignore("GROKFIX_PURGE_DESTRUCTIVE_ORG not set.");
            }

            var dbName = Query("SELECT DB_NAME()")[0][0];
            if (!dbName.StartsWith("GrokScratch", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail("Refusing to purge: database '" + dbName + "' is not a GrokScratch throwaway copy.");
            }

            var tables = Query("SELECT t.name FROM sys.tables t JOIN sys.columns c ON c.object_id = t.object_id AND c.name = 'OrganizationId'")
                .Select(r => r[0]).Where(t => !t.StartsWith("Bak_", StringComparison.OrdinalIgnoreCase)).ToList();
            var before = CountByOrg(tables, orgId);
            Assert.Greater(before.Item1, 0, "Org has no rows; nothing to prove.");

            var result = new OrganizationPurgeService(new FixedConnectionFactory(_connectionString)).DeleteOrganizationAndData(orgId);
            Assert.IsTrue(result.Succeeded, result.Message);

            var after = CountByOrg(tables, orgId);
            Assert.AreEqual(0, after.Item1, "Rows left for the purged org.");
            Assert.AreEqual(before.Item2, after.Item2, "Rows of other organisations changed.");
            Assert.AreEqual("0", Query("SELECT COUNT(*) FROM [Organization] WHERE [Id] = " + orgId)[0][0]);
            Console.WriteLine("Purged org " + orgId + " in " + dbName + ": " + before.Item1 + " org rows removed, " + before.Item2 + " other-org rows unchanged.");
        }

        private Tuple<long, long> CountByOrg(IEnumerable<string> tables, int orgId)
        {
            long mine = 0, others = 0;
            foreach (var t in tables)
            {
                var r = Query("SELECT SUM(CASE WHEN [OrganizationId] = " + orgId + " THEN 1 ELSE 0 END), SUM(CASE WHEN [OrganizationId] = " + orgId + " THEN 0 ELSE 1 END) FROM [" + t + "]")[0];
                mine += string.IsNullOrEmpty(r[0]) ? 0 : long.Parse(r[0]);
                others += string.IsNullOrEmpty(r[1]) ? 0 : long.Parse(r[1]);
            }

            return Tuple.Create(mine, others);
        }

        private List<string[]> Query(string sql)
        {
            var rows = new List<string[]>();
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = new string[reader.FieldCount];
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            row[i] = reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i));
                        }

                        rows.Add(row);
                    }
                }
            }

            return rows;
        }

        private sealed class FixedConnectionFactory : ISqlConnectionFactory
        {
            private readonly string _connectionString;

            public FixedConnectionFactory(string connectionString)
            {
                _connectionString = connectionString;
            }

            public SqlConnection CreateConnection()
            {
                return new SqlConnection(_connectionString);
            }
        }
    }

    /// <summary>
    /// A NO ACTION foreign key child.column -> parent is safe for the purge when the parent is never
    /// deleted, or the child rows are deleted first, or the column is nulled first, or it is a self
    /// reference (removed inside one DELETE). CASCADE / SET NULL keys are handled by SQL Server.
    /// </summary>
    public static class PurgePlanForeignKeyChecker
    {
        public static List<string> FindUnhandled(IEnumerable<PurgeForeignKeySnapshot.ForeignKeyEdge> edges)
        {
            var steps = OrganizationPurgeSql.GetSteps();
            var problems = new List<string>();
            foreach (var fk in edges)
            {
                var parentIdx = DeleteIndex(steps, fk.Parent);
                if (parentIdx < 0 || fk.DeleteAction != "NO_ACTION" || string.Equals(fk.Child, fk.Parent, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var childIdx = DeleteIndex(steps, fk.Child);
                var nulledIdx = NullIndex(steps, fk.Child, fk.Column);
                if (!((childIdx >= 0 && childIdx < parentIdx) || (nulledIdx >= 0 && nulledIdx < parentIdx)))
                {
                    problems.Add(fk.Name + " (" + fk.Child + "." + fk.Column + " -> " + fk.Parent + ")");
                }
            }

            return problems;
        }

        public static int DeleteIndex(IList<OrganizationPurgeSql.PurgeStep> steps, string table)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                if (string.Equals(s.Table, table, StringComparison.OrdinalIgnoreCase)
                    && (s.Kind == OrganizationPurgeSql.StepKind.DeleteByOrganization || s.Kind == OrganizationPurgeSql.StepKind.DeleteOrganizationRow))
                {
                    return i;
                }
            }

            return -1;
        }

        public static int NullIndex(IList<OrganizationPurgeSql.PurgeStep> steps, string table, string column)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                if (s.Kind == OrganizationPurgeSql.StepKind.NullColumnByOrganization
                    && string.Equals(s.Table, table, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(s.Column, column, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }
    }

    /// <summary>
    /// Offline FK checks against the checked-in snapshot: any table that references a purged table
    /// (Organization, Users, Roles, Department, Asset, ...) must be deleted or have the column nulled first.
    /// </summary>
    [TestFixture]
    public class OrganizationPurgeForeignKeyTests
    {
        [Test]
        public void EverySnapshotForeignKey_IsHandledByThePlan()
        {
            var problems = PurgePlanForeignKeyChecker.FindUnhandled(PurgeForeignKeySnapshot.Edges);
            CollectionAssert.IsEmpty(problems, "FKs that would block the purge: " + string.Join("; ", problems));
        }

        [TestCase("Organization")]
        [TestCase("Users")]
        [TestCase("Roles")]
        [TestCase("Department")]
        [TestCase("Asset")]
        public void EveryTableReferencing_IsInThePlan(string parent)
        {
            var steps = OrganizationPurgeSql.GetSteps();
            var children = PurgeForeignKeySnapshot.Edges.Where(e => e.Parent == parent && e.DeleteAction == "NO_ACTION" && e.Child != parent).ToList();
            Assert.IsNotEmpty(children, "Snapshot has no FKs into " + parent);
            var missing = children
                .Where(e => PurgePlanForeignKeyChecker.DeleteIndex(steps, e.Child) < 0 && PurgePlanForeignKeyChecker.NullIndex(steps, e.Child, e.Column) < 0)
                .Select(e => e.Child + "." + e.Column)
                .ToList();
            CollectionAssert.IsEmpty(missing, "Tables referencing " + parent + " missing from the purge plan: " + string.Join(", ", missing));
        }

        [Test]
        public void Checker_FlagsAnUnhandledReference()
        {
            var fake = new[] { new PurgeForeignKeySnapshot.ForeignKeyEdge("FK_Fake", "NotInPlan", "AssetId", "Asset", "NO_ACTION") };
            CollectionAssert.AreEqual(new[] { "FK_Fake (NotInPlan.AssetId -> Asset)" }, PurgePlanForeignKeyChecker.FindUnhandled(fake));
        }

        [Test]
        public void Checker_IgnoresCascadeAndSelfReferences()
        {
            var edges = new[]
            {
                new PurgeForeignKeySnapshot.ForeignKeyEdge("FK_C", "PasswordResetToken", "UserId", "Users", "CASCADE"),
                new PurgeForeignKeySnapshot.ForeignKeyEdge("FK_S", "Department", "ParentDepartmentId", "Department", "NO_ACTION")
            };
            CollectionAssert.IsEmpty(PurgePlanForeignKeyChecker.FindUnhandled(edges));
        }
    }
}

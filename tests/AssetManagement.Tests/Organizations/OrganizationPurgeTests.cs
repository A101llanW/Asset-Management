using System;
using System.Data.SqlClient;
using System.Linq;
using AssetManagement.Application.Helpers;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Services;
using NUnit.Framework;

namespace AssetManagement.Tests.Organizations
{
    [TestFixture]
    public class OrganizationPurgeConfirmationTests
    {
        [Test]
        public void IsValidOrganizationId_RejectsZeroAndNegative()
        {
            Assert.IsFalse(OrganizationPurgeConfirmation.IsValidOrganizationId(0));
            Assert.IsFalse(OrganizationPurgeConfirmation.IsValidOrganizationId(-1));
            Assert.IsFalse(OrganizationPurgeConfirmation.IsValidOrganizationId(int.MinValue));
        }

        [Test]
        public void IsValidOrganizationId_AcceptsPositive()
        {
            Assert.IsTrue(OrganizationPurgeConfirmation.IsValidOrganizationId(1));
            Assert.IsTrue(OrganizationPurgeConfirmation.IsValidOrganizationId(42));
        }

        [Test]
        public void MatchesTypedName_RequiresExactName_IgnoringCaseAndWhitespace()
        {
            Assert.IsTrue(OrganizationPurgeConfirmation.MatchesTypedName("Acme Corp", "Acme Corp"));
            Assert.IsTrue(OrganizationPurgeConfirmation.MatchesTypedName("Acme Corp", "  acme corp  "));
            Assert.IsFalse(OrganizationPurgeConfirmation.MatchesTypedName("Acme Corp", "Acme"));
            Assert.IsFalse(OrganizationPurgeConfirmation.MatchesTypedName("Acme Corp", ""));
            Assert.IsFalse(OrganizationPurgeConfirmation.MatchesTypedName("Acme Corp", "   "));
            Assert.IsFalse(OrganizationPurgeConfirmation.MatchesTypedName(null, "Acme Corp"));
            Assert.IsFalse(OrganizationPurgeConfirmation.MatchesTypedName("Acme Corp", null));
        }

        [Test]
        public void IsImpersonatingTarget_BlocksSameOrgOnly()
        {
            Assert.IsTrue(OrganizationPurgeConfirmation.IsImpersonatingTarget(7, 7));
            Assert.IsFalse(OrganizationPurgeConfirmation.IsImpersonatingTarget(7, 8));
            Assert.IsFalse(OrganizationPurgeConfirmation.IsImpersonatingTarget(7, null));
        }
    }

    [TestFixture]
    public class OrganizationPurgeSqlTests
    {
        [Test]
        public void BuildSafeDelete_WrapsObjectIdGuard_AndOrgFilter()
        {
            var sql = OrganizationPurgeSql.BuildSafeDelete("Asset");
            StringAssert.Contains("IF OBJECT_ID(N'[Asset]', N'U') IS NOT NULL", sql);
            StringAssert.Contains("DELETE FROM [Asset] WHERE [OrganizationId] = @OrgId", sql);
        }

        [Test]
        public void BuildSafeDelete_NullOrBlank_Throws()
        {
            Assert.Throws<ArgumentException>(() => OrganizationPurgeSql.BuildSafeDelete(null));
            Assert.Throws<ArgumentException>(() => OrganizationPurgeSql.BuildSafeDelete("  "));
        }

        [Test]
        public void GetOrderedStatements_EndsWithOrganizationDelete()
        {
            var statements = OrganizationPurgeSql.GetOrderedStatements();
            Assert.Greater(statements.Length, 10);
            Assert.AreEqual(statements.Length - 1, OrganizationPurgeSql.OrganizationDeleteIndex);
            Assert.AreEqual("DELETE FROM [Organization] WHERE [Id] = @OrgId", statements[statements.Length - 1]);
        }

        [Test]
        public void GetOrderedStatements_DeletesChildrenBeforeParents()
        {
            var statements = OrganizationPurgeSql.GetOrderedStatements();
            var assetIdx = IndexOfTable(statements, "Asset");
            var assetTypeIdx = IndexOfTable(statements, "AssetType");
            var usersIdx = IndexOfTable(statements, "Users");
            var rolesIdx = IndexOfTable(statements, "Roles");
            var departmentIdx = IndexOfTable(statements, "Department");
            var orgIdx = OrganizationPurgeSql.OrganizationDeleteIndex;

            Assert.Less(assetIdx, assetTypeIdx, "Asset rows before AssetType");
            Assert.Less(usersIdx, rolesIdx, "Users before Roles (FK clear + delete)");
            Assert.Less(rolesIdx, departmentIdx, "Roles before Department");
            Assert.Less(departmentIdx, orgIdx, "Department before Organization");
        }

        [Test]
        public void GetOrderedStatements_ClearsUserFks_BeforeDeletingUsers()
        {
            var statements = OrganizationPurgeSql.GetOrderedStatements();
            var clearIdx = Array.FindIndex(statements, s => s.IndexOf("UPDATE [Users]", StringComparison.OrdinalIgnoreCase) >= 0
                && s.IndexOf("[DepartmentId] = NULL", StringComparison.OrdinalIgnoreCase) >= 0);
            var deleteUsersIdx = IndexOfTable(statements, "Users");
            Assert.GreaterOrEqual(clearIdx, 0);
            Assert.Less(clearIdx, deleteUsersIdx);
        }

        [Test]
        public void GetOrderedStatements_AllSafeDeletes_UseOrgIdParameter()
        {
            var statements = OrganizationPurgeSql.GetOrderedStatements();
            foreach (var sql in statements)
            {
                StringAssert.Contains("@OrgId", sql);
            }
        }

        private static int IndexOfTable(string[] statements, string tableName)
        {
            var needle = "DELETE FROM [" + tableName + "]";
            for (var i = 0; i < statements.Length; i++)
            {
                if (statements[i].IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return i;
                }
            }

            Assert.Fail("Missing delete for table " + tableName);
            return -1;
        }
    }

    [TestFixture]
    public class OrganizationPurgeServiceGuardTests
    {
        private sealed class ThrowingConnectionFactory : ISqlConnectionFactory
        {
            public bool WasCalled { get; private set; }

            public SqlConnection CreateConnection()
            {
                WasCalled = true;
                throw new InvalidOperationException("CreateConnection must not run for invalid org id.");
            }
        }

        [Test]
        public void DeleteOrganizationAndData_InvalidId_DoesNotOpenConnection()
        {
            var factory = new ThrowingConnectionFactory();
            var service = new OrganizationPurgeService(factory);

            var zero = service.DeleteOrganizationAndData(0);
            Assert.IsFalse(zero.Succeeded);
            Assert.AreEqual("Invalid organization id.", zero.Message);
            Assert.IsFalse(factory.WasCalled);

            var negative = service.DeleteOrganizationAndData(-3);
            Assert.IsFalse(negative.Succeeded);
            Assert.AreEqual("Invalid organization id.", negative.Message);
            Assert.IsFalse(factory.WasCalled);
        }
    }
}

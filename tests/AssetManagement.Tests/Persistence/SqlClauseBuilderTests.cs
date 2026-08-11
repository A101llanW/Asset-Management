using AssetManagement.Infrastructure.Persistence;
using NUnit.Framework;

namespace AssetManagement.Tests.Persistence
{
    [TestFixture]
    public class SqlClauseBuilderTests
    {
        [Test]
        public void AppendCondition_AddsWhere_WhenQueryHasNoWhereClause()
        {
            var sql = "SELECT * FROM [Asset]";
            SqlClauseBuilder.AppendCondition(ref sql, "[OrganizationId]=@OrganizationId");

            Assert.AreEqual("SELECT * FROM [Asset] WHERE [OrganizationId]=@OrganizationId", sql);
        }

        [Test]
        public void AppendCondition_AddsAnd_WhenQueryAlreadyHasWhereClause()
        {
            var sql = "SELECT * FROM [Asset] WHERE [Id]=@Id";
            SqlClauseBuilder.AppendCondition(ref sql, "[OrganizationId]=@OrganizationId");

            Assert.AreEqual("SELECT * FROM [Asset] WHERE [Id]=@Id AND [OrganizationId]=@OrganizationId", sql);
        }

        [Test]
        public void AppendCondition_AddsWhereDenyAll_WhenQueryHasNoWhereClause()
        {
            var sql = "SELECT * FROM [DepreciationRecord]";
            SqlClauseBuilder.AppendCondition(ref sql, "1=0");

            Assert.AreEqual("SELECT * FROM [DepreciationRecord] WHERE 1=0", sql);
        }

        [Test]
        public void AppendCondition_AddsAndDenyAll_WhenQueryAlreadyHasWhereClause()
        {
            var sql = "SELECT * FROM [DepreciationRecord] WHERE [Id]=@Id";
            SqlClauseBuilder.AppendCondition(ref sql, "1=0");

            Assert.AreEqual("SELECT * FROM [DepreciationRecord] WHERE [Id]=@Id AND 1=0", sql);
        }

        [Test]
        public void HasWhereClause_IsCaseInsensitive()
        {
            Assert.IsTrue(SqlClauseBuilder.HasWhereClause("SELECT 1 FROM [Asset] where [Id]=1"));
        }
    }
}

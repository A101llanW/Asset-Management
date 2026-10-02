using System.Collections.Generic;
using System.Linq;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using NUnit.Framework;

namespace AssetManagement.Tests.Persistence
{
    [TestFixture]
    public class SqlPredicateBuilderTests
    {
        [Test]
        public void Build_TranslatesClosureListContainsToInClause()
        {
            var permissionIds = new List<int> { 10, 20, 30 };
            var map = EntityMapRegistry.GetMap<Permission>();

            var predicate = SqlPredicateBuilder.Build(
                (Permission x) => permissionIds.Contains(x.Id),
                map);

            StringAssert.Contains("[Id] IN (@p0,@p1,@p2)", predicate.Sql);
            CollectionAssert.AreEquivalent(
                new[] { 10, 20, 30 },
                predicate.Parameters.Select(parameter => parameter.Value).ToArray());
        }

        [Test]
        public void Build_TranslatesStaticEnumerableContainsToInClause()
        {
            var ids = new[] { 5, 6 };
            var map = EntityMapRegistry.GetMap<RolePermission>();

            var predicate = SqlPredicateBuilder.Build(
                (RolePermission x) => ids.Contains(x.RoleId),
                map);

            StringAssert.Contains("[RoleId] IN (@p0,@p1)", predicate.Sql);
            CollectionAssert.AreEquivalent(
                new[] { 5, 6 },
                predicate.Parameters.Select(parameter => parameter.Value).ToArray());
        }

        [Test]
        public void Build_EmptyContainsList_ProducesFalsePredicate()
        {
            var permissionIds = new List<int>();
            var map = EntityMapRegistry.GetMap<Permission>();

            var predicate = SqlPredicateBuilder.Build(
                (Permission x) => permissionIds.Contains(x.Id),
                map);

            Assert.AreEqual("1=0", predicate.Sql);
            Assert.AreEqual(0, predicate.Parameters.Count);
        }
    }
}

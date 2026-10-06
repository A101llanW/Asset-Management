using System.Collections.Generic;
using System.Linq;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using NUnit.Framework;

namespace AssetManagement.Tests.Persistence
{
    [TestFixture]
    public class SqlPredicateBuilderInstanceContainsTests
    {
        [Test]
        public void Build_TranslatesClosureHashSetContainsToInClause()
        {
            var ids = new HashSet<int> { 7, 8 };
            var map = EntityMapRegistry.GetMap<Permission>();

            var predicate = SqlPredicateBuilder.Build((Permission x) => ids.Contains(x.Id), map);

            StringAssert.Contains("[Id] IN (@p0,@p1)", predicate.Sql);
            CollectionAssert.AreEquivalent(new[] { 7, 8 }, predicate.Parameters.Select(p => p.Value).ToArray());
        }

        [Test]
        public void Build_TranslatesClosureListContainsCombinedWithOtherPredicates()
        {
            var roleIds = new List<int> { 140, 145 };
            var map = EntityMapRegistry.GetMap<RolePermission>();

            var predicate = SqlPredicateBuilder.Build(
                (RolePermission x) => x.PermissionId > 0 && roleIds.Contains(x.RoleId),
                map);

            StringAssert.Contains("[PermissionId]>@p0", predicate.Sql);
            StringAssert.Contains("[RoleId] IN (@p1,@p2)", predicate.Sql);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using NUnit.Framework;

namespace AssetManagement.Tests.Persistence
{
    [TestFixture]
    public class SqlQueryableTests
    {
        [Test]
        public void ToList_OnRootQuery_ReturnsRowsWithoutTypeMismatch()
        {
            var rows = new List<AssetType>
            {
                new AssetType { Id = 1, Name = "Laptop" }
            };

            var query = new SqlQueryable<AssetType>(expression =>
                SqlQueryableExpressionHelper.ExecuteInMemory(expression, rows));

            var result = query.ToList();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Laptop", result[0].Name);
        }

        [Test]
        public void Where_ToList_FiltersInMemoryRows()
        {
            var rows = new List<AssetType>
            {
                new AssetType { Id = 1, Name = "Laptop" },
                new AssetType { Id = 2, Name = "Desktop" }
            };

            var query = new SqlQueryable<AssetType>(expression =>
                SqlQueryableExpressionHelper.ExecuteInMemory(expression, rows));

            var result = query.Where(x => x.Name == "Desktop").ToList();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(2, result[0].Id);
        }

        [Test]
        public void OrderBy_ToList_SortsInMemoryRows()
        {
            var rows = new List<Department>
            {
                new Department { Id = 2, Name = "Zebra" },
                new Department { Id = 1, Name = "Alpha" }
            };

            var query = new SqlQueryable<Department>(expression =>
                SqlQueryableExpressionHelper.ExecuteInMemory(expression, rows));

            var result = query.OrderBy(x => x.Name).ToList();

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("Alpha", result[0].Name);
            Assert.AreEqual("Zebra", result[1].Name);
        }

        [Test]
        public void Count_OnRootQuery_ReturnsRowCount()
        {
            var rows = new List<AssetType>
            {
                new AssetType { Id = 1, Name = "Laptop" },
                new AssetType { Id = 2, Name = "Desktop" }
            };

            var query = new SqlQueryable<AssetType>(expression =>
                SqlQueryableExpressionHelper.ExecuteInMemory(expression, rows));

            Assert.AreEqual(2, query.Count());
        }
    }
}

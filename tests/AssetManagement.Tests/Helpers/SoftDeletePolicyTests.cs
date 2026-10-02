using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Entities;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class SoftDeletePolicyTests
    {
        [Test]
        public void MarkInactive_SetsIsActiveFalse_AndUpdatedAt()
        {
            var entity = new Asset { IsActive = true, UpdatedAt = null };
            SoftDeletePolicy.MarkInactive(entity);
            Assert.IsFalse(entity.IsActive);
            Assert.IsTrue(entity.UpdatedAt.HasValue);
        }

        [Test]
        public void MarkInactive_Null_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => SoftDeletePolicy.MarkInactive(null));
        }

        [Test]
        public void IsActiveEntity_Null_IsFalse()
        {
            Assert.IsFalse(SoftDeletePolicy.IsActiveEntity(null));
        }

        [Test]
        public void EnsureActive_Inactive_ThrowsBusinessException()
        {
            var entity = new InsurancePolicy { IsActive = false };
            var ex = Assert.Throws<BusinessException>(() => SoftDeletePolicy.EnsureActive(entity, "Insurance policy not found."));
            Assert.AreEqual("Insurance policy not found.", ex.Message);
        }

        [Test]
        public void WhereActive_FiltersQueryable()
        {
            var items = new List<Supplier>
            {
                new Supplier { Id = 1, IsActive = true, SupplierName = "A" },
                new Supplier { Id = 2, IsActive = false, SupplierName = "B" },
                new Supplier { Id = 3, IsActive = true, SupplierName = "C" }
            }.AsQueryable();

            var active = SoftDeletePolicy.WhereActive(items).ToList();
            Assert.AreEqual(2, active.Count);
            Assert.IsTrue(active.All(x => x.IsActive));
        }

        [Test]
        public void WhereActive_FiltersEnumerable_SkipsNull()
        {
            var items = new List<Department>
            {
                new Department { Id = 1, IsActive = true },
                null,
                new Department { Id = 2, IsActive = false }
            };

            var active = SoftDeletePolicy.WhereActive(items).ToList();
            Assert.AreEqual(1, active.Count);
            Assert.AreEqual(1, active[0].Id);
        }

        [Test]
        public void WhereActiveFlag_FiltersVmStyle()
        {
            var rows = new[]
            {
                new { Id = 1, IsActive = true },
                new { Id = 2, IsActive = false }
            };

            var active = SoftDeletePolicy.WhereActiveFlag(rows, x => x.IsActive).ToList();
            Assert.AreEqual(1, active.Count);
            Assert.AreEqual(1, active[0].Id);
        }

        [Test]
        public void AuditMarker_IsSoftDeleted()
        {
            Assert.AreEqual("SoftDeleted", SoftDeletePolicy.AuditMarker);
        }
    }
}

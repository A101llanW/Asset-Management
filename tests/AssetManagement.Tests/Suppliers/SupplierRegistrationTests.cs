using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Suppliers
{
    [TestFixture]
    public class SupplierRegistrationTests
    {
        [Test]
        public void CreateWithCatalog_requires_at_least_one_catalog_line()
        {
            var unitOfWork = new FakeUnitOfWork();
            var catalogService = new SupplierCatalogService(unitOfWork, new FakeOrganizationScopeService());
            var service = new SupplierService(unitOfWork, catalogService);

            var ex = Assert.Throws<BusinessException>(() => service.CreateWithCatalog(
                new SupplierVm { SupplierName = "Vendor A", IsActive = true },
                new List<SupplierCatalogItemVm>()));

            Assert.That(ex.Message.Contains("supply item"), Is.True);
        }

        [Test]
        public void CreateWithCatalog_persists_supplier_and_catalog_items()
        {
            var unitOfWork = new FakeUnitOfWork();
            var catalogService = new SupplierCatalogService(unitOfWork, new FakeOrganizationScopeService());
            var service = new SupplierService(unitOfWork, catalogService);

            var supplierId = service.CreateWithCatalog(
                new SupplierVm
                {
                    SupplierName = "Catalog Vendor",
                    Phone = "+254700000099",
                    IsActive = true
                },
                new List<SupplierCatalogItemVm>
                {
                    new SupplierCatalogItemVm
                    {
                        ItemName = "Laptop",
                        UnitPrice = 1200m,
                        Currency = "KES",
                        AssetCategoryId = 1
                    }
                });

            Assert.That(supplierId, Is.GreaterThan(0));
            var items = catalogService.GetBySupplier(supplierId).ToList();
            Assert.That(items.Count, Is.EqualTo(1));
            Assert.That(items[0].ItemName, Is.EqualTo("Laptop"));
            Assert.That(items[0].UnitPrice, Is.EqualTo(1200m));
        }
    }
}

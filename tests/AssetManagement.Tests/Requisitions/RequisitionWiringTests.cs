using System;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace AssetManagement.Tests.Requisitions
{
    [TestFixture]
    public class RequisitionWiringTests
    {
        [Test]
        public void SupplierCatalogService_GetPriceComparison_OrdersByPriceAndMarksMinMax()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Supplier { Id = 1, SupplierName = "Cheap Co", IsActive = true, OrganizationId = 1 });
            unitOfWork.Seed(new Supplier { Id = 2, SupplierName = "Premium Co", IsActive = true, OrganizationId = 1, IsPreferred = true });
            unitOfWork.Seed(new SupplierCatalogItem
            {
                Id = 10,
                SupplierId = 1,
                ItemName = "A4 paper ream",
                ItemDescription = "A4 white copy paper 500 sheets",
                UnitPrice = 4.50m,
                Currency = "KES",
                IsActive = true,
                OrganizationId = 1,
                CreatedAt = DateTime.UtcNow
            });
            unitOfWork.Seed(new SupplierCatalogItem
            {
                Id = 11,
                SupplierId = 2,
                ItemName = "A4 paper ream",
                ItemDescription = "A4 white copy paper premium",
                UnitPrice = 6.25m,
                Currency = "KES",
                IsActive = true,
                OrganizationId = 1,
                CreatedAt = DateTime.UtcNow
            });

            var service = new SupplierCatalogService(unitOfWork, new FakeOrganizationScopeService(organizationId: 1));
            var result = service.GetPriceComparison(null, "A4 paper ream");

            Assert.AreEqual(2, result.Rows.Count);
            Assert.IsTrue(result.HasCatalogMatches);
            Assert.AreEqual(1, result.Rows[0].SupplierId);
            Assert.IsTrue(result.Rows[0].IsCheapest);
            Assert.IsTrue(result.Rows[1].IsMostExpensive);
            Assert.IsTrue(result.Rows[1].IsPreferred);
        }

        [Test]
        public void SupplierCatalogService_GetPriceComparison_ExcludesInactiveOffers()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Supplier { Id = 1, SupplierName = "Active Supplier", IsActive = true, OrganizationId = 1 });
            unitOfWork.Seed(new SupplierCatalogItem
            {
                Id = 1,
                SupplierId = 1,
                ItemName = "Stapler",
                ItemDescription = "Desktop stapler",
                UnitPrice = 12m,
                IsActive = false,
                OrganizationId = 1,
                CreatedAt = DateTime.UtcNow
            });

            var service = new SupplierCatalogService(unitOfWork, new FakeOrganizationScopeService(organizationId: 1));
            var result = service.GetPriceComparison(null, "Stapler");

            Assert.AreEqual(0, result.Rows.Count);
            Assert.IsFalse(result.HasCatalogMatches);
        }

        [Test]
        public void PurchaseRequestService_Submit_PersistsRequisitionFormFields()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department { Id = 3, Name = "HR", Code = "HR", IsActive = true, IsRequisitionTarget = true });
            unitOfWork.Seed(new SystemSetting
            {
                Id = 1,
                SettingKey = "Approval.Purchase.Enabled",
                SettingValue = "false",
                IsActive = true
            });

            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "dept-head", DepartmentId = 3, IsActive = true, FirstName = "Grace", LastName = "Head" });
            users.Seed(new UserVm { Id = "staff-1", DepartmentId = 3, IsActive = true, FirstName = "Sam", LastName = "Staff" });

            var service = new PurchaseRequestService(
                unitOfWork,
                new NoOpAuditWriter(),
                users,
                new NoOpDepartmentScopeService(),
                new FakeOrganizationScopeService(organizationId: 1),
                new NoOpOutboxWriter(),
                new NoOpWebhookService(),
                new FakeApprovalWorkflowEngine(unitOfWork, new NoOpAuditWriter()),
                new FakeOperationsQueryRepository());

            var requiredDate = DateTime.UtcNow.Date.AddDays(14);
            var id = service.Submit(new PurchaseRequestCreateVm
            {
                DepartmentId = 3,
                ItemDescription = "Ergonomic office chair",
                Justification = "Replace broken chairs in HR",
                QuantityInStock = 2,
                RequiredDate = requiredDate,
                OrderByUserId = "staff-1",
                Quantity = 5,
                Currency = "KES"
            }, "dept-head");

            var entity = unitOfWork.Repository<PurchaseRequest>().GetById(id);
            Assert.AreEqual("Ergonomic office chair", entity.ItemDescription);
            Assert.AreEqual(2, entity.QuantityInStock);
            Assert.AreEqual(requiredDate, entity.RequiredDate);
            Assert.AreEqual("staff-1", entity.OrderByUserId);
            Assert.AreEqual(ApprovalStatus.Approved, entity.ApprovalStatus);
        }

        [Test]
        public void PurchaseRequestService_Submit_RejectsOrderByUserOutsideDepartment()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department { Id = 3, Name = "HR", Code = "HR", IsActive = true, IsRequisitionTarget = true });
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "staff-other", DepartmentId = 9, IsActive = true, FirstName = "Other", LastName = "Dept" });

            var service = new PurchaseRequestService(
                unitOfWork,
                new NoOpAuditWriter(),
                users,
                new NoOpDepartmentScopeService(),
                new FakeOrganizationScopeService(organizationId: 1),
                new NoOpOutboxWriter(),
                new NoOpWebhookService(),
                new FakeApprovalWorkflowEngine(unitOfWork, new NoOpAuditWriter()),
                new FakeOperationsQueryRepository());

            Assert.Throws<BusinessException>(() => service.Submit(new PurchaseRequestCreateVm
            {
                DepartmentId = 3,
                ItemDescription = "Desk",
                Justification = "Need desk",
                OrderByUserId = "staff-other",
                Quantity = 1,
                Currency = "KES"
            }, "dept-head"));
        }

        [Test]
        public void PurchaseRequestService_Submit_PersistsOptionalTargetAsset()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department { Id = 3, Name = "HR", Code = "HR", IsActive = true, IsRequisitionTarget = true });
            unitOfWork.Seed(new SystemSetting
            {
                Id = 1,
                SettingKey = "Approval.Purchase.Enabled",
                SettingValue = "false",
                IsActive = true
            });
            unitOfWork.Seed(new Asset
            {
                Id = 42,
                AssetTag = "LAP-001",
                AssetName = "Dell Laptop",
                DepartmentId = 3,
                CategoryId = 1,
                AssetTypeId = 1,
                IsActive = true,
                OrganizationId = 1,
                CurrentStatus = AssetStatus.Assigned,
                CreatedAt = DateTime.UtcNow
            });

            var service = new PurchaseRequestService(
                unitOfWork,
                new NoOpAuditWriter(),
                new FakeUserService(),
                new NoOpDepartmentScopeService(),
                new FakeOrganizationScopeService(organizationId: 1),
                new NoOpOutboxWriter(),
                new NoOpWebhookService(),
                new FakeApprovalWorkflowEngine(unitOfWork, new NoOpAuditWriter()),
                new FakeOperationsQueryRepository());

            var id = service.Submit(new PurchaseRequestCreateVm
            {
                DepartmentId = 3,
                ItemDescription = "Replacement laptop",
                Justification = "Broken unit",
                TargetAssetId = 42,
                Quantity = 1,
                Currency = "KES"
            }, "dept-head");

            var entity = unitOfWork.Repository<PurchaseRequest>().GetById(id);
            Assert.AreEqual(42, entity.TargetAssetId);
        }

        [Test]
        public void PurchaseRequestService_Submit_AllowsTargetAssetOutsideRequestDepartment()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department { Id = 3, Name = "HR", Code = "HR", IsActive = true, IsRequisitionTarget = true });
            unitOfWork.Seed(new SystemSetting
            {
                Id = 1,
                SettingKey = "Approval.Purchase.Enabled",
                SettingValue = "false",
                IsActive = true
            });
            unitOfWork.Seed(new Asset
            {
                Id = 50,
                AssetTag = "IT-001",
                AssetName = "Server",
                DepartmentId = 9,
                CategoryId = 1,
                AssetTypeId = 1,
                IsActive = true,
                OrganizationId = 1,
                CurrentStatus = AssetStatus.InStore,
                CreatedAt = DateTime.UtcNow
            });

            var service = new PurchaseRequestService(
                unitOfWork,
                new NoOpAuditWriter(),
                new FakeUserService(),
                new NoOpDepartmentScopeService(),
                new FakeOrganizationScopeService(organizationId: 1),
                new NoOpOutboxWriter(),
                new NoOpWebhookService(),
                new FakeApprovalWorkflowEngine(unitOfWork, new NoOpAuditWriter()),
                new FakeOperationsQueryRepository());

            var id = service.Submit(new PurchaseRequestCreateVm
            {
                DepartmentId = 3,
                ItemDescription = "Server",
                Justification = "Need server",
                TargetAssetId = 50,
                Quantity = 1,
                Currency = "KES"
            }, "dept-head");

            Assert.AreEqual(50, unitOfWork.Repository<PurchaseRequest>().GetById(id).TargetAssetId);
        }

        [Test]
        public void PurchaseRequestService_Submit_PersistsMultipleLineItems()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department { Id = 3, Name = "HR", Code = "HR", IsActive = true, IsRequisitionTarget = true });
            unitOfWork.Seed(new SystemSetting
            {
                Id = 1,
                SettingKey = "Approval.Purchase.Enabled",
                SettingValue = "false",
                IsActive = true
            });

            var service = new PurchaseRequestService(
                unitOfWork,
                new NoOpAuditWriter(),
                new FakeUserService(),
                new NoOpDepartmentScopeService(),
                new FakeOrganizationScopeService(organizationId: 1),
                new NoOpOutboxWriter(),
                new NoOpWebhookService(),
                new FakeApprovalWorkflowEngine(unitOfWork, new NoOpAuditWriter()),
                new FakeOperationsQueryRepository());

            var id = service.Submit(new PurchaseRequestCreateVm
            {
                DepartmentId = 3,
                Justification = "Class supplies",
                Currency = "KES",
                Lines = new List<PurchaseRequestLineCreateVm>
                {
                    new PurchaseRequestLineCreateVm { Description = "Markers", Quantity = 2 },
                    new PurchaseRequestLineCreateVm { Description = "Chart paper", Quantity = 5 }
                }
            }, "dept-head");

            var entity = unitOfWork.Repository<PurchaseRequest>().GetById(id);
            Assert.AreEqual(7, entity.Quantity);
            Assert.IsTrue(entity.ItemDescription.Contains("Markers"));
            Assert.IsTrue(entity.ItemDescription.Contains("Chart paper"));

            var lines = unitOfWork.Repository<PurchaseRequestLine>()
                .Find(x => x.PurchaseRequestId == id)
                .OrderBy(x => x.LineNumber)
                .ToList();
            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual("Markers", lines[0].Description);
            Assert.AreEqual(2, lines[0].Quantity);
            Assert.AreEqual("Chart paper", lines[1].Description);
            Assert.AreEqual(5, lines[1].Quantity);
        }

        [Test]
        public void PurchaseRequestService_Submit_AllowsNullHomeDepartmentWithValidRequisitionTarget()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department { Id = 3, Name = "Grade 4B", Code = "G4B", IsActive = true, IsRequisitionTarget = true });
            unitOfWork.Seed(new SystemSetting
            {
                Id = 1,
                SettingKey = "Approval.Purchase.Enabled",
                SettingValue = "false",
                IsActive = true
            });

            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "staff-null-dept", DepartmentId = null, IsActive = true, RoleId = 5, FirstName = "Staff", LastName = "NoDept" });
            unitOfWork.Seed(new Role { Id = 5, Name = "Staff", IsSystemRole = false, IsActive = true });

            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.Setup(x => x.UserId).Returns("staff-null-dept");

            var departmentScope = new DepartmentScopeService(
                unitOfWork,
                currentUser.Object,
                users,
                new FakeOrganizationScopeService(organizationId: 1));

            var service = new PurchaseRequestService(
                unitOfWork,
                new NoOpAuditWriter(),
                users,
                departmentScope,
                new FakeOrganizationScopeService(organizationId: 1),
                new NoOpOutboxWriter(),
                new NoOpWebhookService(),
                new FakeApprovalWorkflowEngine(unitOfWork, new NoOpAuditWriter()),
                new FakeOperationsQueryRepository());

            var id = service.Submit(new PurchaseRequestCreateVm
            {
                DepartmentId = 3,
                ItemDescription = "Whiteboard markers",
                Justification = "Class supplies",
                Quantity = 2,
                Currency = "KES"
            }, "staff-null-dept");

            Assert.AreEqual(3, unitOfWork.Repository<PurchaseRequest>().GetById(id).DepartmentId);
        }

        [Test]
        public void PurchaseRequestService_Submit_RejectsMissingJustification()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department { Id = 3, Name = "HR", Code = "HR", IsActive = true, IsRequisitionTarget = true });

            var service = new PurchaseRequestService(
                unitOfWork,
                new NoOpAuditWriter(),
                new FakeUserService(),
                new NoOpDepartmentScopeService(),
                new FakeOrganizationScopeService(organizationId: 1),
                new NoOpOutboxWriter(),
                new NoOpWebhookService(),
                new FakeApprovalWorkflowEngine(unitOfWork, new NoOpAuditWriter()),
                new FakeOperationsQueryRepository());

            Assert.Throws<BusinessException>(() => service.Submit(new PurchaseRequestCreateVm
            {
                DepartmentId = 3,
                Justification = "   ",
                ItemDescription = "Desk",
                Quantity = 1,
                Currency = "KES"
            }, "dept-head"));
        }
    }
}

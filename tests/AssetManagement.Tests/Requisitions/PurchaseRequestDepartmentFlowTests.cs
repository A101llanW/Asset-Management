using AssetManagement.Application.DTOs;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Requisitions
{
    [TestFixture]
    public class PurchaseRequestDepartmentFlowTests
    {
        [Test]
        public void PurchaseRequestService_Submit_UsesDepartmentCustomFlow()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department
            {
                Id = 5,
                Name = "Dining",
                Code = "DINING",
                IsActive = true,
                IsRequisitionTarget = true,
                RequisitionFlowMode = RequisitionFlowMode.Custom,
                CustomStageRoleIds = "4"
            });
            unitOfWork.Seed(new SystemSetting
            {
                Id = 1,
                SettingKey = "Approval.Purchase.Enabled",
                SettingValue = "true",
                IsActive = true
            });
            unitOfWork.Seed(new SystemSetting
            {
                Id = 2,
                SettingKey = "Approval.Purchase.StageRoleIds",
                SettingValue = "9",
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
                DepartmentId = 5,
                ItemDescription = "Kitchen supplies",
                Justification = "Restock",
                Quantity = 1,
                Currency = "KES"
            }, "facilities-manager");

            var entity = unitOfWork.Repository<PurchaseRequest>().GetById(id);
            Assert.AreEqual("4", entity.ApprovalStageRoleIds);
            Assert.AreEqual(ApprovalStatus.Pending, entity.ApprovalStatus);
        }
        [Test]
        public void PurchaseRequestService_Submit_AllowsNullHomeDepartmentId_WhenTargetSelected()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department
            {
                Id = 118,
                Name = "Library",
                Code = "LIB",
                IsActive = true,
                IsRequisitionTarget = true,
                ParentDepartmentId = 92
            });
            unitOfWork.Seed(new Department
            {
                Id = 92,
                Name = "Administration",
                Code = "ADMIN",
                IsActive = true,
                IsRequisitionTarget = false
            });
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
                new StrictDepartmentScopeService(null),
                new FakeOrganizationScopeService(organizationId: 10),
                new NoOpOutboxWriter(),
                new NoOpWebhookService(),
                new FakeApprovalWorkflowEngine(unitOfWork, new NoOpAuditWriter()),
                new FakeOperationsQueryRepository());

            var id = service.Submit(new PurchaseRequestCreateVm
            {
                DepartmentId = 118,
                ItemDescription = "Books",
                Justification = "Classroom library restock",
                Quantity = 2,
                Currency = "KES"
            }, "staff-user");

            var entity = unitOfWork.Repository<PurchaseRequest>().GetById(id);
            Assert.AreEqual(118, entity.DepartmentId);
            Assert.AreEqual(ApprovalStatus.Approved, entity.ApprovalStatus);
        }

        [Test]
        public void PurchaseRequestService_Submit_AllowsChildTarget_UnderHomeDepartmentBranch()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department
            {
                Id = 92,
                Name = "Administration",
                Code = "ADMIN",
                IsActive = true,
                IsRequisitionTarget = false
            });
            unitOfWork.Seed(new Department
            {
                Id = 118,
                Name = "Library",
                Code = "LIB",
                IsActive = true,
                IsRequisitionTarget = true,
                ParentDepartmentId = 92
            });
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
                new StrictDepartmentScopeService(92),
                new FakeOrganizationScopeService(organizationId: 10),
                new NoOpOutboxWriter(),
                new NoOpWebhookService(),
                new FakeApprovalWorkflowEngine(unitOfWork, new NoOpAuditWriter()),
                new FakeOperationsQueryRepository());

            var id = service.Submit(new PurchaseRequestCreateVm
            {
                DepartmentId = 118,
                ItemDescription = "Shelves",
                Justification = "Library shelves",
                Quantity = 1,
                Currency = "KES"
            }, "dept-user");

            Assert.AreEqual(118, unitOfWork.Repository<PurchaseRequest>().GetById(id).DepartmentId);
        }

        [Test]
        public void PurchaseRequestService_Submit_RejectsTarget_OutsideHomeDepartmentBranch()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department
            {
                Id = 92,
                Name = "Administration",
                Code = "ADMIN",
                IsActive = true,
                IsRequisitionTarget = false
            });
            unitOfWork.Seed(new Department
            {
                Id = 200,
                Name = "Other Leaf",
                Code = "OTHER",
                IsActive = true,
                IsRequisitionTarget = true
            });
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
                new StrictDepartmentScopeService(92),
                new FakeOrganizationScopeService(organizationId: 10),
                new NoOpOutboxWriter(),
                new NoOpWebhookService(),
                new FakeApprovalWorkflowEngine(unitOfWork, new NoOpAuditWriter()),
                new FakeOperationsQueryRepository());

            Assert.Throws<BusinessException>(() => service.Submit(new PurchaseRequestCreateVm
            {
                DepartmentId = 200,
                ItemDescription = "X",
                Justification = "Y",
                Quantity = 1,
                Currency = "KES"
            }, "dept-user"));
        }
    }
}

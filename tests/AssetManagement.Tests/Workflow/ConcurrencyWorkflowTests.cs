using System;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Workflow
{
    [TestFixture]
    public class ConcurrencyWorkflowTests
    {
        [Test]
        public void ApprovalWorkflowEngine_SecondStageDecision_ThrowsWhenDuplicateStage()
        {
            var unitOfWork = new FakeUnitOfWork { FailConditionalOnDuplicate = true };
            unitOfWork.Seed(new DisposalRecord
            {
                Id = 1,
                AssetId = 10,
                ApprovalStatus = ApprovalStatus.Pending,
                CurrentApprovalStage = 1,
                ApprovalStageRoleIds = "1",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            var engine = new FakeApprovalWorkflowEngine(unitOfWork, new NoOpAuditWriter());
            var request = BuildDisposalDecision(unitOfWork.Repository<DisposalRecord>().GetById(1));

            engine.ExecuteStageDecision(request);

            var ex = Assert.Throws<BusinessException>(() => engine.ExecuteStageDecision(request));
            StringAssert.Contains("already been recorded", ex.Message);
        }

        [Test]
        public void AssignmentService_BlocksAssign_WhenPendingTransferExists()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Asset
            {
                Id = 30,
                AssetTag = "AST-030",
                AssetName = "Blocked Asset",
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                DepartmentId = 1,
                Currency = "USD",
                AcquisitionCost = 1000,
                CurrentStatus = AssetStatus.Assigned,
                CurrentCustodianId = "user-a",
                PurchaseDate = DateTime.UtcNow,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow,
                UsefulLifeMonths = 36,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            unitOfWork.Seed(new AssetTransfer
            {
                AssetId = 30,
                ApprovalStatus = ApprovalStatus.Pending,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "user-b", DepartmentId = 1, IsActive = true, FirstName = "B", LastName = "User" });

            var service = TestServiceFactory.CreateAssignmentService(
                unitOfWork,
                users,
                workflowGuard: new RealWorkflowGuard(unitOfWork));

            Assert.Throws<BusinessException>(() => service.Assign(new AssetAssignmentVm
            {
                AssetId = 30,
                ToUserId = "user-b",
                ToDepartmentId = 1,
                AssignmentType = "Permanent",
                AssignedDate = DateTime.UtcNow
            }));
        }

        private static ApprovalStageDecisionRequest BuildDisposalDecision(DisposalRecord disposal)
        {
            return new ApprovalStageDecisionRequest
            {
                ProcessCode = ApprovalProcessCodes.Disposal,
                ActingUserId = "approver-1",
                ApproverRoleId = 1,
                IsSuperAdmin = true,
                RequesterUserId = "manager-1",
                Decision = ApprovalStatus.Approved,
                RequestEntity = disposal,
                RequestEntityType = typeof(DisposalRecord),
                StageNumber = 1,
                StageRoleIds = new System.Collections.Generic.List<int> { 1 },
                ExpectedRoleId = 1,
                ApprovalActionEntity = new DisposalApprovalAction
                {
                    DisposalRecordId = disposal.Id,
                    StageNumber = 1,
                    RoleId = 1,
                    ApproverUserId = "approver-1",
                    Decision = ApprovalStatus.Approved,
                    DecisionDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                },
                ApprovalActionEntityType = typeof(DisposalApprovalAction)
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace AssetManagement.Tests.Requisitions
{
    /// <summary>Migration 110: a person target carried through receiving (their room, or company custody held by them).</summary>
    public partial class RequisitionLineAssetNameTests
    {
        private static FakeUnitOfWork SeedPersonRequisition(string placement, int? targetDepartmentId)
        {
            var uow = SeedApprovedRequisition();
            uow.Seed(new Department { Id = 40, Name = "Science lab", Code = "LAB", DepartmentKind = DepartmentKind.Room, CreatedAt = DateTime.UtcNow, IsActive = true, IsRequisitionTarget = true });
            var request = uow.Repository<PurchaseRequest>().GetById(100);
            request.RequisitionTargetKind = RequisitionTargetRules.KindPerson;
            request.TargetUserId = "teacher";
            request.TargetPlacement = placement;
            request.TargetDepartmentId = targetDepartmentId;
            return uow;
        }

        private static Mock<IAssignmentService> Assignments()
        {
            var mock = new Mock<IAssignmentService>();
            var next = 0;
            mock.Setup(x => x.AssignWithoutSave(It.IsAny<AssetAssignmentVm>()))
                .Returns((AssetAssignmentVm vm) => new AssetAssignment { Id = ++next, AssetId = vm.AssetId, ToUserId = vm.ToUserId });
            return mock;
        }

        [Test]
        public void Receiving_PersonCompanyCustody_DefaultsToStore_AndAssignsThePersonAsCustodian()
        {
            var uow = SeedPersonRequisition(RequisitionTargetRules.PlacementCompanyCustody, null);
            var poId = TestServiceFactory.CreatePurchaseService(uow).Create(Order());
            var lines = PoLines(uow, poId);
            var assignments = Assignments();
            var receiving = TestServiceFactory.CreateReceivingService(uow, assignmentService: assignments.Object);

            var screen = receiving.GetReceiveOrder(poId);
            Assert.IsTrue(screen.Lines.All(x => x.DestinationKind == ReceiveOrderLineVm.DestinationStore), "company custody: Store");
            StringAssert.Contains("company custody, held by", screen.TargetSummary);

            var result = receiving.ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId,
                ReceivedDate = DateTime.UtcNow,
                Lines = new List<ReceiveOrderLineVm> { ReceiveLine(lines[1].Id, 1) }
            }, "receiver");

            var assetId = result.CreatedAssets.Single().AssetId;
            assignments.Verify(x => x.AssignWithoutSave(It.Is<AssetAssignmentVm>(vm => vm.AssetId == assetId && vm.ToUserId == "teacher" && vm.ToDepartmentId == null)), Times.Once());
            assignments.Verify(x => x.RecordAssignmentAudit(It.IsAny<AssetAssignment>(), assetId), Times.Once());
        }

        [Test]
        public void Receiving_PersonInTheirRoom_DefaultsToTheRoom_NoCustodyAssignment()
        {
            var uow = SeedPersonRequisition(RequisitionTargetRules.PlacementPersonRoom, 40);
            var poId = TestServiceFactory.CreatePurchaseService(uow).Create(Order());
            var assignments = Assignments();
            var receiving = TestServiceFactory.CreateReceivingService(uow, assignmentService: assignments.Object);

            var screen = receiving.GetReceiveOrder(poId);
            Assert.IsTrue(screen.Lines.All(x => x.DestinationKind == ReceiveOrderLineVm.DestinationDepartment && x.DestinationDepartmentId == 40));
            StringAssert.Contains("recorded in Science lab", screen.TargetSummary);

            var lines = PoLines(uow, poId);
            receiving.ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId,
                ReceivedDate = DateTime.UtcNow,
                Lines = new List<ReceiveOrderLineVm> { ReceiveLine(lines[1].Id, 1) }
            }, "receiver");
            assignments.Verify(x => x.AssignWithoutSave(It.IsAny<AssetAssignmentVm>()), Times.Never());
        }

        [Test]
        public void Receiving_LegacyDepartmentOnlyRequisition_BehavesAsBefore()
        {
            var uow = SeedApprovedRequisition();
            var poId = TestServiceFactory.CreatePurchaseService(uow).Create(Order());
            var assignments = Assignments();
            var receiving = TestServiceFactory.CreateReceivingService(uow, assignmentService: assignments.Object);
            var screen = receiving.GetReceiveOrder(poId);
            Assert.IsNull(screen.TargetSummary);
            Assert.IsTrue(screen.Lines.All(x => x.DestinationDepartmentId == 1), "the requesting department, as before");

            var lines = PoLines(uow, poId);
            receiving.ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId,
                ReceivedDate = DateTime.UtcNow,
                Lines = new List<ReceiveOrderLineVm> { ReceiveLine(lines[1].Id, 1) }
            }, "receiver");
            assignments.Verify(x => x.AssignWithoutSave(It.IsAny<AssetAssignmentVm>()), Times.Never());
        }
    }
}

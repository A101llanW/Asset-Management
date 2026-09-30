using System.Collections.Generic;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class DepartmentRequisitionFlowResolverTests
    {
        [Test]
        public void Resolve_Inherit_UsesOrgDefault()
        {
            var room = new Department
            {
                Id = 2,
                ParentDepartmentId = 1,
                DepartmentKind = DepartmentKind.Room,
                RequisitionFlowMode = RequisitionFlowMode.InheritParent
            };
            var support = new Department
            {
                Id = 1,
                DepartmentKind = DepartmentKind.Administrative,
                RequisitionFlowMode = RequisitionFlowMode.InheritParent
            };
            var org = new ApprovalProcessConfiguration
            {
                ProcessCode = ApprovalProcessCodes.Purchase,
                RequiresApproval = true,
                StageRoleIds = new List<int> { 9 }
            };

            var resolved = DepartmentRequisitionFlowResolver.Resolve(room, id => id == 1 ? support : null, org);
            Assert.IsTrue(resolved.RequiresApproval);
            Assert.AreEqual(1, resolved.StageRoleIds.Count);
            Assert.AreEqual(9, resolved.StageRoleIds[0]);
        }

        [Test]
        public void Resolve_Inherit_UsesParentCustom()
        {
            var room = new Department
            {
                Id = 3,
                ParentDepartmentId = 2,
                DepartmentKind = DepartmentKind.Room,
                RequisitionFlowMode = RequisitionFlowMode.InheritParent
            };
            var sub = new Department
            {
                Id = 2,
                ParentDepartmentId = 1,
                DepartmentKind = DepartmentKind.SubDepartment,
                RequisitionFlowMode = RequisitionFlowMode.Custom,
                CustomStageRoleIds = "4"
            };
            var org = new ApprovalProcessConfiguration
            {
                ProcessCode = ApprovalProcessCodes.Purchase,
                RequiresApproval = true,
                StageRoleIds = new List<int> { 9 }
            };

            var resolved = DepartmentRequisitionFlowResolver.Resolve(room, id =>
            {
                if (id == 2) return sub;
                return null;
            }, org);
            Assert.IsTrue(resolved.RequiresApproval);
            Assert.AreEqual(1, resolved.StageRoleIds.Count);
            Assert.AreEqual(4, resolved.StageRoleIds[0]);
        }

        [Test]
        public void Resolve_CustomEmptyStages_DisablesApproval()
        {
            var room = new Department
            {
                Id = 2,
                RequisitionFlowMode = RequisitionFlowMode.Custom,
                CustomStageRoleIds = null
            };
            var org = new ApprovalProcessConfiguration
            {
                RequiresApproval = true,
                StageRoleIds = new List<int> { 9 }
            };
            var resolved = DepartmentRequisitionFlowResolver.Resolve(room, id => null, org);
            Assert.IsFalse(resolved.RequiresApproval);
            Assert.AreEqual(0, resolved.StageRoleIds.Count);
        }

        [Test]
        public void Resolve_CustomStages_UsesRoomStages()
        {
            var room = new Department
            {
                Id = 2,
                RequisitionFlowMode = RequisitionFlowMode.Custom,
                CustomStageRoleIds = "4,5"
            };
            var org = new ApprovalProcessConfiguration
            {
                RequiresApproval = true,
                StageRoleIds = new List<int> { 9 }
            };
            var resolved = DepartmentRequisitionFlowResolver.Resolve(room, id => null, org);
            Assert.IsTrue(resolved.RequiresApproval);
            Assert.AreEqual(2, resolved.StageRoleIds.Count);
            Assert.AreEqual(4, resolved.StageRoleIds[0]);
            Assert.AreEqual(5, resolved.StageRoleIds[1]);
        }

        [Test]
        public void Resolve_IndependentRoom_Inherit_UsesOrgDefault()
        {
            var room = new Department
            {
                Id = 10,
                ParentDepartmentId = null,
                DepartmentKind = DepartmentKind.Room,
                RequisitionFlowMode = RequisitionFlowMode.InheritParent
            };
            var org = new ApprovalProcessConfiguration
            {
                ProcessCode = ApprovalProcessCodes.Purchase,
                RequiresApproval = true,
                StageRoleIds = new List<int> { 9 }
            };

            var resolved = DepartmentRequisitionFlowResolver.Resolve(room, id => null, org);
            Assert.IsTrue(resolved.RequiresApproval);
            Assert.AreEqual(9, resolved.StageRoleIds[0]);
        }

        [Test]
        public void Resolve_IndependentRoom_Custom_UsesRoom()
        {
            var room = new Department
            {
                Id = 10,
                ParentDepartmentId = null,
                DepartmentKind = DepartmentKind.Room,
                RequisitionFlowMode = RequisitionFlowMode.Custom,
                CustomStageRoleIds = "4"
            };
            var org = new ApprovalProcessConfiguration
            {
                ProcessCode = ApprovalProcessCodes.Purchase,
                RequiresApproval = true,
                StageRoleIds = new List<int> { 9 }
            };

            var resolved = DepartmentRequisitionFlowResolver.Resolve(room, id => null, org);
            Assert.IsTrue(resolved.RequiresApproval);
            Assert.AreEqual(1, resolved.StageRoleIds.Count);
            Assert.AreEqual(4, resolved.StageRoleIds[0]);
        }

        [Test]
        public void Resolve_RoomThenSubThenDept_UsesFirstConfigured()
        {
            var room = new Department
            {
                Id = 3,
                ParentDepartmentId = 2,
                DepartmentKind = DepartmentKind.Room,
                RequisitionFlowMode = RequisitionFlowMode.InheritParent
            };
            var sub = new Department
            {
                Id = 2,
                ParentDepartmentId = 1,
                DepartmentKind = DepartmentKind.SubDepartment,
                RequisitionFlowMode = RequisitionFlowMode.InheritParent
            };
            var dept = new Department
            {
                Id = 1,
                DepartmentKind = DepartmentKind.Administrative,
                RequisitionFlowMode = RequisitionFlowMode.Custom,
                CustomStageRoleIds = "7"
            };
            var org = new ApprovalProcessConfiguration
            {
                ProcessCode = ApprovalProcessCodes.Purchase,
                RequiresApproval = true,
                StageRoleIds = new List<int> { 9 }
            };

            var resolved = DepartmentRequisitionFlowResolver.Resolve(room, id =>
            {
                if (id == 2) return sub;
                if (id == 1) return dept;
                return null;
            }, org);
            Assert.IsTrue(resolved.RequiresApproval);
            Assert.AreEqual(7, resolved.StageRoleIds[0]);
        }

        [Test]
        public void Resolve_NullStart_UsesOrgDefault()
        {
            var org = new ApprovalProcessConfiguration
            {
                RequiresApproval = true,
                StageRoleIds = new List<int> { 9 }
            };
            var resolved = DepartmentRequisitionFlowResolver.Resolve(null, id => null, org);
            Assert.IsTrue(resolved.RequiresApproval);
            Assert.AreEqual(9, resolved.StageRoleIds[0]);
        }
    }
}
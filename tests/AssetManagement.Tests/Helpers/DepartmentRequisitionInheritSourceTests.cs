using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class DepartmentRequisitionInheritSourceTests
    {
        private Dictionary<int, Department> _byId;

        private ApprovalProcessConfiguration Org()
        {
            return new ApprovalProcessConfiguration
            {
                ProcessCode = ApprovalProcessCodes.Purchase,
                RequiresApproval = true,
                StageRoleIds = new List<int> { 9 }
            };
        }

        private Department Get(int id)
        {
            Department d;
            return _byId.TryGetValue(id, out d) ? d : null;
        }

        [SetUp]
        public void SetUp()
        {
            _byId = new Dictionary<int, Department>
            {
                { 1, new Department { Id = 1, Name = "Facilities", DepartmentKind = DepartmentKind.Administrative, RequisitionFlowMode = RequisitionFlowMode.Custom, CustomStageRoleIds = "7" } },
                { 2, new Department { Id = 2, Name = "Office Services", ParentDepartmentId = 1, DepartmentKind = DepartmentKind.SubDepartment, RequisitionFlowMode = RequisitionFlowMode.Custom, CustomStageRoleIds = "4" } },
                { 3, new Department { Id = 3, Name = "Boardroom 1", ParentDepartmentId = 2, DepartmentKind = DepartmentKind.Room, RequisitionFlowMode = RequisitionFlowMode.InheritParent } },
                { 4, new Department { Id = 4, Name = "Reception", ParentDepartmentId = 1, DepartmentKind = DepartmentKind.Room, RequisitionFlowMode = RequisitionFlowMode.InheritParent } },
                { 5, new Department { Id = 5, Name = "Kiosk", DepartmentKind = DepartmentKind.Room, RequisitionFlowMode = RequisitionFlowMode.InheritParent } },
                { 6, new Department { Id = 6, Name = "Year 3", DepartmentKind = DepartmentKind.Grade } },
                { 7, new Department { Id = 7, Name = "3A", ParentDepartmentId = 6, DepartmentKind = DepartmentKind.Class, RequisitionFlowMode = RequisitionFlowMode.InheritParent } }
            };
        }

        [Test]
        public void NullChoice_KeepsLegacyNearestBehaviour()
        {
            var r = DepartmentRequisitionFlowResolver.ResolveDetailed(Get(3), Get, Org());
            Assert.AreEqual(4, r.Configuration.StageRoleIds.Single());
            Assert.AreEqual("Inherited from Office Services", r.SourceLabel);
            Assert.AreEqual("Inherit - from Sub: Office Services", r.InheritSourceDisplay);
            Assert.IsNull(r.AppliedInheritChoice);
        }

        [Test]
        public void NearestChoiceValue_IsTreatedAsNull()
        {
            Get(3).RequisitionInheritFrom = RequisitionInheritSource.Nearest;
            var r = DepartmentRequisitionFlowResolver.ResolveDetailed(Get(3), Get, Org());
            Assert.AreEqual(4, r.Configuration.StageRoleIds.Single());
            Assert.IsNull(r.AppliedInheritChoice);
        }

        [Test]
        public void DepartmentChoice_SkipsSubCustom_UsesDeptFlow()
        {
            Get(3).RequisitionInheritFrom = RequisitionInheritSource.Department;
            var r = DepartmentRequisitionFlowResolver.ResolveDetailed(Get(3), Get, Org());
            Assert.AreEqual(7, r.Configuration.StageRoleIds.Single());
            Assert.AreEqual("Inherited", r.SourceKind);
            Assert.AreEqual(1, r.SourceDepartmentId);
            Assert.AreEqual("Inherit - from Dept: Facilities", r.InheritSourceDisplay);
            Assert.AreEqual("Inherit - from Dept: Facilities", r.SourceLabel);
        }

        [Test]
        public void DepartmentChoice_DeptInherits_FallsToOrgMatrix_EvenIfSubIsCustom()
        {
            Get(1).RequisitionFlowMode = RequisitionFlowMode.InheritParent;
            Get(1).CustomStageRoleIds = null;
            Get(3).RequisitionInheritFrom = RequisitionInheritSource.Department;
            var r = DepartmentRequisitionFlowResolver.ResolveDetailed(Get(3), Get, Org());
            Assert.AreEqual(9, r.Configuration.StageRoleIds.Single());
            Assert.AreEqual("OrganizationMatrix", r.SourceKind);
            Assert.AreEqual("Inherit - from Dept: Facilities (org matrix)", r.InheritSourceDisplay);
        }

        [Test]
        public void SubChoice_SubInherits_UsesDeptViaSub()
        {
            Get(2).RequisitionFlowMode = RequisitionFlowMode.InheritParent;
            Get(2).CustomStageRoleIds = null;
            Get(3).RequisitionInheritFrom = RequisitionInheritSource.SubDepartment;
            var r = DepartmentRequisitionFlowResolver.ResolveDetailed(Get(3), Get, Org());
            Assert.AreEqual(7, r.Configuration.StageRoleIds.Single());
            Assert.AreEqual("Inherit - from Sub: Office Services (via Dept: Facilities)", r.InheritSourceDisplay);
        }

        [Test]
        public void SubChoice_WithoutSubAncestor_FallsBackToNearest()
        {
            Get(4).RequisitionInheritFrom = RequisitionInheritSource.SubDepartment; // room directly under admin
            var r = DepartmentRequisitionFlowResolver.ResolveDetailed(Get(4), Get, Org());
            Assert.AreEqual(7, r.Configuration.StageRoleIds.Single());
            Assert.IsNull(r.AppliedInheritChoice);
            Assert.AreEqual("Inherit - from Dept: Facilities", r.InheritSourceDisplay);
        }

        [Test]
        public void CustomUnit_IgnoresStaleChoice()
        {
            Get(2).RequisitionInheritFrom = RequisitionInheritSource.Department;
            var r = DepartmentRequisitionFlowResolver.ResolveDetailed(Get(2), Get, Org());
            Assert.AreEqual(4, r.Configuration.StageRoleIds.Single());
            Assert.AreEqual("CustomDepartment", r.SourceKind);
        }

        [Test]
        public void IntermediateChoice_IsHonouredWhenWalking()
        {
            // Room -> Sub (Inherit, choice Dept) -> Dept custom
            Get(2).RequisitionFlowMode = RequisitionFlowMode.InheritParent;
            Get(2).RequisitionInheritFrom = RequisitionInheritSource.Department;
            var r = DepartmentRequisitionFlowResolver.ResolveDetailed(Get(3), Get, Org());
            Assert.AreEqual(7, r.Configuration.StageRoleIds.Single());
        }

        [Test]
        public void GetInheritOptions_OnlyAncestorsThatExist()
        {
            var room = DepartmentRequisitionFlowResolver.GetInheritOptions(Get(3), Get);
            CollectionAssert.AreEqual(
                new[] { RequisitionInheritSource.SubDepartment, RequisitionInheritSource.Department },
                room.Select(x => x.Source).ToList());
            Assert.AreEqual(2, room[0].AncestorId);
            Assert.AreEqual(1, room[1].AncestorId);

            var sub = DepartmentRequisitionFlowResolver.GetInheritOptions(Get(2), Get);
            CollectionAssert.AreEqual(new[] { RequisitionInheritSource.Department }, sub.Select(x => x.Source).ToList());

            Assert.AreEqual(0, DepartmentRequisitionFlowResolver.GetInheritOptions(Get(5), Get).Count); // independent room
            Assert.AreEqual(0, DepartmentRequisitionFlowResolver.GetInheritOptions(Get(7), Get).Count); // class under grade
            Assert.AreEqual(0, DepartmentRequisitionFlowResolver.GetInheritOptions(Get(1), Get).Count); // top-level admin
        }

        [Test]
        public void Service_PersistsChoice_ClearsOnCustom_RejectsUnavailable()
        {
            var unitOfWork = new FakeUnitOfWork();
            foreach (var d in _byId.Values)
            {
                d.IsActive = true;
                d.Code = "C" + d.Id;
                unitOfWork.Seed(d);
            }

            var service = new DepartmentService(
                unitOfWork,
                new PermissiveDepartmentScopeService(),
                new FakeReferenceDataCache(),
                new FakeOrganizationScopeService());

            var vm = new DepartmentVm
            {
                Id = 3,
                Name = "Boardroom 1",
                Code = "C3",
                DepartmentKind = DepartmentKind.Room,
                ParentDepartmentId = 2,
                IsActive = true,
                IsRequisitionTarget = true,
                RequisitionFlowMode = RequisitionFlowMode.InheritParent,
                RequisitionInheritFrom = RequisitionInheritSource.Department
            };
            service.Update(vm);
            Assert.AreEqual(RequisitionInheritSource.Department, unitOfWork.Repository<Department>().GetById(3).RequisitionInheritFrom);

            vm.RequisitionInheritFrom = null;
            service.Update(vm);
            Assert.IsNull(unitOfWork.Repository<Department>().GetById(3).RequisitionInheritFrom);

            // Custom mode never keeps an inherit source.
            vm.RequisitionInheritFrom = RequisitionInheritSource.SubDepartment;
            vm.UseCustomRequisitionApproval = true;
            service.Update(vm);
            Assert.IsNull(unitOfWork.Repository<Department>().GetById(3).RequisitionInheritFrom);
            Assert.AreEqual(RequisitionFlowMode.Custom, unitOfWork.Repository<Department>().GetById(3).RequisitionFlowMode);

            // Reception sits directly under the admin: no Sub ancestor -> rejected, nothing saved.
            var reception = new DepartmentVm
            {
                Id = 4,
                Name = "Reception",
                Code = "C4",
                DepartmentKind = DepartmentKind.Room,
                ParentDepartmentId = 1,
                IsActive = true,
                IsRequisitionTarget = true,
                RequisitionFlowMode = RequisitionFlowMode.InheritParent,
                RequisitionInheritFrom = RequisitionInheritSource.SubDepartment
            };
            Assert.Throws<BusinessException>(() => service.Update(reception));
            Assert.IsNull(unitOfWork.Repository<Department>().GetById(4).RequisitionInheritFrom);
        }
    }
}

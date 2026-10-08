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

namespace AssetManagement.Tests.Departments
{
    [TestFixture]
    public class DepartmentKindChangeGuardTests
    {
        // Ids are arbitrary; names are not used by any rule (data-driven).
        private static List<Department> BuildTree()
        {
            return new List<Department>
            {
                Dept(1, "Facilities", DepartmentKind.Administrative, null),
                Dept(2, "Academics", DepartmentKind.Administrative, null),
                Dept(10, "Office Services", DepartmentKind.SubDepartment, 1),
                Dept(11, "Grounds", DepartmentKind.SubDepartment, 1),
                Dept(12, "Science", DepartmentKind.SubDepartment, 2),
                Dept(20, "Boardroom 1", DepartmentKind.Room, 10),
                Dept(21, "Boardroom 2", DepartmentKind.Room, 10),
                Dept(22, "Old Store", DepartmentKind.Room, 10, false),
                Dept(23, "Lab A", DepartmentKind.Room, 12),
                Dept(30, "Year 3", DepartmentKind.Grade, null),
                Dept(31, "Year 4", DepartmentKind.Grade, null),
                Dept(40, "3A", DepartmentKind.Class, 30),
                Dept(41, "3B", DepartmentKind.Class, 30)
            };
        }

        private static Department Dept(int id, string name, DepartmentKind kind, int? parentId, bool active = true)
        {
            return new Department
            {
                Id = id,
                Name = name,
                Code = "C" + id,
                DepartmentKind = kind,
                ParentDepartmentId = parentId,
                IsActive = active,
                IsRequisitionTarget = true
            };
        }

        private static DepartmentService BuildService(FakeUnitOfWork unitOfWork)
        {
            return new DepartmentService(
                unitOfWork,
                new PermissiveDepartmentScopeService(),
                new FakeReferenceDataCache(),
                new FakeOrganizationScopeService());
        }

        private static FakeUnitOfWork Seeded()
        {
            var unitOfWork = new FakeUnitOfWork();
            foreach (var d in BuildTree())
            {
                unitOfWork.Seed(d);
            }

            return unitOfWork;
        }

        private static DepartmentVm EditVm(Department d, DepartmentKind kind, int? parentId)
        {
            return new DepartmentVm
            {
                Id = d.Id,
                Name = d.Name,
                Code = d.Code,
                DepartmentKind = kind,
                ParentDepartmentId = parentId,
                IsActive = true,
                IsRequisitionTarget = true,
                RequisitionFlowMode = RequisitionFlowMode.InheritParent
            };
        }

        [Test]
        public void Analyze_NoChange_ReturnsNothing()
        {
            var impacts = DepartmentKindChangeGuard.Analyze(10, DepartmentKind.SubDepartment, 1, BuildTree());
            Assert.AreEqual(0, impacts.Count);
        }

        [Test]
        public void Analyze_SubWithRoomsToRoom_FlagsActiveRoomsOnly_WithValidCandidates()
        {
            var impacts = DepartmentKindChangeGuard.Analyze(10, DepartmentKind.Room, 1, BuildTree());

            CollectionAssert.AreEquivalent(new[] { 20, 21 }, impacts.Select(x => x.Child.Id).ToList()); // inactive 22 ignored
            var candidates = impacts[0].CandidateParents.Select(x => x.Id).ToList();
            CollectionAssert.Contains(candidates, 1);   // top-level admin
            CollectionAssert.Contains(candidates, 2);
            CollectionAssert.Contains(candidates, 11);  // other subs
            CollectionAssert.Contains(candidates, 12);
            CollectionAssert.DoesNotContain(candidates, 10); // the unit (now a Room)
            CollectionAssert.DoesNotContain(candidates, 21); // rooms
            CollectionAssert.DoesNotContain(candidates, 30); // grades
            Assert.IsTrue(impacts[0].AllowNoParent);         // rooms may be independent
            StringAssert.Contains("room", impacts[0].Reason.ToLowerInvariant());
        }

        [Test]
        public void Analyze_AdminWithSubsToRoom_SubsNeedTopLevelAdmin()
        {
            var impacts = DepartmentKindChangeGuard.Analyze(1, DepartmentKind.Room, 2, BuildTree());

            CollectionAssert.AreEquivalent(new[] { 10, 11 }, impacts.Select(x => x.Child.Id).ToList());
            foreach (var impact in impacts)
            {
                CollectionAssert.AreEqual(new[] { 2 }, impact.CandidateParents.Select(x => x.Id).ToList());
                Assert.IsFalse(impact.AllowNoParent);
            }
        }

        [Test]
        public void Analyze_GradeWithClassesToRoom_ClassesNeedAnotherGrade()
        {
            var impacts = DepartmentKindChangeGuard.Analyze(30, DepartmentKind.Room, 1, BuildTree());

            CollectionAssert.AreEquivalent(new[] { 40, 41 }, impacts.Select(x => x.Child.Id).ToList());
            CollectionAssert.AreEqual(new[] { 31 }, impacts[0].CandidateParents.Select(x => x.Id).ToList());
        }

        [Test]
        public void Analyze_LegacyInvalidChild_IsNotFlagged()
        {
            var tree = BuildTree();
            tree.Add(Dept(50, "Legacy nested room", DepartmentKind.Room, 20)); // already Room-under-Room
            var impacts = DepartmentKindChangeGuard.Analyze(20, DepartmentKind.Room, 11, tree); // move room 20 only
            Assert.AreEqual(0, impacts.Count);
        }

        [Test]
        public void Update_SubWithRoomsToRoom_WithoutMoves_BlocksAndListsChildren()
        {
            var unitOfWork = Seeded();
            var service = BuildService(unitOfWork);
            var sub = unitOfWork.Repository<Department>().GetById(10);

            var ex = Assert.Throws<BusinessException>(() => service.Update(EditVm(sub, DepartmentKind.Room, 1)));

            StringAssert.Contains("Boardroom 1", ex.Message);
            StringAssert.Contains("Boardroom 2", ex.Message);
            StringAssert.DoesNotContain("Old Store", ex.Message);
            var reloaded = unitOfWork.Repository<Department>().GetById(10);
            Assert.AreEqual(DepartmentKind.SubDepartment, reloaded.DepartmentKind);
            Assert.AreEqual(10, unitOfWork.Repository<Department>().GetById(20).ParentDepartmentId);
        }

        [Test]
        public void Update_SubWithRoomsToRoom_WithMoves_AppliesKindAndMovesTogether()
        {
            var unitOfWork = Seeded();
            var service = BuildService(unitOfWork);
            var vm = EditVm(unitOfWork.Repository<Department>().GetById(10), DepartmentKind.Room, 1);
            vm.ChildMoves = new List<DepartmentChildMoveVm>
            {
                new DepartmentChildMoveVm { ChildDepartmentId = 20, NewParentDepartmentId = 11 },
                new DepartmentChildMoveVm { ChildDepartmentId = 21, NewParentDepartmentId = 0 } // independent
            };

            service.Update(vm);

            var repo = unitOfWork.Repository<Department>();
            Assert.AreEqual(DepartmentKind.Room, repo.GetById(10).DepartmentKind);
            Assert.AreEqual(1, repo.GetById(10).ParentDepartmentId);
            Assert.AreEqual(11, repo.GetById(20).ParentDepartmentId);
            Assert.IsNull(repo.GetById(21).ParentDepartmentId);
            Assert.AreEqual(10, repo.GetById(22).ParentDepartmentId); // inactive child untouched
        }

        [Test]
        public void Update_MoveToInvalidParent_IsRejected()
        {
            var unitOfWork = Seeded();
            var service = BuildService(unitOfWork);
            var vm = EditVm(unitOfWork.Repository<Department>().GetById(10), DepartmentKind.Room, 1);
            vm.ChildMoves = new List<DepartmentChildMoveVm>
            {
                new DepartmentChildMoveVm { ChildDepartmentId = 20, NewParentDepartmentId = 23 }, // a room
                new DepartmentChildMoveVm { ChildDepartmentId = 21, NewParentDepartmentId = 11 }
            };

            Assert.Throws<BusinessException>(() => service.Update(vm));
            Assert.AreEqual(DepartmentKind.SubDepartment, unitOfWork.Repository<Department>().GetById(10).DepartmentKind);
            Assert.AreEqual(10, unitOfWork.Repository<Department>().GetById(21).ParentDepartmentId);
        }

        [Test]
        public void Update_MoveForNonChild_IsRejected()
        {
            var unitOfWork = Seeded();
            var service = BuildService(unitOfWork);
            var vm = EditVm(unitOfWork.Repository<Department>().GetById(10), DepartmentKind.Room, 1);
            vm.ChildMoves = new List<DepartmentChildMoveVm>
            {
                new DepartmentChildMoveVm { ChildDepartmentId = 20, NewParentDepartmentId = 11 },
                new DepartmentChildMoveVm { ChildDepartmentId = 21, NewParentDepartmentId = 11 },
                new DepartmentChildMoveVm { ChildDepartmentId = 23, NewParentDepartmentId = 11 } // not a child of 10
            };

            Assert.Throws<BusinessException>(() => service.Update(vm));
        }

        [Test]
        public void Update_PartialMoves_StillBlocksForRemainingChild()
        {
            var unitOfWork = Seeded();
            var service = BuildService(unitOfWork);
            var vm = EditVm(unitOfWork.Repository<Department>().GetById(10), DepartmentKind.Room, 1);
            vm.ChildMoves = new List<DepartmentChildMoveVm>
            {
                new DepartmentChildMoveVm { ChildDepartmentId = 20, NewParentDepartmentId = 11 },
                new DepartmentChildMoveVm { ChildDepartmentId = 21, NewParentDepartmentId = null } // not chosen
            };

            var ex = Assert.Throws<BusinessException>(() => service.Update(vm));
            StringAssert.Contains("Boardroom 2", ex.Message);
            Assert.AreEqual(10, unitOfWork.Repository<Department>().GetById(20).ParentDepartmentId);
        }

        [Test]
        public void Update_AdminToSub_MovingSubsKeepsGrandchildRoomsValid()
        {
            var unitOfWork = Seeded();
            unitOfWork.Seed(Dept(3, "Operations", DepartmentKind.Administrative, null));
            var service = BuildService(unitOfWork);
            var vm = EditVm(unitOfWork.Repository<Department>().GetById(1), DepartmentKind.SubDepartment, 2);
            vm.ChildMoves = new List<DepartmentChildMoveVm>
            {
                new DepartmentChildMoveVm { ChildDepartmentId = 10, NewParentDepartmentId = 3 },
                new DepartmentChildMoveVm { ChildDepartmentId = 11, NewParentDepartmentId = 3 }
            };

            service.Update(vm);

            var repo = unitOfWork.Repository<Department>();
            Assert.AreEqual(DepartmentKind.SubDepartment, repo.GetById(1).DepartmentKind);
            Assert.AreEqual(3, repo.GetById(10).ParentDepartmentId);
            Assert.AreEqual(10, repo.GetById(20).ParentDepartmentId); // grandchild room stays under its Sub
        }

        [Test]
        public void Update_RenameOnlyWithChildren_IsUnaffected()
        {
            var unitOfWork = Seeded();
            var service = BuildService(unitOfWork);
            var vm = EditVm(unitOfWork.Repository<Department>().GetById(10), DepartmentKind.SubDepartment, 1);
            vm.Name = "Office Services (renamed)";

            service.Update(vm);

            Assert.AreEqual("Office Services (renamed)", unitOfWork.Repository<Department>().GetById(10).Name);
        }

        [Test]
        public void GetKindChangeImpact_ReturnsLabelsAndIndependentOption()
        {
            var unitOfWork = Seeded();
            var service = BuildService(unitOfWork);

            var impact = service.GetKindChangeImpact(10, DepartmentKind.Room, 1);

            Assert.AreEqual(2, impact.Items.Count);
            Assert.AreEqual(0, impact.Items[0].Candidates[0].Id); // "No parent (independent)" first for rooms
            Assert.IsTrue(impact.Items[0].Candidates.Any(c => c.Id == 11));
        }
    }
}

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
        public void Resolve_CustomEmptyStages_FallsBackToAutoApprove()
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
            var detailed = DepartmentRequisitionFlowResolver.ResolveDetailed(room, id => null, org);
            Assert.IsFalse(detailed.Configuration.RequiresApproval);
            Assert.AreEqual(0, detailed.Configuration.StageRoleIds.Count);
            Assert.AreEqual("AutoApprove", detailed.SourceKind);
        }

        [Test]
        public void Resolve_AutoApprove_DisablesApproval()
        {
            var room = new Department
            {
                Id = 2,
                DepartmentKind = DepartmentKind.Room,
                RequisitionFlowMode = RequisitionFlowMode.AutoApprove,
                CustomStageRoleIds = "4,5"
            };
            var org = new ApprovalProcessConfiguration
            {
                RequiresApproval = true,
                StageRoleIds = new List<int> { 9 }
            };
            var detailed = DepartmentRequisitionFlowResolver.ResolveDetailed(room, id => null, org);
            Assert.IsFalse(detailed.Configuration.RequiresApproval);
            Assert.AreEqual(0, detailed.Configuration.StageRoleIds.Count);
            Assert.AreEqual("AutoApprove", detailed.SourceKind);
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
        public void Resolve_IndependentRoom_Inherit_DoesNotWalkFakeParent()
        {
            var room = new Department
            {
                Id = 10,
                ParentDepartmentId = null,
                DepartmentKind = DepartmentKind.Room,
                RequisitionFlowMode = RequisitionFlowMode.InheritParent
            };
            var fakeParent = new Department
            {
                Id = 99,
                DepartmentKind = DepartmentKind.Administrative,
                RequisitionFlowMode = RequisitionFlowMode.Custom,
                CustomStageRoleIds = "4"
            };
            var org = new ApprovalProcessConfiguration
            {
                RequiresApproval = true,
                StageRoleIds = new List<int> { 9 }
            };

            // getById would return a department if incorrectly called with any id
            var resolved = DepartmentRequisitionFlowResolver.Resolve(room, id => fakeParent, org);
            Assert.IsTrue(resolved.RequiresApproval);
            Assert.AreEqual(9, resolved.StageRoleIds[0]);
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

        [Test]
        public void Resolve_Inherit_ParentAutoApprove_StopsWalk()
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
                Name = "IT Sub",
                DepartmentKind = DepartmentKind.SubDepartment,
                RequisitionFlowMode = RequisitionFlowMode.AutoApprove
            };
            var org = new ApprovalProcessConfiguration
            {
                RequiresApproval = true,
                StageRoleIds = new List<int> { 9 }
            };
            var detailed = DepartmentRequisitionFlowResolver.ResolveDetailed(room, id => id == 2 ? sub : null, org);
            Assert.IsFalse(detailed.Configuration.RequiresApproval);
            Assert.AreEqual("Inherited", detailed.SourceKind);
            Assert.AreEqual(2, detailed.SourceDepartmentId);
        }

        [Test]
        public void Resolve_SubDeptMiss_UsesDeptCustom()
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
                Name = "Support",
                DepartmentKind = DepartmentKind.Administrative,
                RequisitionFlowMode = RequisitionFlowMode.Custom,
                CustomStageRoleIds = "11"
            };
            var org = new ApprovalProcessConfiguration { RequiresApproval = true, StageRoleIds = new List<int> { 9 } };
            var detailed = DepartmentRequisitionFlowResolver.ResolvePurchaseStages(room, id =>
            {
                if (id == 2) return sub;
                if (id == 1) return dept;
                return null;
            }, org, null);
            Assert.IsTrue(detailed.Configuration.RequiresApproval);
            Assert.AreEqual(11, detailed.Configuration.StageRoleIds[0]);
            Assert.AreEqual("Inherited", detailed.SourceKind);
        }

        [Test]
        public void Resolve_DeptMiss_UsesOrgMatrix()
        {
            var dept = new Department
            {
                Id = 1,
                DepartmentKind = DepartmentKind.Administrative,
                RequisitionFlowMode = RequisitionFlowMode.InheritParent
            };
            var org = new ApprovalProcessConfiguration { RequiresApproval = true, StageRoleIds = new List<int> { 9 } };
            var detailed = DepartmentRequisitionFlowResolver.ResolvePurchaseStages(dept, id => null, org, null);
            Assert.AreEqual("OrganizationMatrix", detailed.SourceKind);
            Assert.AreEqual(9, detailed.Configuration.StageRoleIds[0]);
        }

        [Test]
        public void Resolve_RoomCustom_HitsOwnStages()
        {
            var room = new Department
            {
                Id = 5,
                ParentDepartmentId = 1,
                DepartmentKind = DepartmentKind.Room,
                RequisitionFlowMode = RequisitionFlowMode.Custom,
                CustomStageRoleIds = "3,8"
            };
            var org = new ApprovalProcessConfiguration { RequiresApproval = true, StageRoleIds = new List<int> { 9 } };
            var detailed = DepartmentRequisitionFlowResolver.ResolvePurchaseStages(room, id => null, org, null);
            Assert.AreEqual("CustomDepartment", detailed.SourceKind);
            Assert.AreEqual(2, detailed.Configuration.StageRoleIds.Count);
            Assert.AreEqual(3, detailed.Configuration.StageRoleIds[0]);
        }

        [Test]
        public void Resolve_PreferSubDepartment_SkipsAdminCustomUntilSub()
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
                Name = "Lab",
                DepartmentKind = DepartmentKind.SubDepartment,
                RequisitionFlowMode = RequisitionFlowMode.Custom,
                CustomStageRoleIds = "4"
            };
            var admin = new Department
            {
                Id = 1,
                DepartmentKind = DepartmentKind.Administrative,
                RequisitionFlowMode = RequisitionFlowMode.Custom,
                CustomStageRoleIds = "99"
            };
            var org = new ApprovalProcessConfiguration { RequiresApproval = true, StageRoleIds = new List<int> { 9 } };
            var options = new DepartmentRequisitionFlowResolver.ResolveOptions
            {
                RoomInheritTarget = RoomInheritTarget.PreferSubDepartment
            };
            var detailed = DepartmentRequisitionFlowResolver.ResolvePurchaseStages(room, id =>
            {
                if (id == 2) return sub;
                if (id == 1) return admin;
                return null;
            }, org, options);
            Assert.AreEqual(4, detailed.Configuration.StageRoleIds[0]);
        }

        [Test]
        public void Resolve_AcademicDefault_UsesOrgMatrix()
        {
            var stream = new Department
            {
                Id = 20,
                ParentDepartmentId = 10,
                DepartmentKind = DepartmentKind.Class,
                RequisitionFlowMode = RequisitionFlowMode.Custom,
                CustomStageRoleIds = "4"
            };
            var org = new ApprovalProcessConfiguration { RequiresApproval = true, StageRoleIds = new List<int> { 9 } };
            var detailed = DepartmentRequisitionFlowResolver.ResolvePurchaseStages(stream, id => null, org, null);
            Assert.AreEqual("OrganizationMatrix", detailed.SourceKind);
            Assert.AreEqual(9, detailed.Configuration.StageRoleIds[0]);
        }

        [Test]
        public void Resolve_DualReadOverlay_PrefersProvidedStages()
        {
            var room = new Department
            {
                Id = 2,
                RequisitionFlowMode = RequisitionFlowMode.Custom,
                CustomStageRoleIds = "1"
            };
            var org = new ApprovalProcessConfiguration { RequiresApproval = true, StageRoleIds = new List<int> { 9 } };
            var options = new DepartmentRequisitionFlowResolver.ResolveOptions
            {
                GetCustomStageRoleIds = d => new List<int> { 42, 43 }
            };
            var detailed = DepartmentRequisitionFlowResolver.ResolvePurchaseStages(room, id => null, org, options);
            Assert.AreEqual(42, detailed.Configuration.StageRoleIds[0]);
            Assert.AreEqual(43, detailed.Configuration.StageRoleIds[1]);
        }


        [Test]
        public void Resolve_PreferClassCustom_UsesClassStages()
        {
            var stream = new Department
            {
                Id = 20,
                ParentDepartmentId = 10,
                Name = "1A",
                DepartmentKind = DepartmentKind.Class,
                RequisitionFlowMode = RequisitionFlowMode.Custom,
                CustomStageRoleIds = "4",
                AcademicFlowMode = AcademicFlowMode.PreferClassCustom
            };
            var grade = new Department
            {
                Id = 10,
                Name = "Grade 1",
                DepartmentKind = DepartmentKind.Grade,
                RequisitionFlowMode = RequisitionFlowMode.Custom,
                CustomStageRoleIds = "99"
            };
            var org = new ApprovalProcessConfiguration { RequiresApproval = true, StageRoleIds = new List<int> { 9 } };
            var options = DepartmentRequisitionFlowResolver.BuildOptionsFromDepartment(stream);
            var detailed = DepartmentRequisitionFlowResolver.ResolvePurchaseStages(stream, id => id == 10 ? grade : null, org, options);
            Assert.AreEqual("CustomDepartment", detailed.SourceKind);
            Assert.AreEqual(4, detailed.Configuration.StageRoleIds[0]);
        }

        [Test]
        public void Resolve_ClassPreferGradeCustom_UsesGradeThenOrg()
        {
            var stream = new Department
            {
                Id = 20,
                ParentDepartmentId = 10,
                DepartmentKind = DepartmentKind.Class,
                RequisitionFlowMode = RequisitionFlowMode.InheritParent,
                AcademicFlowMode = AcademicFlowMode.PreferGradeCustom
            };
            var grade = new Department
            {
                Id = 10,
                Name = "Grade 1",
                DepartmentKind = DepartmentKind.Grade,
                RequisitionFlowMode = RequisitionFlowMode.Custom,
                CustomStageRoleIds = "15"
            };
            var org = new ApprovalProcessConfiguration { RequiresApproval = true, StageRoleIds = new List<int> { 9 } };
            var options = DepartmentRequisitionFlowResolver.BuildOptionsFromDepartment(stream);
            var detailed = DepartmentRequisitionFlowResolver.ResolvePurchaseStages(stream, id => id == 10 ? grade : null, org, options);
            Assert.AreEqual("Inherited", detailed.SourceKind);
            Assert.AreEqual(15, detailed.Configuration.StageRoleIds[0]);
        }

        [Test]
        public void BuildOptions_Room_UsesPersistedInheritTarget()
        {
            var room = new Department
            {
                Id = 3,
                DepartmentKind = DepartmentKind.Room,
                RequisitionFlowMode = RequisitionFlowMode.InheritParent,
                RoomInheritTarget = RoomInheritTarget.OrganizationOnly
            };
            var options = DepartmentRequisitionFlowResolver.BuildOptionsFromDepartment(room);
            Assert.AreEqual(RoomInheritTarget.OrganizationOnly, options.RoomInheritTarget);
        }

        [Test]
        public void NormalizeAcademicMode_ClassCustom_ForcesPreferClassCustom()
        {
            var stream = new Department
            {
                DepartmentKind = DepartmentKind.Class,
                RequisitionFlowMode = RequisitionFlowMode.Custom,
                AcademicFlowMode = AcademicFlowMode.UseOrganizationMatrix
            };
            Assert.AreEqual(
                AcademicFlowMode.PreferClassCustom,
                DepartmentRequisitionFlowResolver.NormalizeAcademicMode(stream));
        }

    }
}
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using NUnit.Framework;

namespace AssetManagement.Tests.Departments
{
    [TestFixture]
    public class DepartmentSubtreeHelperTests
    {
        private static Department D(int id, int? parentId, DepartmentKind kind = DepartmentKind.SubDepartment)
        {
            return new Department
            {
                Id = id,
                Name = "D" + id,
                Code = "C" + id,
                IsActive = true,
                DepartmentKind = kind,
                ParentDepartmentId = parentId
            };
        }

        [Test]
        public void GetSelfAndDescendantIds_IncludesSelfDirectChildAndGrandchild()
        {
            var all = new[]
            {
                D(1, null, DepartmentKind.Administrative),
                D(2, 1),
                D(3, 2, DepartmentKind.Room),
                D(4, 1)
            };

            var set = DepartmentSubtreeHelper.GetSelfAndDescendantIds(2, all);
            CollectionAssert.AreEquivalent(new[] { 2, 3 }, set.ToArray());
        }

        [Test]
        public void IsSelfOrDescendantParent_RejectsSelf()
        {
            var all = new[] { D(10, null, DepartmentKind.Administrative), D(11, 10) };
            Assert.That(DepartmentSubtreeHelper.IsSelfOrDescendantParent(10, 10, all), Is.True);
        }

        [Test]
        public void IsSelfOrDescendantParent_RejectsDirectChildAndGrandchild()
        {
            var all = new[]
            {
                D(1, null, DepartmentKind.Administrative),
                D(2, 1),
                D(3, 2, DepartmentKind.Room)
            };
            Assert.That(DepartmentSubtreeHelper.IsSelfOrDescendantParent(1, 2, all), Is.True);
            Assert.That(DepartmentSubtreeHelper.IsSelfOrDescendantParent(1, 3, all), Is.True);
        }

        [Test]
        public void IsSelfOrDescendantParent_AllowsValidSiblingOrAncestor()
        {
            var all = new[]
            {
                D(1, null, DepartmentKind.Administrative),
                D(2, 1),
                D(3, 1),
                D(4, 2, DepartmentKind.Room)
            };
            Assert.That(DepartmentSubtreeHelper.IsSelfOrDescendantParent(2, 1, all), Is.False);
            Assert.That(DepartmentSubtreeHelper.IsSelfOrDescendantParent(2, 3, all), Is.False);
            Assert.That(DepartmentSubtreeHelper.IsSelfOrDescendantParent(4, 3, all), Is.False);
            Assert.That(DepartmentSubtreeHelper.IsSelfOrDescendantParent(2, null, all), Is.False);
        }

        [Test]
        public void GetSelfAndDescendantIds_CycleSafe_DoesNotLoopForever()
        {
            // 5 -> 6 -> 5, plus root 4 -> 5
            var all = new[]
            {
                D(4, null, DepartmentKind.Administrative),
                D(5, 6),
                D(6, 5)
            };

            var set = DepartmentSubtreeHelper.GetSelfAndDescendantIds(4, all);
            CollectionAssert.AreEquivalent(new[] { 4 }, set.ToArray());

            var fromCycle = DepartmentSubtreeHelper.GetSelfAndDescendantIds(5, all);
            CollectionAssert.AreEquivalent(new[] { 5, 6 }, fromCycle.ToArray());
        }

        [Test]
        public void AssertValidHierarchy_RejectsSelfAndDescendantParent_WithBusinessException()
        {
            var all = new[]
            {
                D(1, null, DepartmentKind.Administrative),
                D(2, 1),
                D(3, 2, DepartmentKind.Room)
            };

            Assert.Throws<BusinessException>(() =>
                DepartmentHierarchyRules.AssertValidHierarchy(2, DepartmentKind.SubDepartment, 2, all));

            Assert.Throws<BusinessException>(() =>
                DepartmentHierarchyRules.AssertValidHierarchy(1, DepartmentKind.Administrative, 3, all));
        }

        [Test]
        public void AssertValidHierarchy_AllowsValidSubParent()
        {
            var all = new[]
            {
                D(1, null, DepartmentKind.Administrative),
                D(2, 1)
            };

            Assert.DoesNotThrow(() =>
                DepartmentHierarchyRules.AssertValidHierarchy(2, DepartmentKind.SubDepartment, 1, all));
        }

        [Test]
        public void BuildOtherParentPickerGroups_ExcludesSelfAndDescendants()
        {
            var scoped = new[]
            {
                new DepartmentVm { Id = 1, Name = "Support", Code = "SUP", IsActive = true, DepartmentKind = DepartmentKind.Administrative },
                new DepartmentVm { Id = 2, Name = "IT", Code = "IT", IsActive = true, DepartmentKind = DepartmentKind.SubDepartment, ParentDepartmentId = 1 },
                new DepartmentVm { Id = 3, Name = "ICT", Code = "ICT", IsActive = true, DepartmentKind = DepartmentKind.Room, ParentDepartmentId = 2 },
                new DepartmentVm { Id = 4, Name = "Facilities", Code = "FAC", IsActive = true, DepartmentKind = DepartmentKind.SubDepartment, ParentDepartmentId = 1 }
            }.ToList();

            var groups = DepartmentRoomParentCandidates.BuildOtherParentPickerGroups(scoped, excludeSubtreeRootId: 2);
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(1, groups[0].AdminId);
            CollectionAssert.AreEquivalent(new[] { 4 }, groups[0].SubDepartments.Select(x => x.SubDepartmentId).ToArray());
        }
    }
}
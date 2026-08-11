using System;
using AssetManagement.Application.Security;
using AssetManagement.Domain.Entities;
using NUnit.Framework;

namespace AssetManagement.Tests.Users
{
    [TestFixture]
    public class DepartmentHeadAssignmentRulesTests
    {
        [Test]
        public void IsDepartmentHeadRole_MatchesActiveDepartmentHeadRole()
        {
            var role = new Role { Name = "Department Head", IsActive = true };

            Assert.IsTrue(DepartmentHeadAssignmentRules.IsDepartmentHeadRole(role));
        }

        [Test]
        public void IsDepartmentHeadRole_RejectsInactiveOrOtherRoles()
        {
            Assert.IsFalse(DepartmentHeadAssignmentRules.IsDepartmentHeadRole(new Role { Name = "Department Head", IsActive = false }));
            Assert.IsFalse(DepartmentHeadAssignmentRules.IsDepartmentHeadRole(new Role { Name = "Asset Manager", IsActive = true }));
            Assert.IsFalse(DepartmentHeadAssignmentRules.IsDepartmentHeadRole(null));
        }

        [Test]
        public void IsDepartmentHeadRoleId_UsesResolver()
        {
            Assert.IsTrue(DepartmentHeadAssignmentRules.IsDepartmentHeadRoleId(
                8,
                id => new Role { Id = id, Name = "department head", IsActive = true }));
            Assert.IsFalse(DepartmentHeadAssignmentRules.IsDepartmentHeadRoleId(
                8,
                id => new Role { Id = id, Name = "Custodian", IsActive = true }));
        }
    }
}

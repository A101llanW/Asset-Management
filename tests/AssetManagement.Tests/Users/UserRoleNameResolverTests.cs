using System.Collections.Generic;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using NUnit.Framework;

namespace AssetManagement.Tests.Users
{
    [TestFixture]
    public class UserRoleNameResolverTests
    {
        [Test]
        public void ApplyOrganizationRoleNames_UsesOrganizationRoleLookup()
        {
            var users = new List<UserVm>
            {
                new UserVm { Id = "hod", RoleId = 8, RoleName = null },
                new UserVm { Id = "staff", RoleId = 5, RoleName = "Staff" }
            };
            var roles = new List<RoleVm>
            {
                new RoleVm { Id = 5, Name = "Staff" },
                new RoleVm { Id = 8, Name = "Department Head" }
            };

            UserRoleNameResolver.ApplyOrganizationRoleNames(users, roles);

            Assert.AreEqual("Department Head", users[0].RoleName);
            Assert.AreEqual("Staff", users[1].RoleName);
        }

        [Test]
        public void ApplyOrganizationRoleNames_LeavesUnknownRoleIdUntouched()
        {
            var users = new List<UserVm>
            {
                new UserVm { Id = "orphan", RoleId = 99, RoleName = "Legacy role" }
            };

            UserRoleNameResolver.ApplyOrganizationRoleNames(users, new List<RoleVm>
            {
                new RoleVm { Id = 5, Name = "Staff" }
            });

            Assert.AreEqual("Legacy role", users[0].RoleName);
        }
    }
}

using System.Collections.Generic;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using NUnit.Framework;

namespace AssetManagement.Tests.Requisitions
{
    /// <summary>Owner decision: catalog-sourced stages resolve approvers catalog-path-first (before the requesting department).</summary>
    [TestFixture]
    public class CatalogApproverFirstTests
    {
        private const int IctManagerRoleId = 7;

        private static StageApproverScopeResolver Resolver(params UserVm[] users)
        {
            var departments = new Dictionary<int, Department>
            {
                { 1, new Department { Id = 1, Name = "ICT" } },
                { 5, new Department { Id = 5, Name = "Science", ParentDepartmentId = 9 } },
                { 9, new Department { Id = 9, Name = "Academics" } }
            };
            return new StageApproverScopeResolver(() => users, id => departments.ContainsKey(id) ? departments[id] : null);
        }

        private static UserVm U(string id, int? dept, int role = IctManagerRoleId, bool active = true)
        {
            return new UserVm { Id = id, RoleId = role, DepartmentId = dept, IsActive = active };
        }

        [Test]
        public void CatalogStage_IctManagerInIct_IsApprover_EvenWhenRequestingChainHasHolder()
        {
            var resolver = Resolver(U("ict-mgr", 1), U("sci-holder", 5));

            var scope = resolver.Resolve(IctManagerRoleId, null, 5, true);

            Assert.AreEqual(StageApproverScopeKind.CatalogPath, scope.Kind);
            Assert.IsTrue(scope.Contains("ict-mgr"));
        }

        [Test]
        public void DepartmentStage_Unchanged_RequestingDepartmentHolderWins()
        {
            var resolver = Resolver(U("ict-mgr", 1), U("sci-holder", 5));

            var scope = resolver.Resolve(IctManagerRoleId, null, 5, false);

            Assert.AreEqual(StageApproverScopeKind.Department, scope.Kind);
            CollectionAssert.AreEqual(new[] { "sci-holder" }, scope.UserIds);
        }

        [Test]
        public void CatalogStage_NamedPerson_StillWinsFirst()
        {
            var resolver = Resolver(U("ict-mgr", 1), U("named", 1, 3));

            var scope = resolver.Resolve(IctManagerRoleId, "named", 5, true);

            Assert.AreEqual(StageApproverScopeKind.NamedPerson, scope.Kind);
        }

        [Test]
        public void CatalogStage_NoActiveHolders_FallsBackToDepartmentChain()
        {
            var resolver = Resolver(U("ict-mgr", 1, IctManagerRoleId, false));

            var scope = resolver.Resolve(IctManagerRoleId, null, 5, true);

            Assert.AreNotEqual(StageApproverScopeKind.CatalogPath, scope.Kind);
            Assert.AreEqual(0, scope.UserIds.Count);
        }

        [TestCase("Custom on Type: Laptops", true)]
        [TestCase("Custom on Sub-type: Gaming laptops", true)]
        [TestCase("Auto-approve on Category #4", true)]
        [TestCase("Custom on ICT", false)]
        [TestCase("Inherited from ICT", false)]
        [TestCase("Organization Approval Matrix", false)]
        [TestCase(null, false)]
        public void IsCatalogSource_RecognisesCatalogLabelsOnly(string label, bool expected)
        {
            Assert.AreEqual(expected, CatalogRequisitionFlowResolver.IsCatalogSource(label));
        }
    }
}

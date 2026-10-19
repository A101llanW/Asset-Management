using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Services;
using AssetManagement.Domain.Entities;
using AssetManagement.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    /// <summary>
    /// rel1009c QA: saving Roles Edit wiped RolePermission rows that the matrix does not render
    /// (Api.* ids 3/4/5). Saves must only touch the rendered matrix set. Since 5857956 (2026-10-12) Depreciation.*
    /// is rendered in the matrix, so the outside-matrix rows here are hidden Api.* ids.
    /// </summary>
    [TestFixture]
    public class RolePermissionMatrixSaveTests
    {
        private FakeUnitOfWork _uow;

        [SetUp]
        public void SetUp()
        {
            _uow = new FakeUnitOfWork();
            _uow.Seed(new Permission { Id = 3, Code = "Api.Assets.Read", Module = "Api", Name = "API Read Assets", IsActive = true });
            _uow.Seed(new Permission { Id = 4, Code = "Api.Assets.Write", Module = "Api", Name = "API Write Assets", IsActive = true });
            _uow.Seed(new Permission { Id = 39, Code = "Assets.View", Module = "Assets", Name = "View Assets", IsActive = true });
            _uow.Seed(new Permission { Id = 61, Code = "Depreciation.View", Module = "Depreciation", Name = "View Depreciation", IsActive = true });
            _uow.Seed(new Permission { Id = 65, Code = "Reports.View", Module = "Reports", Name = "View Reports", IsActive = true });
            _uow.Seed(new Permission { Id = 67, Code = "AuditLogs.View", Module = "Audit", Name = "View Audit Logs", IsActive = true });
            _uow.Seed(new Role { Id = 65, Name = "Auditor", OrganizationId = 1, IsActive = true });
            foreach (var id in new[] { 3, 4, 39, 65 })
            {
                _uow.Seed(new RolePermission { Id = id, RoleId = 65, PermissionId = id, OrganizationId = 1 });
            }
        }

        private RoleService CreateService(bool companyAdmin, IAuthorizationService auth = null)
        {
            var scope = new Mock<IOrganizationScopeService>();
            scope.Setup(x => x.IsCompanyAdmin()).Returns(companyAdmin);
            scope.Setup(x => x.GetCurrentOrganizationId()).Returns(1);
            return new RoleService(_uow, Mock.Of<IAuditWriter>(), auth ?? Mock.Of<IAuthorizationService>(), scope.Object, new FakeCurrentUserContext("editor-1"));
        }

        private List<int> Current()
        {
            return _uow.Repository<RolePermission>().GetAll().Where(x => x.RoleId == 65).Select(x => x.PermissionId).OrderBy(x => x).ToList();
        }

        [Test]
        public void MatrixIds_ExcludeApi_IncludeDepreciation()
        {
            CollectionAssert.AreEquivalent(new[] { 39, 61, 65, 67 }, CreateService(true).GetMatrixPermissionIds());
        }

        [Test]
        public void SaveWithNoChanges_KeepsPermissionsOutsideMatrix()
        {
            CreateService(true).SetPermissions(65, new[] { 39, 65 });
            CollectionAssert.AreEqual(new[] { 3, 4, 39, 65 }, Current());
        }

        [Test]
        public void TogglingRenderedBoxes_AddsAndRemovesOnlyThose()
        {
            CreateService(true).SetPermissions(65, new[] { 39, 67 });
            CollectionAssert.AreEqual(new[] { 3, 4, 39, 67 }, Current());
        }

        [Test]
        public void ClearingEveryBox_StillKeepsPermissionsOutsideMatrix()
        {
            CreateService(true).SetPermissions(65, new int[0]);
            CollectionAssert.AreEqual(new[] { 3, 4 }, Current());
        }

        [Test]
        public void ForgedIdOutsideMatrix_IsNotGranted()
        {
            _uow.Seed(new Permission { Id = 5, Code = "Api.Assets.Delete", Module = "Api", Name = "API Delete Assets", IsActive = true });
            CreateService(true).SetPermissions(65, new[] { 39, 65, 5 });
            CollectionAssert.AreEqual(new[] { 3, 4, 39, 65 }, Current());
        }

        [Test]
        public void EscalationCheck_StillBlocksGrantingAPermissionTheEditorLacks()
        {
            var auth = new Mock<IAuthorizationService>();
            auth.Setup(x => x.HasPermission("editor-1", It.IsAny<string>())).Returns(true);
            auth.Setup(x => x.HasPermission("editor-1", "AuditLogs.View")).Returns(false);
            Assert.Throws<BusinessException>(() => CreateService(false, auth.Object).SetPermissions(65, new[] { 39, 65, 67 }));
            CollectionAssert.AreEqual(new[] { 3, 4, 39, 65 }, Current());
        }
    }
}

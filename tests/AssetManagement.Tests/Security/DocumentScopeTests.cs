using System;
using System.IO;
using System.Text;
using AssetManagement.Application.DTOs;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    [TestFixture]
    public class DocumentScopeTests
    {
        [Test]
        public void GetStoredRelativePath_BlocksDocumentForOutOfScopeAsset()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Asset
            {
                Id = 5,
                AssetTag = "DOC-ASSET",
                DepartmentId = 10,
                IsActive = true,
                CurrentStatus = AssetStatus.InStore,
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                CreatedAt = DateTime.UtcNow
            });
            unitOfWork.Seed(new AssetDocument
            {
                Id = 100,
                AssetId = 5,
                FileName = "invoice.pdf",
                FilePath = "assets/5/invoice.pdf",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            var scope = new StrictDepartmentScopeService(20);
            var service = TestServiceFactory.CreateAssetDocumentService(unitOfWork, scope);

            Assert.Throws<BusinessException>(() => service.GetStoredRelativePath(100, "user-1"));
        }

        [Test]
        public void GetStoredRelativePath_AllowsDocumentForScopedAsset()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Asset
            {
                Id = 6,
                AssetTag = "DOC-OK",
                DepartmentId = 10,
                IsActive = true,
                CurrentStatus = AssetStatus.InStore,
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                CreatedAt = DateTime.UtcNow
            });
            unitOfWork.Seed(new AssetDocument
            {
                Id = 101,
                AssetId = 6,
                FileName = "warranty.pdf",
                FilePath = "assets/6/warranty.pdf",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            var scope = new StrictDepartmentScopeService(10);
            var service = TestServiceFactory.CreateAssetDocumentService(unitOfWork, scope);

            Assert.AreEqual("assets/6/warranty.pdf", service.GetStoredRelativePath(101, "user-1"));
        }

        [Test]
        public void Upload_AllowsCurrentCustodianWithoutDocumentsUploadPermission()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Asset
            {
                Id = 7,
                AssetTag = "DOC-CUST",
                DepartmentId = 10,
                CurrentCustodianId = "custodian-1",
                IsActive = true,
                CurrentStatus = AssetStatus.Assigned,
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                CreatedAt = DateTime.UtcNow
            });

            var scope = new StrictDepartmentScopeService(10);
            var service = TestServiceFactory.CreateAssetDocumentService(
                unitOfWork,
                scope,
                authorization: new DenyAllAuthorizationService(),
                currentUser: new FakeCurrentUserContext("custodian-1"));

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("condition note")))
            {
                var documentId = service.Upload(7, "Condition photo", "condition.txt", "text/plain", stream, "custodian-1");
                Assert.Greater(documentId, 0);
            }
        }

        [Test]
        public void Upload_BlocksNonCustodianWithoutDocumentsUploadPermission()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Asset
            {
                Id = 8,
                AssetTag = "DOC-BLOCK",
                DepartmentId = 10,
                CurrentCustodianId = "custodian-1",
                IsActive = true,
                CurrentStatus = AssetStatus.Assigned,
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                CreatedAt = DateTime.UtcNow
            });

            var scope = new StrictDepartmentScopeService(10);
            var service = TestServiceFactory.CreateAssetDocumentService(
                unitOfWork,
                scope,
                authorization: new DenyAllAuthorizationService(),
                currentUser: new FakeCurrentUserContext("other-user"));

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("condition note")))
            {
                Assert.Throws<BusinessException>(() =>
                    service.Upload(8, "Condition photo", "condition.txt", "text/plain", stream, "other-user"));
            }
        }

        [Test]
        public void GetStoredRelativePath_AllowsCurrentCustodianWithoutDocumentsDownloadPermission()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Asset
            {
                Id = 9,
                AssetTag = "DOC-DL",
                DepartmentId = 10,
                CurrentCustodianId = "custodian-1",
                IsActive = true,
                CurrentStatus = AssetStatus.Assigned,
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                CreatedAt = DateTime.UtcNow
            });
            unitOfWork.Seed(new AssetDocument
            {
                Id = 102,
                AssetId = 9,
                FileName = "photo.jpg",
                FilePath = "assets/9/photo.jpg",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            var scope = new StrictDepartmentScopeService(20);
            var service = TestServiceFactory.CreateAssetDocumentService(
                unitOfWork,
                scope,
                authorization: new DenyAllAuthorizationService(),
                currentUser: new FakeCurrentUserContext("custodian-1"));

            Assert.AreEqual("assets/9/photo.jpg", service.GetStoredRelativePath(102, "custodian-1"));
        }
    }
}

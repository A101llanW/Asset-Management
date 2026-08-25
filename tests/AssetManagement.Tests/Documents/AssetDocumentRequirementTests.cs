using System;
using System.IO;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Documents
{
    [TestFixture]
    public class AssetDocumentRequirementTests
    {
        [Test]
        public void CreateIncidentPhotoRequirement_AddsPendingRowToStatusTable()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Asset
            {
                Id = 5,
                AssetTag = "LAP-001",
                AssetName = "Laptop",
                DepartmentId = 1,
                CategoryId = 1,
                AssetTypeId = 1,
                IsActive = true,
                OrganizationId = 1,
                CurrentStatus = AssetStatus.Assigned,
                CreatedAt = DateTime.UtcNow
            });

            var requirementService = new AssetDocumentRequirementService(
                unitOfWork,
                new FakeUserService(),
                new NoOpDepartmentScopeService(),
                new FakeCurrentUserContext("user-1"));

            var requirementId = requirementService.CreateIncidentPhotoRequirement(5, 12, "INC-1001");
            var rows = requirementService.GetStatusRowsByAsset(5).ToList();

            Assert.AreEqual(requirementId, rows.Single().RequirementId);
            Assert.IsTrue(rows.Single().IsPending);
            Assert.AreEqual(AssetDocumentProcessCodes.IncidentDamagePhotoType, rows.Single().DocumentType);
            Assert.AreEqual("Incident INC-1001", rows.Single().ProcessReference);
        }

        [Test]
        public void UploadForRequirement_FulfillsPendingIncidentPhotoRequirement()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Asset
            {
                Id = 5,
                AssetTag = "LAP-001",
                AssetName = "Laptop",
                DepartmentId = 1,
                CategoryId = 1,
                AssetTypeId = 1,
                IsActive = true,
                OrganizationId = 1,
                CurrentStatus = AssetStatus.Damaged,
                CreatedAt = DateTime.UtcNow
            });

            var requirementService = new AssetDocumentRequirementService(
                unitOfWork,
                new FakeUserService(),
                new NoOpDepartmentScopeService(),
                new FakeCurrentUserContext("user-1"));

            var requirementId = requirementService.CreateIncidentPhotoRequirement(5, 12, "INC-1001");

            var storage = new NoOpFileStorageProvider();
            var documentService = new AssetDocumentService(
                unitOfWork,
                storage,
                new FakeUserService(),
                new NoOpAuditWriter(),
                new NoOpDepartmentScopeService(),
                new NoOpAuthorizationService(),
                new FakeCurrentUserContext("user-1"),
                requirementService);

            var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00 };
            using (var stream = new MemoryStream(pngBytes))
            {
                documentService.UploadForRequirement(5, requirementId, "damage.png", "image/png", stream, "user-1");
            }

            var rows = requirementService.GetStatusRowsByAsset(5).ToList();
            Assert.IsFalse(rows.Single().IsPending);
            Assert.AreEqual("damage.png", rows.Single().FileName);
            Assert.IsNotNull(rows.Single().DocumentId);
        }
    }
}

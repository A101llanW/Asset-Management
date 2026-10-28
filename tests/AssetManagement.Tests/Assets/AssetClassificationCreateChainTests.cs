#pragma warning disable 618 // GetAll() is fine for in-memory fakes
using System;
using System.Linq;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Assets
{
    /// <summary>
    /// Inline "Add new..." from the requisition form / receiving screen (Allan 2026-10-28): category + first type + first
    /// sub-type, type + first sub-type, or sub-type; one transaction; case-insensitive duplicates blocked under the same
    /// parent; the organisation is always the signed-in user's; who / when / where recorded (migration 110 + AuditLog).
    /// </summary>
    public partial class AssetClassificationCreateServiceTests
    {
        private const string Requester = "hod-1";

        private ClassificationChainCreatedVm Chain(ClassificationChainCreateVm model, string via = ClassificationCreateOrigin.Requisition, int org = 1)
        {
            return new AssetClassificationCreateService(_uow, new FakeOrganizationScopeService(organizationId: org), _audit).CreateChain(model, Requester, via);
        }

        [Test]
        public void Chain_NewCategoryTypeAndSubType_OneTransaction_StampedAndAudited()
        {
            var result = Chain(new ClassificationChainCreateVm { CategoryName = "  Lab  equipment ", TypeName = "Microscopes", SubTypeName = "Student microscope" }, ClassificationCreateOrigin.Receiving);

            Assert.AreEqual(1, _uow.TransactionsCommitted);
            Assert.IsTrue(result.CategoryCreated);
            Assert.IsTrue(result.TypeCreated);
            var category = _uow.Repository<AssetCategory>().GetById(result.CategoryId);
            var type = _uow.Repository<AssetType>().GetById(result.AssetTypeId);
            var sub = _uow.Repository<AssetSubType>().GetById(result.AssetSubTypeId);
            Assert.AreEqual("Lab equipment", category.Name);
            Assert.AreEqual(category.Id, type.AssetCategoryId);
            Assert.AreEqual(type.Id, sub.AssetTypeId);
            foreach (var row in new object[] { category, type, sub })
            {
                Assert.AreEqual(1, (int?)row.GetType().GetProperty("OrganizationId").GetValue(row, null));
                Assert.AreEqual(Requester, row.GetType().GetProperty("CreatedById").GetValue(row, null));
                Assert.AreEqual("Receiving", row.GetType().GetProperty("CreatedVia").GetValue(row, null));
                Assert.AreNotEqual(default(DateTime), (DateTime)row.GetType().GetProperty("CreatedAt").GetValue(row, null));
            }

            CollectionAssert.AreEqual(
                new[] { AssetClassificationCreateService.AuditActionCreateCategoryInline, AssetClassificationCreateService.AuditActionCreateTypeInline, AssetClassificationCreateService.AuditActionCreateSubTypeInline },
                _audit.Rows.Select(r => r.Action).ToArray());
            Assert.IsTrue(_audit.Rows.All(r => r.InsideTransaction));
            Assert.IsTrue(_audit.Rows.All(r => r.NewValues.Contains("CreatedVia=Receiving") && r.NewValues.Contains("CreatedById=" + Requester)));
        }

        [Test]
        public void Chain_NewTypeUnderExistingCategory_ComesWithItsFirstSubType()
        {
            var result = Chain(new ClassificationChainCreateVm { CategoryId = 1, TypeName = "Benches", SubTypeName = "Lab bench" });
            Assert.IsFalse(result.CategoryCreated);
            Assert.IsTrue(result.TypeCreated);
            Assert.AreEqual(1, result.CategoryId);
            Assert.AreEqual(1, _uow.TransactionsCommitted);
            var type = _uow.Repository<AssetType>().GetById(result.AssetTypeId);
            Assert.AreEqual("Requisition", type.CreatedVia);
            Assert.AreEqual(1, _uow.Repository<AssetSubType>().GetAll().Count(x => x.AssetTypeId == type.Id));
            Assert.AreEqual(2, _audit.Rows.Count);
        }

        [Test]
        public void Chain_NewSubTypeUnderExistingType()
        {
            var result = Chain(new ClassificationChainCreateVm { AssetTypeId = 10, SubTypeName = "Teacher desk" });
            Assert.IsFalse(result.TypeCreated);
            Assert.AreEqual(10, result.AssetTypeId);
            var sub = _uow.Repository<AssetSubType>().GetById(result.AssetSubTypeId);
            Assert.AreEqual("Teacher desk", sub.Name);
            Assert.AreEqual(Requester, sub.CreatedById);
            Assert.AreEqual(AssetClassificationCreateService.AuditActionCreateSubTypeInline, _audit.Rows.Single().Action);
        }

        [TestCase("furniture")]
        [TestCase("  FURNITURE ")]
        public void Chain_DuplicateCategory_CaseInsensitive_IsBlocked(string name)
        {
            var ex = Assert.Throws<BusinessException>(() => Chain(new ClassificationChainCreateVm { CategoryName = name, TypeName = "X", SubTypeName = "Y" }));
            StringAssert.Contains("already exists", ex.Message);
            Assert.AreEqual(0, _uow.TransactionsCommitted);
            Assert.AreEqual(0, _audit.Rows.Count);
        }

        [Test]
        public void Chain_CategoryOfAnotherOrganisation_DoesNotCountAsDuplicate_AndIsNotUsable()
        {
            var created = Chain(new ClassificationChainCreateVm { CategoryName = "Other org", TypeName = "T", SubTypeName = "S" });
            Assert.AreEqual(1, _uow.Repository<AssetCategory>().GetById(created.CategoryId).OrganizationId);
            Assert.Throws<BusinessException>(() => Chain(new ClassificationChainCreateVm { CategoryId = 9, TypeName = "T", SubTypeName = "S" }));
        }

        [TestCase("desks")]
        [TestCase(" DESKS ")]
        public void Chain_DuplicateTypeUnderTheCategory_CaseInsensitive_IsBlocked(string name)
        {
            var ex = Assert.Throws<BusinessException>(() => Chain(new ClassificationChainCreateVm { CategoryId = 1, TypeName = name, SubTypeName = "Y" }));
            StringAssert.Contains("already has an asset type", ex.Message);
            Assert.AreEqual(2, TypeCount());
            Assert.AreEqual(0, SubTypeCount());
        }

        [Test]
        public void Chain_DuplicateSubTypeUnderTheType_CaseInsensitive_IsBlocked()
        {
            Chain(new ClassificationChainCreateVm { AssetTypeId = 10, SubTypeName = "Teacher desk" });
            Assert.Throws<BusinessException>(() => Chain(new ClassificationChainCreateVm { AssetTypeId = 10, SubTypeName = "  teacher   DESK " }));
            Assert.AreEqual(1, SubTypeCount());
        }

        [Test]
        public void Chain_TypeAlwaysNeedsItsFirstSubType()
        {
            var ex = Assert.Throws<BusinessException>(() => Chain(new ClassificationChainCreateVm { CategoryId = 1, TypeName = "Benches", SubTypeName = " " }));
            StringAssert.Contains("sub-type", ex.Message);
            Assert.AreEqual(2, TypeCount());
        }

        [Test]
        public void Chain_ForcesTheSignedInOrganisation_EvenUnderASharedType()
        {
            var shared = new AssetType { OrganizationId = null, AssetCategoryId = 1, Name = "Shared", IsActive = true, CreatedAt = DateTime.UtcNow };
            _uow.Repository<AssetType>().Add(shared);
            _uow.SaveChanges();
            var result = Chain(new ClassificationChainCreateVm { AssetTypeId = shared.Id, SubTypeName = "Own" });
            Assert.AreEqual(1, _uow.Repository<AssetSubType>().GetById(result.AssetSubTypeId).OrganizationId);
        }

        [Test]
        public void Chain_WithoutOrganisationScope_IsRejected()
        {
            var ex = Assert.Throws<BusinessException>(() => Chain(new ClassificationChainCreateVm { CategoryName = "New", TypeName = "T", SubTypeName = "S" }, org: 0));
            StringAssert.Contains("inside your organisation", ex.Message);
            Assert.AreEqual(0, _uow.TransactionsCommitted);
        }

        [TestCase(null)]
        [TestCase("Settings")]
        public void Chain_UnknownOrigin_IsRejected(string via)
        {
            Assert.Throws<BusinessException>(() => Chain(new ClassificationChainCreateVm { AssetTypeId = 10, SubTypeName = "S" }, via));
            Assert.AreEqual(0, SubTypeCount());
        }

        [Test]
        public void Chain_WithoutUser_IsRejected()
        {
            var service = new AssetClassificationCreateService(_uow, new FakeOrganizationScopeService(organizationId: 1), _audit);
            Assert.Throws<BusinessException>(() => service.CreateChain(new ClassificationChainCreateVm { AssetTypeId = 10, SubTypeName = "S" }, " ", ClassificationCreateOrigin.Requisition));
            Assert.AreEqual(0, SubTypeCount());
        }

        [Test]
        public void Chain_InactiveParents_AreRefused()
        {
            Assert.Throws<BusinessException>(() => Chain(new ClassificationChainCreateVm { CategoryId = 3, TypeName = "T", SubTypeName = "S" }));
            Assert.Throws<BusinessException>(() => Chain(new ClassificationChainCreateVm { AssetTypeId = 11, SubTypeName = "S" }));
            Assert.AreEqual(0, SubTypeCount());
        }
    }
}

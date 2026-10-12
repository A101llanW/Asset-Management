using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using NUnit.Framework;

namespace AssetManagement.Tests.Requisitions
{
    /// <summary>Stage (a) of catalog requisition flows: catalog cascade, department fallback and fingerprint.</summary>
    [TestFixture]
    public class CatalogRequisitionFlowResolverTests
    {
        private const int CategoryId = 1;
        private const int TypeId = 10;
        private const int SubTypeId = 100;

        private static readonly ApprovalProcessConfiguration OrgMatrix = new ApprovalProcessConfiguration
        {
            ProcessCode = ApprovalProcessCodes.Purchase,
            RequiresApproval = true,
            StageRoleIds = new List<int> { 9 }
        };

        private static Department DeptCustom()
        {
            return new Department
            {
                Id = 5,
                Name = "ICT",
                DepartmentKind = DepartmentKind.Administrative,
                RequisitionFlowMode = RequisitionFlowMode.Custom,
                CustomStageRoleIds = "4"
            };
        }

        private static CatalogRequisitionFlow Flow(CatalogFlowScope scope, int id, RequisitionFlowMode mode, string roles = null, int? raiserRole = null, string raiserUser = null)
        {
            return new CatalogRequisitionFlow
            {
                Scope = scope,
                ScopedEntityId = id,
                FlowMode = mode,
                CustomStageRoleIds = roles,
                PurchaseOrderRaiserRoleId = raiserRole,
                PurchaseOrderRaiserUserId = raiserUser,
                IsActive = true
            };
        }

        private static RequisitionFlowResolveResult Resolve(
            Department department,
            RequisitionLineClassification classification,
            params CatalogRequisitionFlow[] flows)
        {
            return RequisitionFlowResolver.Resolve(
                department,
                classification,
                id => null,
                OrgMatrix,
                (scope, id) => flows.FirstOrDefault(f => f.Scope == scope && f.ScopedEntityId == id),
                (scope, id) => scope + "-" + id);
        }

        private static RequisitionLineClassification Full()
        {
            return RequisitionLineClassification.Create(CategoryId, TypeId, SubTypeId);
        }

        [Test]
        public void SubTypeOverride_BeatsTypeAndCategory()
        {
            var result = Resolve(DeptCustom(), Full(),
                Flow(CatalogFlowScope.Category, CategoryId, RequisitionFlowMode.Custom, "1"),
                Flow(CatalogFlowScope.Type, TypeId, RequisitionFlowMode.Custom, "2"),
                Flow(CatalogFlowScope.SubType, SubTypeId, RequisitionFlowMode.Custom, "3,7"));

            Assert.AreEqual("CatalogCustom", result.SourceKind);
            Assert.AreEqual(CatalogFlowScope.SubType, result.CatalogHit.Scope);
            CollectionAssert.AreEqual(new[] { 3, 7 }, result.Configuration.StageRoleIds.ToArray());
            Assert.AreEqual("Catalog:SubType:100", result.SourceKey);
            StringAssert.StartsWith("Custom on Sub-type", result.SourceLabel);
        }

        [Test]
        public void TypeOverride_BeatsCategory_WhenSubTypeHasNone()
        {
            var result = Resolve(DeptCustom(), Full(),
                Flow(CatalogFlowScope.Category, CategoryId, RequisitionFlowMode.Custom, "1"),
                Flow(CatalogFlowScope.Type, TypeId, RequisitionFlowMode.Custom, "2"));

            Assert.AreEqual(CatalogFlowScope.Type, result.CatalogHit.Scope);
            Assert.AreEqual(2, result.Configuration.StageRoleIds.Single());
        }

        [Test]
        public void CategoryOverride_UsedWhenTypeAndSubTypeInherit()
        {
            var result = Resolve(DeptCustom(), Full(),
                Flow(CatalogFlowScope.Category, CategoryId, RequisitionFlowMode.Custom, "1"),
                Flow(CatalogFlowScope.Type, TypeId, RequisitionFlowMode.InheritParent, "2"),
                Flow(CatalogFlowScope.SubType, SubTypeId, RequisitionFlowMode.InheritParent));

            Assert.AreEqual(CatalogFlowScope.Category, result.CatalogHit.Scope);
            Assert.AreEqual(1, result.Configuration.StageRoleIds.Single());
        }

        [Test]
        public void NoCatalogOverride_FallsBackToDepartmentChain()
        {
            var result = Resolve(DeptCustom(), Full());

            Assert.IsFalse(result.IsCatalogSource);
            Assert.AreEqual("CustomDepartment", result.SourceKind);
            Assert.AreEqual("Department:5", result.SourceKey);
            Assert.AreEqual(4, result.Configuration.StageRoleIds.Single());
        }

        [Test]
        public void UnclassifiedLine_SkipsCatalog_EvenWhenOverridesExist()
        {
            var result = Resolve(DeptCustom(), new RequisitionLineClassification(),
                Flow(CatalogFlowScope.Type, TypeId, RequisitionFlowMode.Custom, "2"));

            Assert.AreEqual("CustomDepartment", result.SourceKind);
            Assert.AreEqual(4, result.Configuration.StageRoleIds.Single());
        }

        [Test]
        public void NoCatalogAndInheritingDepartment_UsesOrgMatrix()
        {
            var dept = new Department { Id = 6, Name = "Library", RequisitionFlowMode = RequisitionFlowMode.InheritParent };
            var result = Resolve(dept, Full());

            Assert.AreEqual("OrganizationMatrix", result.SourceKind);
            Assert.AreEqual("OrganizationMatrix", result.SourceKey);
            Assert.AreEqual(9, result.Configuration.StageRoleIds.Single());
        }

        [Test]
        public void CatalogAutoApprove_ExplicitZeroStages_StopsCascade()
        {
            var result = Resolve(DeptCustom(), Full(),
                Flow(CatalogFlowScope.Type, TypeId, RequisitionFlowMode.AutoApprove, "2"),
                Flow(CatalogFlowScope.Category, CategoryId, RequisitionFlowMode.Custom, "1"));

            Assert.AreEqual("CatalogAutoApprove", result.SourceKind);
            Assert.IsFalse(result.Configuration.UsesApproval);
            Assert.AreEqual(0, result.Configuration.StageRoleIds.Count);
        }

        [Test]
        public void InactiveCatalogRow_IsIgnored()
        {
            var flow = Flow(CatalogFlowScope.Type, TypeId, RequisitionFlowMode.Custom, "2");
            flow.IsActive = false;
            var result = Resolve(DeptCustom(), Full(), flow);

            Assert.AreEqual("CustomDepartment", result.SourceKind);
        }

        [Test]
        public void PoRaiser_FollowsWinningCatalogNode()
        {
            var result = Resolve(DeptCustom(), Full(),
                Flow(CatalogFlowScope.Type, TypeId, RequisitionFlowMode.Custom, "2", raiserRole: 31, raiserUser: " u-buyer "));

            Assert.AreEqual(31, result.Configuration.PurchaseOrderRaiserRoleId);
            Assert.AreEqual("u-buyer", result.Configuration.PurchaseOrderRaiserUserId);
        }

        [Test]
        public void PoRaiserUser_WithoutRole_IsDropped()
        {
            var config = CatalogRequisitionFlowResolver.ToConfiguration(
                Flow(CatalogFlowScope.Type, TypeId, RequisitionFlowMode.Custom, "2", raiserRole: null, raiserUser: "u-buyer"));

            Assert.IsFalse(config.HasPurchaseOrderRaiser);
            Assert.IsNull(config.PurchaseOrderRaiserUserId);
        }

        [Test]
        public void Fingerprint_SamePath_IsEqual_AcrossDifferentUnconfiguredTypes()
        {
            var a = Resolve(DeptCustom(), RequisitionLineClassification.Create(null, 10, null));
            var b = Resolve(DeptCustom(), RequisitionLineClassification.Create(null, 11, null));
            var none = Resolve(DeptCustom(), new RequisitionLineClassification());

            Assert.AreEqual(a.Fingerprint, b.Fingerprint);
            Assert.AreEqual(a.Fingerprint, none.Fingerprint);
        }

        [Test]
        public void Fingerprint_DifferentStages_Differ()
        {
            var catalog = Resolve(DeptCustom(), Full(), Flow(CatalogFlowScope.Type, TypeId, RequisitionFlowMode.Custom, "2"));
            var department = Resolve(DeptCustom(), new RequisitionLineClassification());

            Assert.AreNotEqual(catalog.Fingerprint, department.Fingerprint);
        }

        [Test]
        public void Fingerprint_StageOrderAndRaiserMatter()
        {
            var c1 = new ApprovalProcessConfiguration { RequiresApproval = true, StageRoleIds = new List<int> { 1, 2 } };
            var c2 = new ApprovalProcessConfiguration { RequiresApproval = true, StageRoleIds = new List<int> { 2, 1 } };
            var c3 = new ApprovalProcessConfiguration { RequiresApproval = true, StageRoleIds = new List<int> { 1, 2 }, PurchaseOrderRaiserRoleId = 5 };
            var c1Again = new ApprovalProcessConfiguration { RequiresApproval = true, StageRoleIds = new List<int> { 1, 2 }, StageUserIds = new List<string> { "", " " } };

            var f1 = RequisitionFlowResolver.ComputeFingerprint("Department:5", c1);
            Assert.AreNotEqual(f1, RequisitionFlowResolver.ComputeFingerprint("Department:5", c2));
            Assert.AreNotEqual(f1, RequisitionFlowResolver.ComputeFingerprint("Department:5", c3));
            Assert.AreEqual(f1, RequisitionFlowResolver.ComputeFingerprint("Department:5", c1Again));
            Assert.AreEqual(32, f1.Length);
        }

        [Test]
        public void Fingerprint_StageUserIsCaseInsensitive_AndSourceKeyMatters()
        {
            var a = new ApprovalProcessConfiguration { RequiresApproval = true, StageRoleIds = new List<int> { 1 }, StageUserIds = new List<string> { "ABC" } };
            var b = new ApprovalProcessConfiguration { RequiresApproval = true, StageRoleIds = new List<int> { 1 }, StageUserIds = new List<string> { "abc" } };

            Assert.AreEqual(
                RequisitionFlowResolver.ComputeFingerprint("Catalog:Type:10", a),
                RequisitionFlowResolver.ComputeFingerprint("Catalog:Type:10", b));
            Assert.AreNotEqual(
                RequisitionFlowResolver.ComputeFingerprint("Catalog:Type:10", a),
                RequisitionFlowResolver.ComputeFingerprint("Catalog:Type:11", a));
        }

        [Test]
        public void Normalize_SubTypeImpliesTypeAndCategory()
        {
            var normalized = Normalize(RequisitionLineClassification.Create(null, null, SubTypeId));

            Assert.AreEqual(SubTypeId, normalized.AssetSubTypeId);
            Assert.AreEqual(TypeId, normalized.AssetTypeId);
            Assert.AreEqual(CategoryId, normalized.AssetCategoryId);
        }

        [Test]
        public void Normalize_RejectsSubTypeUnderOtherType()
        {
            var ex = Assert.Throws<BusinessException>(() => Normalize(RequisitionLineClassification.Create(null, 11, SubTypeId)));
            StringAssert.Contains("does not belong", ex.Message);
            StringAssert.StartsWith("Line 2", ex.Message);
        }

        [Test]
        public void Normalize_RejectsTypeUnderOtherCategory()
        {
            Assert.Throws<BusinessException>(() => Normalize(RequisitionLineClassification.Create(2, TypeId, null)));
        }

        [Test]
        public void Normalize_RejectsForeignOrganizationAndInactive()
        {
            Assert.Throws<BusinessException>(() => Normalize(RequisitionLineClassification.Create(null, 12, null)));
            Assert.Throws<BusinessException>(() => Normalize(RequisitionLineClassification.Create(null, 13, null)));
            Assert.Throws<BusinessException>(() => Normalize(RequisitionLineClassification.Create(null, 999, null)));
        }

        [Test]
        public void Normalize_IgnoresZeroIds()
        {
            var normalized = Normalize(RequisitionLineClassification.Create(0, 0, 0));
            Assert.IsFalse(normalized.HasAny);
        }

        [Test]
        public void PolicySettings_ParseWithSafeDefaults()
        {
            Assert.AreEqual(RequisitionClassificationMode.Optional, RequisitionPolicySettings.ParseClassificationMode(null));
            Assert.AreEqual(RequisitionClassificationMode.Optional, RequisitionPolicySettings.ParseClassificationMode("garbage"));
            Assert.AreEqual(RequisitionClassificationMode.RequiredPerLine, RequisitionPolicySettings.ParseClassificationMode(" requiredperline "));
            Assert.AreEqual(RequisitionClassificationMode.RequiredHeader, RequisitionPolicySettings.ParseClassificationMode("RequiredHeader"));
            Assert.AreEqual(RequisitionMixedFlowPolicy.AutoSplit, RequisitionPolicySettings.ParseMixedFlowPolicy(null));
            Assert.AreEqual(RequisitionMixedFlowPolicy.Block, RequisitionPolicySettings.ParseMixedFlowPolicy("block"));
        }

        private static RequisitionLineClassification Normalize(RequisitionLineClassification input)
        {
            var categories = new Dictionary<int, AssetCategory>
            {
                { 1, new AssetCategory { Id = 1, OrganizationId = 1, IsActive = true } },
                { 2, new AssetCategory { Id = 2, OrganizationId = 1, IsActive = true } }
            };
            var types = new Dictionary<int, AssetType>
            {
                { 10, new AssetType { Id = 10, AssetCategoryId = 1, OrganizationId = 1, IsActive = true } },
                { 11, new AssetType { Id = 11, AssetCategoryId = 1, OrganizationId = 1, IsActive = true } },
                { 12, new AssetType { Id = 12, AssetCategoryId = 1, OrganizationId = 2, IsActive = true } },
                { 13, new AssetType { Id = 13, AssetCategoryId = 1, OrganizationId = 1, IsActive = false } }
            };
            var subTypes = new Dictionary<int, AssetSubType>
            {
                { 100, new AssetSubType { Id = 100, AssetTypeId = 10, OrganizationId = 1, IsActive = true } }
            };

            return CatalogRequisitionFlowResolver.Normalize(
                input,
                id => subTypes.ContainsKey(id) ? subTypes[id] : null,
                id => types.ContainsKey(id) ? types[id] : null,
                id => categories.ContainsKey(id) ? categories[id] : null,
                1,
                "Line 2");
        }
    }
}

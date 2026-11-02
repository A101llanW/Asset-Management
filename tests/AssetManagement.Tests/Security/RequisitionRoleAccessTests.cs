using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Security;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using AssetManagement.Tests.Requisitions;
using AssetManagement.Web.Controllers;
using AssetManagement.Web.Filters;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    /// <summary>
    /// Requisition role access (audit 2026-10-29, fixes #2, #5, #6, #7, #9, #10). Allan: "If on the roles they do not
    /// have that right, then they shouldn't have it ... Finance ... will need a wider view".
    /// </summary>
    [TestFixture]
    public class RequisitionRoleAccessTests
    {
        private static Func<string, bool> Grants(params string[] codes)
        {
            var set = new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);
            return code => set.Contains(code);
        }

        // ---- #5 PDF / attachment ----

        [TestCase("Purchases.View")]
        [TestCase("Purchases.Create")]
        [TestCase("Purchases.Approve")]
        public void CanDownloadDocument_AnyOfViewCreateApprove(string code)
        {
            Assert.IsTrue(RequisitionAccessRules.CanDownloadDocument(Grants(code)));
        }

        [Test]
        public void CanDownloadDocument_FalseWithoutRequisitionPermissions()
        {
            Assert.IsFalse(RequisitionAccessRules.CanDownloadDocument(Grants("Purchases.Edit", "Assets.View")));
            Assert.IsFalse(RequisitionAccessRules.CanDownloadDocument(null));
        }

        [TestCase("DownloadAttachment")]
        [TestCase("DownloadDocument")]
        [TestCase("DocumentFragment")]
        public void DownloadActions_AcceptViewCreateOrApprove_LikeDetails(string action)
        {
            var methods = typeof(PurchaseRequestsController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == action)
                .ToList();
            Assert.IsNotEmpty(methods, action);
            foreach (var method in methods)
            {
                Assert.IsFalse(method.GetCustomAttributes(typeof(PermissionAuthorizeAttribute), true).Any(),
                    action + " must not require Purchases.View alone");
                var any = method.GetCustomAttributesData()
                    .Single(a => a.Constructor.DeclaringType == typeof(AnyPermissionAuthorizeAttribute));
                var codes = ((IEnumerable<CustomAttributeTypedArgument>)any.ConstructorArguments[0].Value)
                    .Select(x => (string)x.Value)
                    .ToList();
                CollectionAssert.AreEquivalent(RequisitionAccessRules.DocumentPermissions, codes);
            }
        }

        [Test]
        public void DetailsView_ShowsPdfButtonOnlyWhenDownloadAllowed()
        {
            var controller = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Controllers", "PurchaseRequestsController.cs");
            var view = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Views", "PurchaseRequests", "Details.cshtml");
            StringAssert.Contains("ViewBag.CanDownloadRequisition = RequisitionAccessRules.CanDownloadDocument(HasPermission)", controller);
            var guard = view.IndexOf("@if (canDownloadRequisition)", StringComparison.Ordinal);
            var button = view.IndexOf("id=\"amDownloadRequisitionPdf\"", StringComparison.Ordinal);
            Assert.That(guard, Is.GreaterThan(0));
            Assert.That(button, Is.GreaterThan(guard));
            // Out-of-scope requisitions return 404 / JSON fail instead of an error page.
            StringAssert.Contains("private PurchaseRequestDetailVm GetRequisitionForDownload(int id)", controller);
        }

        // ---- #6 Create PO button ----

        [Test]
        public void CreatePoButton_MatchesPurchasesControllerCreate()
        {
            // PurchasesController: [PermissionAuthorize("Purchases.View")] on the class + "Purchases.Edit" on Create.
            Assert.IsTrue(RequisitionAccessRules.CanShowCreatePurchaseOrder(true, 1, true, Grants("Purchases.View", "Purchases.Edit")));
            Assert.IsFalse(RequisitionAccessRules.CanShowCreatePurchaseOrder(true, 1, true, Grants("Purchases.View", "Purchases.Create")),
                "Create without Edit got a 403 from PurchasesController.Create");
            Assert.IsFalse(RequisitionAccessRules.CanShowCreatePurchaseOrder(true, 1, true, Grants("Purchases.Edit")));
            Assert.IsFalse(RequisitionAccessRules.CanShowCreatePurchaseOrder(true, 1, false, Grants("Purchases.View", "Purchases.Edit")),
                "PO raiser rule still applies");
            Assert.IsFalse(RequisitionAccessRules.CanShowCreatePurchaseOrder(false, 1, true, Grants("Purchases.View", "Purchases.Edit")));
            Assert.IsFalse(RequisitionAccessRules.CanShowCreatePurchaseOrder(true, 0, true, Grants("Purchases.View", "Purchases.Edit")));
        }

        [Test]
        public void CreatePoButton_ViewUsesSharedRule_AndActionIsNotWidened()
        {
            var view = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Views", "PurchaseRequests", "Details.cshtml");
            StringAssert.Contains("RequisitionAccessRules.CanShowCreatePurchaseOrder(", view);
            StringAssert.DoesNotContain("Html.HasPermission(\"Purchases.Create\") && Model.CanCurrentUserRaisePurchaseOrder", view);

            var purchases = typeof(PurchasesController);
            var classAttr = purchases.GetCustomAttributesData().Single(a => a.Constructor.DeclaringType == typeof(PermissionAuthorizeAttribute));
            Assert.AreEqual("Purchases.View", classAttr.ConstructorArguments[0].Value);
            foreach (var create in purchases.GetMethods().Where(m => m.Name == "Create"))
            {
                var attr = create.GetCustomAttributesData().Single(a => a.Constructor.DeclaringType == typeof(PermissionAuthorizeAttribute));
                Assert.AreEqual("Purchases.Edit", attr.ConstructorArguments[0].Value);
            }
        }

        // ---- #2 create-for-any room list ----

        [Test]
        public void CreateForAnyHolder_GetsOrganisationRoomList_AndIsNotPinnedToOwnDepartment()
        {
            var controller = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Controllers", "PurchaseRequestsController.cs");
            // 2026-11-02 (answer 2): the list goes through the shared scope; create-for-any keeps the organisation's rooms.
            StringAssert.Contains(": BuildScopedRequisitionDepartmentGroups(departmentId, activeDepartments, targetScope);", controller);
            StringAssert.DoesNotContain("? BuildRequisitionDepartmentGroups(departmentId)\n", controller.Replace("\r\n", "\n"));
            StringAssert.DoesNotContain("ApplyLockedUserDepartment(", controller,
                "the own-department overwrite applied to create-for-any holders with a department");
            StringAssert.Contains("if (model.DepartmentLocked)", controller);
        }

        [Test]
        public void IsDepartmentLocked_OnlyWithoutCreateForAnyAndAdmin()
        {
            Assert.IsTrue(RequisitionAccessRules.IsDepartmentLocked(false, false, 10));
            Assert.IsFalse(RequisitionAccessRules.IsDepartmentLocked(true, false, 10), "create-for-any picks any room");
            Assert.IsFalse(RequisitionAccessRules.IsDepartmentLocked(false, true, 10), "Company Admin picks any room");
            Assert.IsFalse(RequisitionAccessRules.IsDepartmentLocked(false, false, null), "no department: unchanged (#1 pending)");
        }

        // ---- #10 approve-any-stage and create-for-any are Company Admin only ----

        [Test]
        public void CanBypassApprovalStages_CompanyAdminOnly()
        {
            var viewAll = new FixedPermissionAuthorizationService("Assets.ViewAll");
            var none = new DenyAllAuthorizationService();
            Assert.IsTrue(RequisitionAccessRules.CanBypassApprovalStages(none,
                new Role { Name = SystemRoleNames.CompanyAdmin, IsSystemRole = true, IsActive = true }, "admin"));
            Assert.IsFalse(RequisitionAccessRules.CanBypassApprovalStages(viewAll,
                new Role { Name = "Finance Manager", IsSystemRole = false, IsActive = true }, "fin"),
                "Assets.ViewAll alone is not break-glass");
            Assert.IsFalse(RequisitionAccessRules.CanBypassApprovalStages(viewAll,
                new Role { Name = SystemRoleNames.CompanyAdmin, IsSystemRole = false, IsActive = true }, "x"),
                "a custom role renamed Company Admin is not the system role");
            Assert.IsTrue(RequisitionAccessRules.CanBypassApprovalStages(viewAll,
                new Role { Name = SystemRoleNames.PlatformAdmin, IsSystemRole = true, IsActive = true }, "support"),
                "platform support while impersonating");
            Assert.IsFalse(RequisitionAccessRules.CanBypassApprovalStages(none,
                new Role { Name = SystemRoleNames.PlatformAdmin, IsSystemRole = true, IsActive = true }, "support"));
            Assert.IsFalse(RequisitionAccessRules.CanBypassApprovalStages(viewAll, null, "x"));
        }

        [Test]
        public void ApprovalCallSites_UseCompanyAdminBypass_NotViewAll()
        {
            var baseController = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Controllers", "BaseController.cs");
            StringAssert.Contains("protected bool CanBypassApprovalStages()\r\n        {\r\n            return IsCurrentUserCompanyAdmin();".Replace("\r\n", "\n"),
                baseController.Replace("\r\n", "\n"));

            foreach (var file in new[] { "PurchaseRequestsController.cs", "TransfersController.cs", "AssetsController.cs" })
            {
                var text = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Controllers", file);
                StringAssert.DoesNotContain("GetCurrentUserRoleId(), IsCurrentUserSuperAdmin());", text, file);
                StringAssert.Contains("GetCurrentUserRoleId(), CanBypassApprovalStages());", text, file);
            }

            var pending = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Controllers", "PendingApprovalsController.cs");
            StringAssert.Contains("IsSuperAdmin = CanBypassApprovalStages(),", pending);
            var details = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Controllers", "PurchaseRequestsController.cs");
            StringAssert.Contains("var isSuperAdmin = CanBypassApprovalStages();", details);
            var assets = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Controllers", "AssetsController.cs");
            StringAssert.Contains("var isSuperAdmin = CanBypassApprovalStages();", assets);
            var metrics = QaSixRequisitionFormTests.Read("src", "AssetManagement.Application", "Services", "MetricsService.cs");
            StringAssert.Contains("RequisitionAccessRules.CanBypassApprovalStages(_authorizationService, role, userId)", metrics);
            StringAssert.DoesNotContain("var isSuperAdmin = AssetManagement.Application.Security.OrgCapabilities.HasOrgWideScope", metrics);
        }

        [Test]
        public void RequisitionTarget_ViewAllHolderWithDepartment_CannotRaiseForAnotherDepartment()
        {
            var service = CreateScope(new Role { Id = 5, Name = "Auditor", IsActive = true }, departmentId: 10,
                companyAdmin: false, permissions: new[] { "Purchases.Create", "Assets.ViewAll" });
            Assert.IsTrue(service.BypassesDepartmentScope, "still sees every asset");

            var ex = Assert.Throws<BusinessException>(() => service.EnsureCanCreateForRequisitionTarget(Room(30, DepartmentKind.Room)));
            StringAssert.Contains("outside your scope", ex.Message);
            Assert.DoesNotThrow(() => service.EnsureCanCreateForRequisitionTarget(Room(10, DepartmentKind.Room)), "own department");
        }

        [Test]
        public void RequisitionTarget_ViewAllHolderWithTransfer_NoLongerRaisesForAClassroomOutsideTheirDepartment()
        {
            // 2026-11-02 (answer 2): one shared scope rule; Assets.Transfer no longer widens raising to every classroom.
            var service = CreateScope(new Role { Id = 5, Name = "Auditor", IsActive = true }, departmentId: 10,
                companyAdmin: false, permissions: new[] { "Purchases.Create", "Assets.ViewAll", "Assets.Transfer" });
            Assert.Throws<BusinessException>(() => service.EnsureCanCreateForRequisitionTarget(Room(30, DepartmentKind.Class)));
            Assert.Throws<BusinessException>(() => service.EnsureCanCreateForRequisitionTarget(Room(31, DepartmentKind.Room)));
        }

        [Test]
        public void RequisitionTarget_CompanyAdminAndCreateForAny_MayRaiseForAnyRoom()
        {
            var companyAdminByScope = CreateScope(new Role { Id = 1, Name = "Company Admin", IsSystemRole = true, IsActive = true },
                departmentId: 10, companyAdmin: true, permissions: new string[0]);
            Assert.DoesNotThrow(() => companyAdminByScope.EnsureCanCreateForRequisitionTarget(Room(30, DepartmentKind.Room)));

            var companyAdminByRole = CreateScope(new Role { Id = 1, Name = "Company Admin", IsSystemRole = true, IsActive = true },
                departmentId: 10, companyAdmin: false, permissions: new[] { "Assets.ViewAll" });
            Assert.DoesNotThrow(() => companyAdminByRole.EnsureCanCreateForRequisitionTarget(Room(30, DepartmentKind.Room)));

            var createForAny = CreateScope(new Role { Id = 6, Name = "Procurement Officer", IsActive = true },
                departmentId: 10, companyAdmin: false, permissions: new[] { "Purchases.Create", "Purchases.CreateForAnyDepartment" });
            Assert.DoesNotThrow(() => createForAny.EnsureCanCreateForRequisitionTarget(Room(30, DepartmentKind.Room)));

            // 2026-11-02 correction 2: create-for-any without the create permission itself does not raise.
            var createForAnyOnly = CreateScope(new Role { Id = 6, Name = "Procurement Officer", IsActive = true },
                departmentId: 10, companyAdmin: false, permissions: new[] { "Purchases.CreateForAnyDepartment" });
            var refused = Assert.Throws<BusinessException>(() => createForAnyOnly.EnsureCanCreateForRequisitionTarget(Room(30, DepartmentKind.Room)));
            Assert.AreEqual(RequisitionTargetScope.MissingCreatePermissionMessage, refused.Message);

            // 2026-11-02 correction 1: no department = organisation-wide (not blocked), with Purchases.Create.
            var noDepartment = CreateScope(new Role { Id = 7, Name = "Staff", IsActive = true },
                departmentId: null, companyAdmin: false, permissions: new[] { "Purchases.Create" });
            Assert.DoesNotThrow(() => noDepartment.EnsureCanCreateForRequisitionTarget(Room(30, DepartmentKind.Room)),
                "2026-11-02 correction 1: no department = organisation-wide");
            Assert.IsTrue(noDepartment.GetRequisitionTargetScope().IsOrganizationWide);

            var noDepartmentNoCreate = CreateScope(new Role { Id = 7, Name = "Staff", IsActive = true },
                departmentId: null, companyAdmin: false, permissions: new[] { "Assets.View" });
            var ex = Assert.Throws<BusinessException>(() => noDepartmentNoCreate.EnsureCanCreateForRequisitionTarget(Room(30, DepartmentKind.Room)),
                "2026-11-02 correction 2: organisation-wide users need Purchases.Create too");
            Assert.AreEqual(RequisitionTargetScope.MissingCreatePermissionMessage, ex.Message);

            var noDepartmentCreateForAny = CreateScope(new Role { Id = 8, Name = "Facilities Manager", IsActive = true },
                departmentId: null, companyAdmin: false, permissions: new[] { "Purchases.Create", "Purchases.CreateForAnyDepartment" });
            Assert.DoesNotThrow(() => noDepartmentCreateForAny.EnsureCanCreateForRequisitionTarget(Room(30, DepartmentKind.Room)));
        }

        // ---- #9 pending-approvals report uses the requisition list rule ----

        [Test]
        public void ResolveListScope_MatchesRequisitionList()
        {
            var approve = RequisitionAccessRules.ResolveListScope(new StrictDepartmentScopeService(10), true);
            Assert.IsTrue(approve.BypassDepartmentScope);
            Assert.IsTrue(approve.Includes(20));

            var own = RequisitionAccessRules.ResolveListScope(new StrictDepartmentScopeService(10), false);
            Assert.IsTrue(own.Includes(10));
            Assert.IsFalse(own.Includes(20));

            var deny = RequisitionAccessRules.ResolveListScope(new StrictDepartmentScopeService(null), false);
            Assert.IsTrue(deny.DenyDepartmentScope);
            Assert.IsFalse(deny.Includes(10));

            Assert.IsTrue(RequisitionAccessRules.ResolveListScope(new PermissiveDepartmentScopeService(), false).Includes(20));
        }

        [Test]
        public void PendingApprovalsCsv_PurchasesApproveHolder_SeesEveryPendingRequisition()
        {
            var csv = ExportPendingCsv(new FixedPermissionAuthorizationService("Purchases.Approve"), 247);
            StringAssert.Contains("REQ-DEPT-10", csv);
            StringAssert.Contains("REQ-DEPT-20", csv);
        }

        [Test]
        public void PendingApprovalsCsv_ScopedUserWithoutApprove_SeesOwnDepartmentOnly()
        {
            var csv = ExportPendingCsv(new DenyAllAuthorizationService(), 10);
            StringAssert.Contains("REQ-DEPT-10", csv);
            StringAssert.DoesNotContain("REQ-DEPT-20", csv);
        }

        [Test]
        public void PendingApprovalsReport_BothExportsUseTheSharedScope()
        {
            var csvService = QaSixRequisitionFormTests.Read("src", "AssetManagement.Application", "Services", "ReportService.cs");
            var docService = QaSixRequisitionFormTests.Read("src", "AssetManagement.Application", "Services", "ReportDocumentService.cs");
            var list = QaSixRequisitionFormTests.Read("src", "AssetManagement.Application", "Services", "Purchases", "PurchaseRequestService.cs");
            foreach (var text in new[] { csvService, docService, list })
            {
                StringAssert.Contains("RequisitionAccessRules.ResolveListScope(", text);
            }

            StringAssert.Contains("if (!purchaseListScope.Includes(purchaseRequest.DepartmentId))", docService);
            StringAssert.Contains("if (!purchaseListScope.Includes(purchaseRequest.DepartmentId))", csvService);
        }

        // ---- #7 requisition approver opens tagged assets read-only ----

        [Test]
        public void TaggedAsset_CurrentStageApprover_CanView()
        {
            var unitOfWork = SeedRequisitionWithTaggedAsset(ApprovalStatus.Pending);
            var users = Users();
            Assert.IsTrue(ApprovalWorkflowHelper.CanViewAssetTaggedOnRequisitionApproval(unitOfWork, users, "finance-user",
                unitOfWork.Repository<Asset>().GetById(7)));
            Assert.IsFalse(ApprovalWorkflowHelper.CanViewAssetTaggedOnRequisitionApproval(unitOfWork, users, "staff-user",
                unitOfWork.Repository<Asset>().GetById(7)), "not an approver of the requisition");
            Assert.IsFalse(ApprovalWorkflowHelper.CanViewAssetTaggedOnRequisitionApproval(unitOfWork, users, "finance-user",
                unitOfWork.Repository<Asset>().GetById(8)), "asset not tagged on any requisition");
        }

        [Test]
        public void TaggedAsset_ApproverWhoActed_CanStillView_AfterTheRequisitionIsApproved()
        {
            var unitOfWork = SeedRequisitionWithTaggedAsset(ApprovalStatus.Approved);
            unitOfWork.Seed(new PurchaseApprovalAction
            {
                Id = 1,
                OrganizationId = 1,
                PurchaseRequestId = 1,
                StageNumber = 1,
                RoleId = 4,
                ApproverUserId = "finance-user",
                Decision = ApprovalStatus.Approved,
                DecisionDate = DateTime.UtcNow,
                IsActive = true
            });
            var users = Users();
            Assert.IsTrue(ApprovalWorkflowHelper.CanViewAssetTaggedOnRequisitionApproval(unitOfWork, users, "finance-user",
                unitOfWork.Repository<Asset>().GetById(7)));
            Assert.IsFalse(ApprovalWorkflowHelper.CanViewAssetTaggedOnRequisitionApproval(unitOfWork, users, "other-finance",
                unitOfWork.Repository<Asset>().GetById(7)), "no longer pending and never acted");
        }

        [Test]
        public void AssetService_GetById_RequisitionApprover_GetsReadOnlyView()
        {
            var unitOfWork = SeedRequisitionWithTaggedAsset(ApprovalStatus.Pending);
            var users = Users();
            users.Seed(new UserVm { Id = "test-user", RoleId = 4, DepartmentId = 247, IsActive = true });
            var service = TestServiceFactory.CreateAssetService(unitOfWork, new StrictDepartmentScopeService(247), users);

            var tagged = service.GetById(7);
            Assert.IsNotNull(tagged, "approver opens the tagged asset");
            Assert.IsTrue(tagged.ViewedForRequisitionApprovalOnly);
            Assert.IsNull(service.GetById(8), "an untagged asset of another department stays hidden");
        }

        [Test]
        public void RequisitionApproverView_IsReadOnly()
        {
            var view = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Views", "Assets", "Details.cshtml");
            StringAssert.Contains("var assetActionsAllowed = !Model.ViewedForRequisitionApprovalOnly;", view);
            foreach (var flag in new[] { "canEditAsset", "canAssignAsset", "canTransferAsset", "canReturnAsset", "canCreateIncident", "canCreateClaim", "canRequestDisposal" })
            {
                StringAssert.Contains("var " + flag + " = assetActionsAllowed && ", view, flag);
            }

            var assets = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Controllers", "AssetsController.cs");
            StringAssert.Contains("!details.ViewedAsCustodianOnly && !details.ViewedForRequisitionApprovalOnly", assets);
            var documents = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Controllers", "DocumentsController.cs");
            StringAssert.Contains("uploadAsset.ViewedForRequisitionApprovalOnly", documents);
            StringAssert.Contains("deleteAsset.ViewedForRequisitionApprovalOnly", documents);
            var scope = QaSixRequisitionFormTests.Read("src", "AssetManagement.Application", "Services", "Security", "DepartmentScopeService.cs");
            StringAssert.DoesNotContain("CanViewAssetTaggedOnRequisitionApproval", scope, "EnsureCanAccessAsset (edits) is unchanged");
        }

        // ---- helpers ----

        private static Department Room(int id, DepartmentKind kind)
        {
            return new Department { Id = id, Name = "Room " + id, IsActive = true, IsRequisitionTarget = true, DepartmentKind = kind };
        }

        private static DepartmentScopeService CreateScope(Role role, int? departmentId, bool companyAdmin, string[] permissions)
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(role);
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "u1", RoleId = role.Id, DepartmentId = departmentId, IsActive = true });
            return new DepartmentScopeService(
                unitOfWork,
                new FakeCurrentUserContext("u1"),
                users,
                new FakeOrganizationScopeService(companyAdmin: companyAdmin),
                new FixedPermissionAuthorizationService(permissions));
        }

        private static string ExportPendingCsv(IAuthorizationService authorization, int scopedDepartmentId)
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department { Id = 10, Name = "Dept 10", IsActive = true });
            unitOfWork.Seed(new Department { Id = 20, Name = "Dept 20", IsActive = true });
            unitOfWork.Seed(new PurchaseRequest
            {
                Id = 1, OrganizationId = 1, RequestNumber = "REQ-DEPT-10", DepartmentId = 10, RequestedById = "a",
                ApprovalStatus = ApprovalStatus.Pending, CurrentApprovalStage = 1, IsActive = true, CreatedAt = DateTime.UtcNow
            });
            unitOfWork.Seed(new PurchaseRequest
            {
                Id = 2, OrganizationId = 1, RequestNumber = "REQ-DEPT-20", DepartmentId = 20, RequestedById = "b",
                ApprovalStatus = ApprovalStatus.Pending, CurrentApprovalStage = 1, IsActive = true, CreatedAt = DateTime.UtcNow
            });
            var service = new ReportService(
                unitOfWork,
                new StrictDepartmentScopeService(scopedDepartmentId),
                new PermissiveMetricsService(),
                new FakeDashboardQueryService(),
                new FakeOrganizationScopeService(),
                new FakeAssetQueryService(),
                authorization,
                new FakeCurrentUserContext("report-user"));
            return Encoding.UTF8.GetString(service.ExportPendingApprovalsAgingCsv());
        }

        private static FakeUserService Users()
        {
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "finance-user", RoleId = 4, DepartmentId = 247, IsActive = true });
            users.Seed(new UserVm { Id = "other-finance", RoleId = 4, DepartmentId = 247, IsActive = true });
            users.Seed(new UserVm { Id = "staff-user", RoleId = 5, DepartmentId = 247, IsActive = true });
            return users;
        }

        private static FakeUnitOfWork SeedRequisitionWithTaggedAsset(ApprovalStatus status)
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Role { Id = 4, Name = "Finance", IsActive = true });
            unitOfWork.Seed(new Role { Id = 5, Name = "Staff Custom", IsActive = true });
            unitOfWork.Seed(new Department { Id = 10, Name = "Science Lab", IsActive = true, IsRequisitionTarget = true });
            unitOfWork.Seed(new Department { Id = 247, Name = "Finance", IsActive = true });
            unitOfWork.Seed(new Asset { Id = 7, OrganizationId = 1, AssetTag = "LAB-7", AssetName = "Microscope", DepartmentId = 10, IsActive = true, CurrentStatus = AssetStatus.InStore });
            unitOfWork.Seed(new Asset { Id = 8, OrganizationId = 1, AssetTag = "LAB-8", AssetName = "Bench", DepartmentId = 10, IsActive = true, CurrentStatus = AssetStatus.InStore });
            unitOfWork.Seed(new PurchaseRequest
            {
                Id = 1,
                OrganizationId = 1,
                RequestNumber = "REQ-1",
                DepartmentId = 10,
                RequestedById = "lab-head",
                ApprovalStatus = status,
                ApprovalStageRoleIds = "4",
                CurrentApprovalStage = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            unitOfWork.Seed(new PurchaseRequestLine
            {
                Id = 1,
                OrganizationId = 1,
                PurchaseRequestId = 1,
                LineNumber = 1,
                Description = "Repair",
                Quantity = 1,
                TargetAssetId = 7,
                IsActive = true
            });
            return unitOfWork;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web.Mvc;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
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
    /// Allan's answers of 2026-11-02 (requisition role access, phase 2): 1 no department = cannot raise; 2 department
    /// subtree scope shared by rooms, people and the server; 3 tagging stays org-wide; 4 approvers see all; 5 PO raising
    /// is permission-based; 6 Assets.ViewAll is read scope only; 7 Users.View is read-only; audit #11 preview scope;
    /// Receive button matches its action.
    /// </summary>
    [TestFixture]
    public class RequisitionScopeAnswersTests
    {
        private const int Org = 10;

        private static Func<string, bool> Grants(params string[] codes)
        {
            var set = new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);
            return code => set.Contains(code);
        }

        // Science (10, own department, requisition target) > Lab (11, room) ; Science > Inactive wing (12) > Store (13);
        // Science > Physics (14, sub-department) > Physics lab (15, room); Arts room (20) elsewhere.
        private static List<DepartmentVm> Tree()
        {
            return new List<DepartmentVm>
            {
                new DepartmentVm { Id = 10, Name = "Science", IsActive = true, IsRequisitionTarget = true, DepartmentKind = DepartmentKind.Administrative },
                new DepartmentVm { Id = 11, Name = "Lab", ParentDepartmentId = 10, IsActive = true, IsRequisitionTarget = true, DepartmentKind = DepartmentKind.Room },
                new DepartmentVm { Id = 12, Name = "Old wing", ParentDepartmentId = 10, IsActive = false, IsRequisitionTarget = true, DepartmentKind = DepartmentKind.SubDepartment },
                new DepartmentVm { Id = 13, Name = "Store", ParentDepartmentId = 12, IsActive = true, IsRequisitionTarget = true, DepartmentKind = DepartmentKind.Room },
                new DepartmentVm { Id = 14, Name = "Physics", ParentDepartmentId = 10, IsActive = true, IsRequisitionTarget = true, DepartmentKind = DepartmentKind.SubDepartment },
                new DepartmentVm { Id = 15, Name = "Physics lab", ParentDepartmentId = 14, IsActive = true, IsRequisitionTarget = true, DepartmentKind = DepartmentKind.Room },
                new DepartmentVm { Id = 20, Name = "Arts room", IsActive = true, IsRequisitionTarget = true, DepartmentKind = DepartmentKind.Room }
            };
        }

        private static FakeUnitOfWork SeedTree()
        {
            var uow = new FakeUnitOfWork();
            foreach (var d in Tree())
            {
                uow.Seed(new Department
                {
                    Id = d.Id, Name = d.Name, Code = "D" + d.Id, ParentDepartmentId = d.ParentDepartmentId, IsActive = d.IsActive,
                    IsRequisitionTarget = d.IsRequisitionTarget, DepartmentKind = d.DepartmentKind, OrganizationId = Org
                });
            }

            uow.Seed(new Role { Id = 5, Name = "Department Head", IsActive = true });
            uow.Seed(new SystemSetting { Id = 1, SettingKey = "Approval.Purchase.Enabled", SettingValue = "false", IsActive = true });
            return uow;
        }

        // ---- answers 1 + 2: the shared scope rule ----

        [Test]
        public void Scope_NoDepartmentAndNoCreateForAny_IsBlocked()
        {
            var scope = RequisitionTargetScope.Resolve(false, false, (int?)null, Tree());
            Assert.IsTrue(scope.IsBlocked);
            Assert.IsFalse(scope.Contains(11));
            Assert.IsEmpty(scope.AllowedRoomOptionIds(Tree()));
            StringAssert.Contains("no department", RequisitionTargetScope.BlockedMessage);
            var ex = Assert.Throws<BusinessException>(() => scope.EnsureContains(new Department { Id = 11 }));
            Assert.AreEqual(RequisitionTargetScope.BlockedMessage, ex.Message);
        }

        [Test]
        public void Scope_CreateForAnyOrTenantAdmin_IsOrganisationWide_EvenWithoutDepartment()
        {
            Assert.IsTrue(RequisitionTargetScope.Resolve(true, false, (int?)null, Tree()).IsOrganizationWide);
            Assert.IsTrue(RequisitionTargetScope.Resolve(false, true, (int?)null, Tree()).IsOrganizationWide);
            var any = RequisitionTargetScope.Resolve(true, false, 10, Tree());
            Assert.IsTrue(any.Contains(20));
            CollectionAssert.AreEquivalent(new[] { 11, 13, 15, 20 }, any.AllowedRoomOptionIds(Tree()).ToList(),
                "valid rooms of the organisation (the picker hides rooms under an inactive parent separately)");
        }

        [Test]
        public void Scope_DepartmentUser_OwnDepartmentAndActiveSubtree()
        {
            var scope = RequisitionTargetScope.Resolve(false, false, 10, Tree());
            Assert.AreEqual(RequisitionTargetScopeMode.DepartmentSubtree, scope.Mode);
            CollectionAssert.AreEquivalent(new[] { 10, 11, 14, 15 }, scope.DepartmentIds.ToList(), "inactive unit cuts its branch");
            Assert.IsFalse(scope.Contains(20), "another department");
            Assert.IsFalse(scope.Contains(13), "under an inactive unit");
            // Room list: own department (what they raised for before) + valid rooms below; not the sub-department itself.
            CollectionAssert.AreEquivalent(new[] { 10, 11, 15 }, scope.AllowedRoomOptionIds(Tree()).ToList());
            Assert.IsFalse(scope.LocksToOwnDepartment(Tree()), "rooms below: pick from the scoped list");
        }

        [Test]
        public void Scope_DepartmentUserWithNothingBelow_StaysLockedToOwnDepartment()
        {
            var scope = RequisitionTargetScope.Resolve(false, false, 20, Tree());
            Assert.IsTrue(scope.LocksToOwnDepartment(Tree()));
            Assert.IsFalse(RequisitionTargetScope.Resolve(true, false, 20, Tree()).LocksToOwnDepartment(Tree()));
        }

        [Test]
        public void Scope_IsCycleSafe()
        {
            var pairs = new[]
            {
                new KeyValuePair<int, int?>(1, 2),
                new KeyValuePair<int, int?>(2, 1),
                new KeyValuePair<int, int?>(3, 2)
            };
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, RequisitionTargetScope.SelfAndActiveDescendants(1, pairs).ToList());
        }

        [Test]
        public void Scope_PersonList_OnlyPeopleRoutedInsideTheScope()
        {
            var options = new List<RequisitionPersonTargetOptionVm>
            {
                new RequisitionPersonTargetOptionVm { UserId = "lab-tech", RoutingDepartmentId = 11 },
                new RequisitionPersonTargetOptionVm { UserId = "hod", RoutingDepartmentId = 10 },
                new RequisitionPersonTargetOptionVm { UserId = "artist", RoutingDepartmentId = 20 },
                new RequisitionPersonTargetOptionVm { UserId = "nobody", RoutingDepartmentId = null }
            };
            var dept = RequisitionTargetScope.Resolve(false, false, 10, Tree());
            CollectionAssert.AreEquivalent(new[] { "lab-tech", "hod" }, dept.FilterPersonOptions(options).Select(x => x.UserId).ToList());
            Assert.AreEqual(4, RequisitionTargetScope.Resolve(true, false, 10, Tree()).FilterPersonOptions(options).Count);
            Assert.IsEmpty(RequisitionTargetScope.BlockedScope().FilterPersonOptions(options));
        }

        [Test]
        public void ServerCheck_DepartmentScopeService_UsesTheSubtree()
        {
            var uow = SeedTree();
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "hod", RoleId = 5, DepartmentId = 10, IsActive = true });
            var scope = new DepartmentScopeService(uow, new FakeCurrentUserContext("hod"), users,
                new FakeOrganizationScopeService(organizationId: Org, companyAdmin: false), new FixedPermissionAuthorizationService("Purchases.Create"));
            var repo = uow.Repository<Department>();

            Assert.DoesNotThrow(() => scope.EnsureCanCreateForRequisitionTarget(repo.GetById(11)), "room under own department");
            Assert.DoesNotThrow(() => scope.EnsureCanCreateForRequisitionTarget(repo.GetById(15)), "room two levels below");
            Assert.DoesNotThrow(() => scope.EnsureCanCreateForRequisitionTarget(repo.GetById(10)), "own department");
            var ex = Assert.Throws<BusinessException>(() => scope.EnsureCanCreateForRequisitionTarget(repo.GetById(20)));
            Assert.AreEqual(RequisitionTargetScope.OutsideScopeMessage, ex.Message);
        }

        [Test]
        public void ServerCheck_NoDepartment_IsBlocked_WithAuthorizationService()
        {
            var uow = SeedTree();
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "depthead", RoleId = 5, DepartmentId = null, IsActive = true });
            var scope = new DepartmentScopeService(uow, new FakeCurrentUserContext("depthead"), users,
                new FakeOrganizationScopeService(organizationId: Org, companyAdmin: false), new FixedPermissionAuthorizationService("Purchases.Create", "Assets.View"));
            var ex = Assert.Throws<BusinessException>(() => scope.EnsureCanCreateForRequisitionTarget(uow.Repository<Department>().GetById(11)));
            Assert.AreEqual(RequisitionTargetScope.BlockedMessage, ex.Message);
            Assert.IsTrue(scope.GetRequisitionTargetScope().IsBlocked);
        }

        private static PurchaseRequestService RequestService(FakeUnitOfWork uow, FakeUserService users, string currentUserId, params string[] permissions)
        {
            var scope = new DepartmentScopeService(uow, new FakeCurrentUserContext(currentUserId), users,
                new FakeOrganizationScopeService(organizationId: Org, companyAdmin: false), new FixedPermissionAuthorizationService(permissions));
            return new PurchaseRequestService(
                uow,
                new NoOpAuditWriter(),
                users,
                scope,
                new FakeOrganizationScopeService(organizationId: Org, companyAdmin: false),
                new NoOpOutboxWriter(),
                new NoOpWebhookService(),
                new FakeApprovalWorkflowEngine(uow, new NoOpAuditWriter()),
                new FakeOperationsQueryRepository());
        }

        private static PurchaseRequestCreateVm RoomForm(int departmentId)
        {
            return new PurchaseRequestCreateVm
            {
                DepartmentId = departmentId,
                RequisitionTargetKind = RequisitionTargetRules.KindRoom,
                ItemDescription = "Beakers",
                Justification = "Lab",
                Quantity = 1,
                Currency = "KES"
            };
        }

        [Test]
        public void Submit_DepartmentUser_RoomInSubtreeAccepted_OtherDepartmentRefused()
        {
            var uow = SeedTree();
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "hod", RoleId = 5, DepartmentId = 10, IsActive = true });
            var service = RequestService(uow, users, "hod", "Purchases.Create");

            var id = service.SubmitCompleted(uow, RoomForm(15), "hod");
            Assert.AreEqual(15, uow.Repository<PurchaseRequest>().GetById(id).DepartmentId);

            var ex = Assert.Throws<BusinessException>(() => service.SubmitCompleted(uow, RoomForm(20), "hod"));
            Assert.AreEqual(RequisitionTargetScope.OutsideScopeMessage, ex.Message);
        }

        [Test]
        public void Submit_NoDepartment_Refused()
        {
            var uow = SeedTree();
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "depthead", RoleId = 5, DepartmentId = null, IsActive = true });
            var service = RequestService(uow, users, "depthead", "Purchases.Create");
            var ex = Assert.Throws<BusinessException>(() => service.SubmitCompleted(uow, RoomForm(11), "depthead"));
            Assert.AreEqual(RequisitionTargetScope.BlockedMessage, ex.Message);
            Assert.AreEqual(0, uow.Repository<PurchaseRequest>().GetAll().Count());
        }

        [Test]
        public void Submit_PersonOutsideTheScope_Refused_PersonInside_Accepted()
        {
            var uow = SeedTree();
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "hod", RoleId = 5, DepartmentId = 10, IsActive = true });
            users.Seed(new UserVm { Id = "artist", FirstName = "Ann", LastName = "Artist", RoleId = 5, DepartmentId = 20, IsActive = true });
            users.Seed(new UserVm { Id = "physicist", FirstName = "Phil", LastName = "Physics", RoleId = 5, DepartmentId = 15, IsActive = true });
            var service = RequestService(uow, users, "hod", "Purchases.Create");

            var outside = RoomForm(10);
            outside.RequisitionTargetKind = RequisitionTargetRules.KindPerson;
            outside.TargetUserId = "artist";
            outside.TargetPlacement = RequisitionTargetRules.PlacementPersonRoom;
            var ex = Assert.Throws<BusinessException>(() => service.SubmitCompleted(uow, outside, "hod"));
            StringAssert.Contains("outside your department", ex.Message);

            var inside = RoomForm(10);
            inside.RequisitionTargetKind = RequisitionTargetRules.KindPerson;
            inside.TargetUserId = "physicist";
            inside.TargetPlacement = RequisitionTargetRules.PlacementPersonRoom;
            var id = service.SubmitCompleted(uow, inside, "hod");
            Assert.AreEqual(15, uow.Repository<PurchaseRequest>().GetById(id).DepartmentId);
        }

        [Test]
        public void Controller_CreateAndPreview_UseTheSharedScope()
        {
            var controller = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Controllers", "PurchaseRequestsController.cs");
            StringAssert.Contains("RequisitionTargetScope.Resolve(", controller);
            StringAssert.Contains("return CannotRaise(returnUrl);", controller);
            StringAssert.Contains("targetScope.FilterPersonOptions(_purchaseRequestService.GetPersonTargetOptions())", controller);
            StringAssert.Contains("!targetScope.Contains(departmentId.Value)", controller, "audit #11: preview only in-scope departments");
            StringAssert.Contains("ViewBag.RequisitionRaiseBlockedMessage", controller);
            var catalog = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Controllers", "PurchaseRequestsController.Catalog.cs");
            StringAssert.Contains("ApplyRequisitionTargetLock(model);", catalog, "line-path preview uses the same lock as submit");
            var csproj = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "AssetManagement.Web.csproj");
            StringAssert.Contains("Views\\PurchaseRequests\\CannotRaise.cshtml", csproj);
            var index = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Views", "PurchaseRequests", "Index.cshtml");
            StringAssert.Contains("data-am-req-raise-blocked", index);
        }

        // ---- answer 2 list / requisition visibility ----

        [Test]
        public void ListScope_DepartmentUser_SeesTheSubtree()
        {
            var pairs = Tree().Where(x => x.IsActive).Select(x => new KeyValuePair<int, int?>(x.Id, x.ParentDepartmentId)).ToList();
            var scope = RequisitionAccessRules.ResolveListScope(new StrictDepartmentScopeService(10), false, pairs);
            Assert.IsTrue(scope.Includes(10));
            Assert.IsTrue(scope.Includes(15));
            Assert.IsFalse(scope.Includes(20));
            // answer 4: every approver keeps the organisation-wide view.
            Assert.IsTrue(RequisitionAccessRules.ResolveListScope(new StrictDepartmentScopeService(10), true, pairs).Includes(20));
        }

        [Test]
        public void ListSql_FiltersBySubtree()
        {
            var sql = QaSixRequisitionFormTests.Read("src", "AssetManagement.Infrastructure", "Queries", "OperationsQueryRepository.cs");
            StringAssert.Contains("PurchaseRequestDepartmentScopeSql", sql);
            StringAssert.Contains("sd7.[ParentDepartmentId] = @DepartmentId", sql);
            StringAssert.DoesNotContain("AND p.[DepartmentId] = @DepartmentId))", sql);
        }

        // ---- answer 3: tagging stays organisation-wide (still organisation-scoped) ----

        [Test]
        public void Tagging_StaysOrganisationWide()
        {
            var controller = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Controllers", "PurchaseRequestsController.cs");
            StringAssert.Contains("OrganizationWide = true", controller);
            var service = QaSixRequisitionFormTests.Read("src", "AssetManagement.Application", "Services", "Purchases", "PurchaseRequestService.cs");
            StringAssert.Contains("Selected asset does not belong to this organization.", service);
        }

        // ---- answer 5: PO raising is permission-based ----

        [Test]
        public void PurchaseOrderPermission_IsViewPlusEdit()
        {
            Assert.IsTrue(RequisitionAccessRules.HasPurchaseOrderPermission(Grants("Purchases.View", "Purchases.Edit")));
            Assert.IsFalse(RequisitionAccessRules.HasPurchaseOrderPermission(Grants("Purchases.Edit")));
            Assert.IsFalse(RequisitionAccessRules.HasPurchaseOrderPermission(Grants("Purchases.View", "Purchases.Approve")));
            Assert.IsFalse(RequisitionAccessRules.HasPurchaseOrderPermission(null));
        }

        [Test]
        public void PurchaseOrder_ButtonActionAndServiceAgree()
        {
            var details = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Controllers", "PurchaseRequestsController.cs");
            StringAssert.Contains("PurchaseOrderRaiserRules.CanRaise(\r\n                RequisitionAccessRules.HasPurchaseOrderPermission(HasPermission));".Replace("\r\n", "\n"), details.Replace("\r\n", "\n"));
            var purchases = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Controllers", "PurchasesController.cs");
            StringAssert.Contains("RequisitionAccessRules.HasPurchaseOrderPermission(HasPermission)", purchases);
            StringAssert.DoesNotContain("can raise the purchase order for this requisition", purchases);
            var view = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Views", "PurchaseRequests", "Details.cshtml");
            StringAssert.DoesNotContain("can record it.", view);
            StringAssert.Contains("Purchase order expected from:", view);
        }

        // ---- Receive button matches ReceiveForRequisition / Receive ----

        [Test]
        public void ReceiveButton_NeedsPurchasesViewAndAssetsReceive()
        {
            Assert.IsTrue(RequisitionAccessRules.CanShowReceive(Grants("Purchases.View", "Assets.Receive")));
            Assert.IsFalse(RequisitionAccessRules.CanShowReceive(Grants("Assets.Receive")), "PurchasesController class filter is Purchases.View");
            Assert.IsFalse(RequisitionAccessRules.CanShowReceive(Grants("Purchases.View")));

            var view = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Views", "PurchaseRequests", "Details.cshtml");
            StringAssert.DoesNotContain("Html.HasPermission(\"Assets.Receive\")", view);
            StringAssert.Contains("RequisitionAccessRules.CanShowReceive(", view);

            var purchases = typeof(PurchasesController);
            var classAttr = purchases.GetCustomAttributesData().Single(a => a.Constructor.DeclaringType == typeof(PermissionAuthorizeAttribute));
            Assert.AreEqual("Purchases.View", classAttr.ConstructorArguments[0].Value);
            foreach (var name in new[] { "Receive", "ReceiveForRequisition" })
            {
                foreach (var method in purchases.GetMethods().Where(m => m.Name == name))
                {
                    var attr = method.GetCustomAttributesData().Single(a => a.Constructor.DeclaringType == typeof(PermissionAuthorizeAttribute));
                    Assert.AreEqual("Assets.Receive", attr.ConstructorArguments[0].Value, name);
                }
            }
        }

        // ---- answer 6: Assets.ViewAll is read scope only ----

        [Test]
        public void ViewAll_GrantsNoWritePowers()
        {
            var viewAll = new FixedPermissionAuthorizationService("Assets.View", "Assets.ViewAll");
            Assert.IsFalse(RequisitionAccessRules.CanBypassApprovalStages(viewAll,
                new Role { Name = "Finance Officer", IsActive = true }, "finance"));
            Assert.IsFalse(ClassificationInlineCreatePolicy.CanCreate(ClassificationCreateOrigin.Requisition, Grants("Assets.View", "Assets.ViewAll"), false));
            Assert.IsTrue(RequisitionTargetScope.Resolve(false, false, (int?)null, Tree()).IsBlocked, "ViewAll does not unlock raising");

            var baseController = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Controllers", "BaseController.cs");
            StringAssert.Contains("ClassificationInlineCreatePolicy.CanCreate(origin, HasPermission, IsCurrentUserCompanyAdmin());", baseController);
            StringAssert.DoesNotContain("ClassificationInlineCreatePolicy.CanCreate(origin, HasPermission, IsCurrentUserSuperAdmin())", baseController);
            var normalized = baseController.Replace("\r\n", "\n");
            var start = normalized.IndexOf("protected bool CanRequestForOthers(", StringComparison.Ordinal);
            Assert.Greater(start, 0);
            var body = normalized.Substring(start, 600);
            StringAssert.DoesNotContain("IsCurrentUserSuperAdmin()", body);

            var assetDetails = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Views", "Assets", "Details.cshtml");
            foreach (var code in new[] { "Assets.Edit", "Assets.Assign", "Assets.Transfer", "Assets.Dispose" })
            {
                StringAssert.Contains("Html.HasPermission(\"" + code + "\")", assetDetails, code + " stays permission-gated");
            }
        }

        // ---- answer 7: Users.View is read-only ----

        [Test]
        public void UsersView_GivesNoUserEditRights()
        {
            var users = typeof(UsersController);
            var readOnly = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Users.View", "Users.ViewDepartment" };
            var posts = users.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes(typeof(HttpPostAttribute), true).Length > 0)
                .ToList();
            Assert.IsNotEmpty(posts);
            foreach (var post in posts)
            {
                var codes = post.GetCustomAttributesData()
                    .Where(a => a.Constructor.DeclaringType == typeof(PermissionAuthorizeAttribute) || a.Constructor.DeclaringType == typeof(AnyPermissionAuthorizeAttribute))
                    .SelectMany(a => a.ConstructorArguments)
                    .SelectMany(arg => arg.Value is System.Collections.ObjectModel.ReadOnlyCollection<CustomAttributeTypedArgument>
                        ? ((System.Collections.ObjectModel.ReadOnlyCollection<CustomAttributeTypedArgument>)arg.Value).Select(x => x.Value as string)
                        : new[] { arg.Value as string })
                    .Where(x => x != null)
                    .ToList();
                Assert.IsNotEmpty(codes, post.Name + " has its own write permission");
                Assert.IsFalse(codes.Any(readOnly.Contains), post.Name + " must not accept a read permission");
            }
        }
    }
}

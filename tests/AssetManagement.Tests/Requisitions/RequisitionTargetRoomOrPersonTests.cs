using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using AssetManagement.Web.Controllers;
using AssetManagement.Web.Filters;
using NUnit.Framework;

namespace AssetManagement.Tests.Requisitions
{
    /// <summary>Migration 110: "Requisition target (room or person)", the tagging rule and the inline-create gate.</summary>
    [TestFixture]
    public class RequisitionTargetRoomOrPersonTests
    {
        private const int RoomId = 30;
        private const int AdminId = 5;

        // ---- RequisitionTargetRules -------------------------------------------------------------------------------

        [Test]
        public void ResolvePlacement_NoRoom_IsAlwaysCompanyCustody_WithoutAsking()
        {
            Assert.AreEqual(RequisitionTargetRules.PlacementCompanyCustody, RequisitionTargetRules.ResolvePlacement(false, null));
            Assert.AreEqual(RequisitionTargetRules.PlacementCompanyCustody, RequisitionTargetRules.ResolvePlacement(false, "PersonRoom"), "no room: a posted PersonRoom is ignored");
        }

        [Test]
        public void ResolvePlacement_WithRoom_MustBeChosen()
        {
            var ex = Assert.Throws<BusinessException>(() => RequisitionTargetRules.ResolvePlacement(true, " ", "Jane"));
            StringAssert.Contains("Jane's room", ex.Message);
            Assert.AreEqual(RequisitionTargetRules.PlacementPersonRoom, RequisitionTargetRules.ResolvePlacement(true, "personroom"));
            Assert.AreEqual(RequisitionTargetRules.PlacementCompanyCustody, RequisitionTargetRules.ResolvePlacement(true, "CompanyCustody"));
        }

        [Test]
        public void NormalizeKind_AcceptsRoomAndPersonOnly()
        {
            Assert.AreEqual("Room", RequisitionTargetRules.NormalizeKind(" room "));
            Assert.AreEqual("Person", RequisitionTargetRules.NormalizeKind("PERSON"));
            Assert.IsNull(RequisitionTargetRules.NormalizeKind("Department"));
            Assert.IsNull(RequisitionTargetRules.NormalizeKind(null));
        }

        [Test]
        public void FindAssignedRoom_ClassTeacherOrRoomCustodian_ThenRoomLikeProfile()
        {
            var departments = new List<Department>
            {
                new Department { Id = 1, Name = "Admin", DepartmentKind = DepartmentKind.Administrative, IsActive = true },
                new Department { Id = 2, Name = "Grade 4 East", DepartmentKind = DepartmentKind.Class, ClassTeacherUserId = "t-1", IsActive = true },
                new Department { Id = 3, Name = "Lab", DepartmentKind = DepartmentKind.Room, RoomCustodianUserId = "c-1", IsActive = true },
                new Department { Id = 4, Name = "Old class", DepartmentKind = DepartmentKind.Class, ClassTeacherUserId = "x-1", IsActive = false },
                new Department { Id = 6, Name = "Library", DepartmentKind = DepartmentKind.Room, IsActive = true }
            };
            Assert.AreEqual(2, RequisitionTargetRules.FindAssignedRoom("T-1", null, departments).Id, "class teacher (case-insensitive id)");
            Assert.AreEqual(3, RequisitionTargetRules.FindAssignedRoom("c-1", 1, departments).Id, "room custodian");
            Assert.IsNull(RequisitionTargetRules.FindAssignedRoom("x-1", null, departments), "inactive class does not count");
            Assert.IsNull(RequisitionTargetRules.FindAssignedRoom("u-1", 1, departments), "administrative profile department is not a room");
            Assert.AreEqual(6, RequisitionTargetRules.FindAssignedRoom("u-1", 6, departments).Id, "room-like profile department");
        }

        [Test]
        public void ResolveRoutingDepartment_LockedFirst_ThenRoom_ThenProfile()
        {
            var locked = new Department { Id = 1, IsActive = true, IsRequisitionTarget = true };
            var room = new Department { Id = 2, IsActive = true, IsRequisitionTarget = true };
            var notTarget = new Department { Id = 3, IsActive = true, IsRequisitionTarget = false };
            var profile = new Department { Id = 4, IsActive = true, IsRequisitionTarget = true };
            Assert.AreEqual(1, RequisitionTargetRules.ResolveRoutingDepartment(locked, room, profile).Id);
            Assert.AreEqual(2, RequisitionTargetRules.ResolveRoutingDepartment(null, room, profile).Id);
            Assert.AreEqual(4, RequisitionTargetRules.ResolveRoutingDepartment(null, notTarget, profile).Id);
            Assert.IsNull(RequisitionTargetRules.ResolveRoutingDepartment(null, notTarget, null));
        }

        [Test]
        public void Describe_RoomPersonAndLegacy()
        {
            Assert.AreEqual("Admin", RequisitionTargetRules.Describe(null, "Admin", null, null, null), "legacy: department only");
            Assert.AreEqual("Room: Lab", RequisitionTargetRules.Describe("Room", "Lab", null, null, null));
            Assert.AreEqual("Person: Jane (recorded in Lab)", RequisitionTargetRules.Describe("Person", "Admin", "Jane", "PersonRoom", "Lab"));
            Assert.AreEqual("Person: Jane (company custody, held by Jane)", RequisitionTargetRules.Describe("Person", "Admin", "Jane", "CompanyCustody", null));
            Assert.IsNull(RequisitionTargetRules.Describe(null, null, null, null, null));
        }

        [Test]
        public void DecideTargetOnTag_NoTargetYet_PrefillsTheAssetsRoom()
        {
            Assert.AreEqual(TaggedAssetTargetDecision.PrefillRoom, RequisitionTargetRules.DecideTargetOnTag(null, null, null, 7, false));
            Assert.AreEqual(TaggedAssetTargetDecision.PrefillRoom, RequisitionTargetRules.DecideTargetOnTag("Person", null, "", 7, false), "person mode without a person picked");
            Assert.AreEqual(TaggedAssetTargetDecision.Keep, RequisitionTargetRules.DecideTargetOnTag(null, null, null, null, false), "asset without a department");
        }

        [Test]
        public void DecideTargetOnTag_ChosenTarget_IsKept_WithNoticeWhenTheAssetSitsElsewhere()
        {
            Assert.AreEqual(TaggedAssetTargetDecision.Keep, RequisitionTargetRules.DecideTargetOnTag("Room", 7, null, 7, false), "same room");
            Assert.AreEqual(TaggedAssetTargetDecision.KeepWithNotice, RequisitionTargetRules.DecideTargetOnTag("Room", 8, null, 7, false));
            Assert.AreEqual(TaggedAssetTargetDecision.KeepWithNotice, RequisitionTargetRules.DecideTargetOnTag("Person", null, "u-1", 7, false));
            Assert.AreEqual(TaggedAssetTargetDecision.KeepWithNotice, RequisitionTargetRules.DecideTargetOnTag(null, 3, null, 7, true), "locked department is never changed");
            Assert.AreEqual(
                "The tagged asset sits in Lab; the requisition target you chose was kept.",
                RequisitionTargetRules.TaggedAssetKeptNotice(" Lab "));
        }

        // ---- Submit: person / room persisted ----------------------------------------------------------------------

        private static FakeUnitOfWork Seed()
        {
            var uow = new FakeUnitOfWork();
            uow.Seed(new Department { Id = AdminId, OrganizationId = 1, Name = "Administration", Code = "ADM", DepartmentKind = DepartmentKind.Administrative, IsActive = true, IsRequisitionTarget = true });
            uow.Seed(new Department { Id = RoomId, OrganizationId = 1, Name = "Science lab", Code = "LAB", DepartmentKind = DepartmentKind.Room, RoomCustodianUserId = "teacher", IsActive = true, IsRequisitionTarget = true });
            uow.Seed(new Role { Id = 9, Name = "Bursar", IsActive = true });
            uow.Seed(new SystemSetting { Id = 1, SettingKey = "Approval.Process.Purchase.Enabled", SettingValue = "true", IsActive = true });
            uow.Seed(new SystemSetting { Id = 2, SettingKey = "Approval.Process.Purchase.StageRoleIds", SettingValue = "9", IsActive = true });
            uow.Seed(new AssetCategory { Id = 1, OrganizationId = 1, Name = "ICT", IsActive = true });
            uow.Seed(new AssetType { Id = 10, OrganizationId = 1, AssetCategoryId = 1, Name = "Laptops", IsActive = true });
            uow.Seed(new AssetSubType { Id = 100, OrganizationId = 1, AssetTypeId = 10, Name = "Latitude", IsActive = true });
            return uow;
        }

        private static FakeUserService Users()
        {
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "teacher", FirstName = "Jane", LastName = "Teacher", IsActive = true, DepartmentId = AdminId });
            users.Seed(new UserVm { Id = "clerk", FirstName = "Sam", LastName = "Clerk", IsActive = true, DepartmentId = AdminId });
            users.Seed(new UserVm { Id = "gone", FirstName = "Old", LastName = "User", IsActive = false, DepartmentId = AdminId });
            return users;
        }

        private static PurchaseRequestService Service(FakeUnitOfWork uow)
        {
            return new PurchaseRequestService(
                uow,
                new NoOpAuditWriter(),
                Users(),
                new NoOpDepartmentScopeService(),
                new FakeOrganizationScopeService(organizationId: 1),
                new NoOpOutboxWriter(),
                new NoOpWebhookService(),
                new FakeApprovalWorkflowEngine(uow, new NoOpAuditWriter()),
                new FakeOperationsQueryRepository());
        }

        private static PurchaseRequestCreateVm Form(string kind, int departmentId, string userId = null, string placement = null)
        {
            return new PurchaseRequestCreateVm
            {
                RequisitionTargetKind = kind,
                DepartmentId = departmentId,
                TargetUserId = userId,
                TargetPlacement = placement,
                Justification = "Term 3",
                Currency = "KES",
                Lines = new List<PurchaseRequestLineCreateVm>
                {
                    new PurchaseRequestLineCreateVm { Description = "Laptop", ItemName = "Laptop", Quantity = 1, AssetCategoryId = 1, AssetTypeId = 10, AssetSubTypeId = 100 }
                }
            };
        }

        [Test]
        public void Submit_PersonWithRoom_InTheirRoom_SavesPersonPlacementAndRoom_RoutesViaTheRoom()
        {
            var uow = Seed();
            var result = Service(uow).SubmitBatchCompleted(uow, Form("person", 0, "teacher", "PersonRoom"), "requester");
            var pr = uow.Repository<PurchaseRequest>().GetById(result.PrimaryId);
            Assert.AreEqual("Person", pr.RequisitionTargetKind);
            Assert.AreEqual("teacher", pr.TargetUserId);
            Assert.AreEqual("PersonRoom", pr.TargetPlacement);
            Assert.AreEqual(RoomId, pr.TargetDepartmentId);
            Assert.AreEqual(RoomId, pr.DepartmentId, "approval routes through the person's room");
        }

        [Test]
        public void Submit_PersonWithRoom_WithoutPlacement_IsRejected()
        {
            var uow = Seed();
            var ex = Assert.Throws<BusinessException>(() => Service(uow).SubmitBatchCompleted(uow, Form("Person", 0, "teacher"), "requester"));
            StringAssert.Contains("company custody", ex.Message);
            Assert.AreEqual(0, uow.Repository<PurchaseRequest>().GetAll().Count());
        }

        [Test]
        public void Submit_PersonWithoutRoom_IsCompanyCustody_WithoutAsking()
        {
            var uow = Seed();
            var result = Service(uow).SubmitBatchCompleted(uow, Form("Person", 0, "clerk", "PersonRoom"), "requester");
            var pr = uow.Repository<PurchaseRequest>().GetById(result.PrimaryId);
            Assert.AreEqual("Person", pr.RequisitionTargetKind);
            Assert.AreEqual("clerk", pr.TargetUserId);
            Assert.AreEqual("CompanyCustody", pr.TargetPlacement);
            Assert.IsNull(pr.TargetDepartmentId);
            Assert.AreEqual(AdminId, pr.DepartmentId, "approval routes through the person's profile department");
        }

        [Test]
        public void Submit_InactiveOrMissingPerson_IsRejected()
        {
            var uow = Seed();
            Assert.Throws<BusinessException>(() => Service(uow).SubmitBatchCompleted(uow, Form("Person", 0, "gone"), "requester"));
            var ex = Assert.Throws<BusinessException>(() => Service(uow).SubmitBatchCompleted(uow, Form("Person", 0, " "), "requester"));
            StringAssert.Contains("Choose the person", ex.Message);
        }

        [Test]
        public void Submit_Room_KeepsTheDepartment_AndClearsPersonFields()
        {
            var uow = Seed();
            var result = Service(uow).SubmitBatchCompleted(uow, Form("Room", RoomId, "teacher", "PersonRoom"), "requester");
            var pr = uow.Repository<PurchaseRequest>().GetById(result.PrimaryId);
            Assert.AreEqual("Room", pr.RequisitionTargetKind);
            Assert.AreEqual(RoomId, pr.DepartmentId);
            Assert.IsNull(pr.TargetUserId);
            Assert.IsNull(pr.TargetPlacement);
        }

        [Test]
        public void PersonTargetOptions_ActiveUsersOnly_WithRoomAndRouting()
        {
            var options = Service(Seed()).GetPersonTargetOptions();
            CollectionAssert.AreEquivalent(new[] { "teacher", "clerk" }, options.Select(x => x.UserId).ToArray());
            var teacher = options.Single(x => x.UserId == "teacher");
            Assert.AreEqual(RoomId, teacher.RoomId);
            Assert.AreEqual(RoomId, teacher.RoutingDepartmentId);
            var clerk = options.Single(x => x.UserId == "clerk");
            Assert.IsNull(clerk.RoomId);
            Assert.AreEqual(AdminId, clerk.RoutingDepartmentId);
        }

        // ---- Inline create gate (Allan 2026-10-28 15:03 EAT: screen access, not category management) --------------

        [Test]
        public void Policy_ScreenPermissionPerOrigin()
        {
            Assert.AreEqual("Purchases.Create", ClassificationInlineCreatePolicy.PermissionFor(ClassificationCreateOrigin.Requisition));
            Assert.AreEqual("Assets.Receive", ClassificationInlineCreatePolicy.PermissionFor(ClassificationCreateOrigin.Receiving));
            Assert.IsNull(ClassificationInlineCreatePolicy.PermissionFor("Settings"));
        }

        [Test]
        public void Policy_HodWithRequisitionCreate_CanCreateOnTheRequisitionScreen_NotOnReceiving()
        {
            Func<string, bool> hod = p => p == "Purchases.Create" || p == "Purchases.View";
            Assert.IsTrue(ClassificationInlineCreatePolicy.CanCreate(ClassificationCreateOrigin.Requisition, hod, false));
            Assert.IsFalse(ClassificationInlineCreatePolicy.CanCreate(ClassificationCreateOrigin.Receiving, hod, false));
        }

        [Test]
        public void Policy_CategoryManagementAloneIsNotEnough_AndUnknownOriginIsRefused()
        {
            Func<string, bool> manager = p => p == "Settings.Manage" || p == "Assets.Edit";
            Assert.IsFalse(ClassificationInlineCreatePolicy.CanCreate(ClassificationCreateOrigin.Requisition, manager, false));
            Assert.IsFalse(ClassificationInlineCreatePolicy.CanCreate(ClassificationCreateOrigin.Receiving, manager, false));
            Assert.IsTrue(ClassificationInlineCreatePolicy.CanCreate(ClassificationCreateOrigin.Receiving, p => p == "Assets.Receive", false));
            Assert.IsFalse(ClassificationInlineCreatePolicy.CanCreate("Elsewhere", p => true, true));
            Assert.IsTrue(ClassificationInlineCreatePolicy.CanCreate(ClassificationCreateOrigin.Requisition, p => false, true), "super admin");
        }

        private static string PermissionOf(MethodInfo method)
        {
            var data = method.GetCustomAttributesData().SingleOrDefault(x => x.Constructor.DeclaringType == typeof(PermissionAuthorizeAttribute));
            return data == null ? null : (string)data.ConstructorArguments[0].Value;
        }

        [TestCase(typeof(PurchaseRequestsController), "Purchases.Create")]
        [TestCase(typeof(PurchasesController), "Assets.Receive")]
        public void CreateClassification_IsAPostWithAntiForgery_GatedByTheScreenPermission_AndTakesNoOrganisation(Type controller, string permission)
        {
            var method = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public).Single(x => x.Name == "CreateClassification");
            Assert.AreEqual(permission, PermissionOf(method));
            Assert.IsTrue(method.IsDefined(typeof(HttpPostAttribute), false));
            Assert.IsTrue(method.IsDefined(typeof(ValidateAntiForgeryTokenAttribute), false));
            Assert.IsFalse(method.GetParameters().Any(p => p.Name.IndexOf("organization", StringComparison.OrdinalIgnoreCase) >= 0
                || p.Name.IndexOf("org", StringComparison.OrdinalIgnoreCase) == 0), "the organisation is never posted");
        }

        [Test]
        public void InlineCreateSources_DoNotUseCategoryManagementPermissions()
        {
            var policy = QaSixRequisitionFormTests.Read("src", "AssetManagement.Application", "Helpers", "ClassificationInlineCreatePolicy.cs");
            StringAssert.DoesNotContain("HasPermission(\"Settings.Manage\")", policy);
            var catalog = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Controllers", "PurchaseRequestsController.Catalog.cs");
            StringAssert.Contains("[PermissionAuthorize(ClassificationInlineCreatePolicy.RequisitionScreenPermission)]", catalog);
            var purchases = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Controllers", "PurchasesController.cs");
            StringAssert.Contains("[PermissionAuthorize(Application.Helpers.ClassificationInlineCreatePolicy.ReceivingScreenPermission)]", purchases);
            var baseController = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Controllers", "BaseController.cs");
            StringAssert.Contains("CreateChain(", baseController);
            StringAssert.Contains("User.GetUserId(), origin)", baseController);
        }

        // ---- Views / scripts --------------------------------------------------------------------------------------

        private static string CreateView()
        {
            return QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Views", "PurchaseRequests", "Create.cshtml");
        }

        private static string Script(string name)
        {
            return QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Scripts", "app", name);
        }

        [Test]
        public void CreateView_AsksRoomOrPersonFirst_WithPersonPanelAndPlacement()
        {
            var view = CreateView();
            StringAssert.Contains("Requisition target (room or person)", view);
            StringAssert.Contains("name=\"RequisitionTargetKind\" id=\"target-kind-room\" value=\"Room\"", view);
            StringAssert.Contains("name=\"RequisitionTargetKind\" id=\"target-kind-person\" value=\"Person\"", view);
            StringAssert.Contains("id=\"TargetUserId\" name=\"TargetUserId\"", view);
            StringAssert.Contains("data-room-id=", view);
            StringAssert.Contains("data-routing-department-id=", view);
            StringAssert.Contains("value=\"PersonRoom\"", view);
            StringAssert.Contains("id=\"target-placement-no-room\" name=\"TargetPlacement\" value=\"CompanyCustody\"", view);
            StringAssert.Contains("id=\"target-tag-notice\"", view);
            Assert.Less(view.IndexOf("target-kind-room", StringComparison.Ordinal), view.IndexOf("GroupedDepartmentDropDown(\"DepartmentId\"", StringComparison.Ordinal), "room or person is asked first");
        }

        [Test]
        public void CreateView_TagSitsAboveTheItemName_AndLinesHaveACategoryPicker()
        {
            var view = CreateView();
            var row = view.Substring(view.IndexOf("class=\"purchase-line-item row", StringComparison.Ordinal));
            Assert.Less(row.IndexOf("am-line-target", StringComparison.Ordinal), row.IndexOf("Item / asset name", StringComparison.Ordinal));
            Assert.AreEqual(2, Regex.Matches(view, "am-pick-category[^>]*data-field=\"AssetCategoryId\"").Count, "line rows + template (the header picker is separate)");
            var js = Script("purchase-request-create.js");
            var add = js.Substring(js.IndexOf("function addLineItem", StringComparison.Ordinal));
            Assert.Less(add.IndexOf("am-line-target", StringComparison.Ordinal), add.IndexOf("Item / asset name", StringComparison.Ordinal));
        }

        [Test]
        public void CreateScript_TaggingRule_MirrorsTheServer_AndIsIe11Safe()
        {
            var js = Script("purchase-request-create.js");
            QaSixRequisitionFormTests.AssertIe11Safe(js);
            StringAssert.Contains("function decideTargetOnTag(kind, departmentId, userId, assetDepartmentId, locked)", js);
            StringAssert.Contains("\"; the requisition target you chose was kept.\"", js);
            StringAssert.Contains("if (decision === \"PrefillRoom\")", js);
            StringAssert.DoesNotContain(".toggle(", js);
        }

        [Test]
        public void InlineCreateScripts_AreIe11Safe_AndOfferAddNew()
        {
            QaSixRequisitionFormTests.AssertIe11Safe(Script("classification-inline-create.js"));
            var classification = Script("purchase-request-classification.js");
            QaSixRequisitionFormTests.AssertIe11Safe(classification);
            StringAssert.Contains("+ Add new category...", classification);
            StringAssert.Contains("+ Add new sub-type...", classification);
            var receive = Script("receive-order.js");
            QaSixRequisitionFormTests.AssertIe11Safe(receive);
            StringAssert.Contains("+ Add new type...", receive);
            StringAssert.Contains("if (isAddOption(opt))", receive);
            StringAssert.DoesNotContain(".toggle(", receive);
        }

        [Test]
        public void Views_ShowTheTargetSummary_AndLoadTheInlineScriptOnlyWhenAllowed()
        {
            StringAssert.Contains("Model.TargetSummary", QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Views", "PurchaseRequests", "Details.cshtml"));
            var receive = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Views", "Purchases", "Receive.cshtml");
            StringAssert.Contains("Model.TargetSummary", receive);
            StringAssert.Contains("ViewBag.CanCreateClassification == true", receive);
            Assert.Less(receive.IndexOf("classification-inline-create.js", StringComparison.Ordinal), receive.IndexOf("receive-order.js", StringComparison.Ordinal));
            var create = CreateView();
            Assert.Less(create.IndexOf("classification-inline-create.js", StringComparison.Ordinal), create.IndexOf("purchase-request-classification.js", StringComparison.Ordinal));
        }

        // ---- Migration 110 ----------------------------------------------------------------------------------------

        private static readonly string[][] Columns110 =
        {
            new[] { "PurchaseRequest", "RequisitionTargetKind" }, new[] { "PurchaseRequest", "TargetUserId" },
            new[] { "PurchaseRequest", "TargetPlacement" }, new[] { "PurchaseRequest", "TargetDepartmentId" },
            new[] { "AssetCategory", "CreatedById" }, new[] { "AssetCategory", "CreatedVia" },
            new[] { "AssetType", "CreatedById" }, new[] { "AssetType", "CreatedVia" },
            new[] { "AssetSubType", "CreatedById" }, new[] { "AssetSubType", "CreatedVia" }
        };

        [Test]
        public void Migration110_IsIdempotent_NullableOnly_AndCoversAllTenColumns()
        {
            var sql = QaSixRequisitionFormTests.Read("database", "scripts", "004_Migrations", "110_requisition_target_room_or_person.sql");
            StringAssert.Contains("N'110_requisition_target_room_or_person.sql'", sql);
            foreach (var c in Columns110)
            {
                StringAssert.Contains("IF COL_LENGTH(N'dbo." + c[0] + "', N'" + c[1] + "') IS NULL", sql);
                Assert.IsTrue(Regex.IsMatch(sql, @"ADD \[" + c[1] + @"\] [A-Z]+(\(\d+\))? NULL;"), c[0] + "." + c[1] + " nullable");
            }

            Assert.IsFalse(Regex.IsMatch(sql, @"\bNOT NULL\b"), "nullable-only");
            Assert.IsFalse(Regex.IsMatch(sql, @"\bDROP\s+(TABLE|COLUMN)\b", RegexOptions.IgnoreCase));
            var live = QaSixRequisitionFormTests.Read("database", "scripts", "ops", "LiveDB_Update_20261028_110", "02_MIGRATION_110_RequisitionTargetRoomOrPerson_LIVE_RUN_BY_ALLAN.sql");
            foreach (var c in Columns110)
            {
                StringAssert.Contains("IF COL_LENGTH(N'dbo." + c[0] + "', N'" + c[1] + "') IS NULL", live, "the live kit carries the same columns");
            }
        }

        [Test]
        public void Rollback110_DropsEveryColumn_AndTheHistoryRow()
        {
            var rollback = QaSixRequisitionFormTests.Read("database", "scripts", "ops", "110_Rollback_RequisitionTargetRoomOrPerson_PREPARE_DO_NOT_RUN.sql");
            foreach (var c in Columns110)
            {
                StringAssert.Contains(c[1], rollback);
            }

            StringAssert.Contains("110_requisition_target_room_or_person.sql", rollback);
        }

        // ---- Room list grouped by department / sub-department (QA A3 on 65d06e1) ------------------------------------

        private static List<DepartmentVm> RoomTree()
        {
            return new List<DepartmentVm>
            {
                new DepartmentVm { Id = 1, Name = "Information Technology", DepartmentKind = DepartmentKind.Administrative, IsActive = true, IsRequisitionTarget = true },
                new DepartmentVm { Id = 2, Name = "ICT", ParentDepartmentId = 1, DepartmentKind = DepartmentKind.Room, IsActive = true, IsRequisitionTarget = true },
                new DepartmentVm { Id = 3, Name = "Administration", DepartmentKind = DepartmentKind.Administrative, IsActive = true, IsRequisitionTarget = false },
                new DepartmentVm { Id = 4, Name = "Facilities", ParentDepartmentId = 3, DepartmentKind = DepartmentKind.SubDepartment, IsActive = true, IsRequisitionTarget = true },
                new DepartmentVm { Id = 5, Name = "Store 10", ParentDepartmentId = 4, DepartmentKind = DepartmentKind.Room, IsActive = true, IsRequisitionTarget = true },
                new DepartmentVm { Id = 6, Name = "store 2", ParentDepartmentId = 4, DepartmentKind = DepartmentKind.Room, IsActive = true, IsRequisitionTarget = true },
                new DepartmentVm { Id = 7, Name = "Boardroom", ParentDepartmentId = 3, DepartmentKind = DepartmentKind.Room, IsActive = true, IsRequisitionTarget = true },
                new DepartmentVm { Id = 8, Name = "Art room", DepartmentKind = DepartmentKind.Room, IsActive = true, IsRequisitionTarget = true },
                new DepartmentVm { Id = 9, Name = "Old lab", ParentDepartmentId = 1, DepartmentKind = DepartmentKind.Room, IsActive = false, IsRequisitionTarget = true },
                new DepartmentVm { Id = 10, Name = "Grade 2", DepartmentKind = DepartmentKind.Grade, IsActive = true, IsRequisitionTarget = false },
                new DepartmentVm { Id = 11, Name = "Grade 2 West", ParentDepartmentId = 10, DepartmentKind = DepartmentKind.Class, IsActive = true, IsRequisitionTarget = true },
                new DepartmentVm { Id = 12, Name = "Grade 2 East", ParentDepartmentId = 10, DepartmentKind = DepartmentKind.Class, IsActive = true, IsRequisitionTarget = true },
                new DepartmentVm { Id = 13, Name = "Not a target room", ParentDepartmentId = 1, DepartmentKind = DepartmentKind.Room, IsActive = true, IsRequisitionTarget = false },
                new DepartmentVm { Id = 14, Name = "Library", DepartmentKind = DepartmentKind.Room, IsActive = true, IsRequisitionTarget = true }
            };
        }

        private static string Describe(IList<RequisitionRoomGroup> groups)
        {
            return string.Join(" | ", groups.Select(g => g.Label + ": " + string.Join(", ", g.Options.Select(o => o.Name).ToArray())).ToArray());
        }

        [Test]
        public void GroupRoomTargets_GroupsByDepartmentAndSubDepartment_Sorted_NoDepartmentLast()
        {
            var groups = RequisitionTargetRules.GroupRoomTargets(RoomTree(), true);
            Assert.AreEqual(
                "Administration: Boardroom"
                + " | Administration > Facilities: Facilities, store 2, Store 10"
                + " | Grade 2: Grade 2 East, Grade 2 West"
                + " | Information Technology: Information Technology, ICT"
                + " | No department: Art room, Library",
                Describe(groups));
            Assert.IsTrue(groups.Last().IsNoDepartment);
            Assert.AreEqual(RequisitionTargetRules.NoDepartmentGroupLabel, groups.Last().Label);
            Assert.IsTrue(groups[1].Options[0].IsGroupDepartment, "the sub-department itself heads its own group");
            Assert.AreEqual(4, groups[1].DepartmentId);
        }

        [Test]
        public void GroupRoomTargets_SkipsInactive_AndNonTargetsWhenTargetsOnly()
        {
            var ids = RequisitionTargetRules.GroupRoomTargets(RoomTree(), true).SelectMany(g => g.Options).Select(o => o.DepartmentId).ToList();
            CollectionAssert.DoesNotContain(ids, 9, "inactive room");
            CollectionAssert.DoesNotContain(ids, 13, "not a requisition target");
            CollectionAssert.DoesNotContain(ids, 3, "non-target department is only a group label");
            Assert.AreEqual(ids.Count, ids.Distinct().Count(), "each room once");
        }

        [Test]
        public void GroupRoomTargets_AllDepartments_KeepsTheSameSet_Grouped()
        {
            var tree = RoomTree();
            var groups = RequisitionTargetRules.GroupRoomTargets(tree, false);
            var ids = groups.SelectMany(g => g.Options).Select(o => o.DepartmentId).OrderBy(x => x).ToList();
            CollectionAssert.AreEqual(tree.Where(x => x.IsActive).Select(x => x.Id).OrderBy(x => x).ToList(), ids, "the old flat list's set, nothing lost");
            Assert.AreEqual("Administration", groups[0].Label);
            Assert.AreEqual("Administration", groups[0].Options[0].Name, "department first in its own group");
            Assert.AreEqual("Boardroom", groups[0].Options[1].Name);
        }

        [Test]
        public void GroupRoomTargets_DeepPath_UsesFullDepartmentPath_AndIsCycleSafe()
        {
            var tree = new List<DepartmentVm>
            {
                new DepartmentVm { Id = 1, Name = "Ops", DepartmentKind = DepartmentKind.Administrative, IsActive = true },
                new DepartmentVm { Id = 2, Name = "Kitchen", ParentDepartmentId = 1, DepartmentKind = DepartmentKind.SubDepartment, IsActive = true },
                new DepartmentVm { Id = 3, Name = "Stores", ParentDepartmentId = 2, DepartmentKind = DepartmentKind.SubDepartment, IsActive = true },
                new DepartmentVm { Id = 4, Name = "Cold room", ParentDepartmentId = 3, DepartmentKind = DepartmentKind.Room, IsActive = true, IsRequisitionTarget = true },
                new DepartmentVm { Id = 5, Name = "Loop A", ParentDepartmentId = 6, DepartmentKind = DepartmentKind.SubDepartment, IsActive = true },
                new DepartmentVm { Id = 6, Name = "Loop B", ParentDepartmentId = 5, DepartmentKind = DepartmentKind.SubDepartment, IsActive = true },
                new DepartmentVm { Id = 7, Name = "Loop room", ParentDepartmentId = 6, DepartmentKind = DepartmentKind.Room, IsActive = true, IsRequisitionTarget = true },
                new DepartmentVm { Id = 8, Name = "Self parent", ParentDepartmentId = 8, DepartmentKind = DepartmentKind.Room, IsActive = true, IsRequisitionTarget = true }
            };
            var groups = RequisitionTargetRules.GroupRoomTargets(tree, true);
            Assert.AreEqual("Loop A > Loop B: Loop room | Ops > Kitchen > Stores: Cold room | No department: Self parent", Describe(groups));
            Assert.AreEqual(0, RequisitionTargetRules.GroupRoomTargets(null, true).Count);
        }

        [Test]
        public void NaturalCompare_NumbersAsNumbers_CaseInsensitive()
        {
            Assert.Less(RequisitionTargetRules.NaturalCompare("Year 2", "Year 10"), 0);
            Assert.Greater(RequisitionTargetRules.NaturalCompare("Room 10", "room 9"), 0);
            Assert.Less(RequisitionTargetRules.NaturalCompare("art", "Biology"), 0);
            Assert.Less(RequisitionTargetRules.NaturalCompare("Lab", "Lab 1"), 0);
            Assert.AreEqual(0, RequisitionTargetRules.NaturalCompare("Lab 02", "Lab 02"));
        }

        [Test]
        public void CreateLookups_RoomListIsGroupedForEveryUnlockedRequester()
        {
            var controller = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Controllers", "PurchaseRequestsController.cs");
            StringAssert.Contains("BuildRequisitionDepartmentGroups(departmentId, GetActiveDepartments(), false)", controller, "no flat fallback for users without CreateForAnyDepartment");
            var view = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Views", "PurchaseRequests", "Create.cshtml");
            StringAssert.Contains("@Html.GroupedDepartmentDropDown(\"DepartmentId\", departmentGroups", view);
            StringAssert.Contains("grouped by department and sub-department", view);
        }

        [Test]
        public void Entities_CarryTheMigration110Columns()
        {
            Assert.IsNotNull(typeof(AssetCategory).GetProperty("CreatedById"));
            Assert.IsNotNull(typeof(AssetType).GetProperty("CreatedVia"));
            Assert.IsNotNull(typeof(AssetSubType).GetProperty("CreatedById"));
            Assert.IsNotNull(typeof(PurchaseRequest).GetProperty("TargetPlacement"));
        }
    }
}

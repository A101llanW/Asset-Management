using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Requisitions
{
    /// <summary>
    /// Optional per-stage "Approver (person)" and "Purchase order raised by" (migration 088):
    /// validation, role-change clear, auto-select rule, inheritance resolution and enforcement.
    /// </summary>
    [TestFixture]
    public class StageApproverAndPurchaseOrderRaiserTests
    {
        private const int Org = 13;
        private const int OtherOrg = 21;
        private const int ProcOfficerRole = 4;
        private const int ProcManagerRole = 6;
        private const int FinanceRole = 7;

        private static Dictionary<string, ApproverUserRecord> Users()
        {
            return new[]
            {
                new ApproverUserRecord { Id = "po-1", OrganizationId = Org, IsActive = true, RoleId = ProcOfficerRole },
                new ApproverUserRecord { Id = "pm-1", OrganizationId = Org, IsActive = true, RoleId = ProcManagerRole },
                new ApproverUserRecord { Id = "pm-2", OrganizationId = Org, IsActive = true, RoleId = ProcManagerRole },
                new ApproverUserRecord { Id = "pm-off", OrganizationId = Org, IsActive = false, RoleId = ProcManagerRole },
                new ApproverUserRecord { Id = "nis-pm", OrganizationId = OtherOrg, IsActive = true, RoleId = ProcManagerRole }
            }.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        }

        private static Func<string, ApproverUserRecord> Finder()
        {
            var users = Users();
            return id =>
            {
                ApproverUserRecord found;
                return id != null && users.TryGetValue(id, out found) ? found : null;
            };
        }

        private static readonly HashSet<int> OrgRoles = new HashSet<int> { ProcOfficerRole, ProcManagerRole, FinanceRole };

        private static List<string> Validate(IList<ApprovalStageSettingsVm> stages, IList<int> storedRoles)
        {
            var errors = new List<string>();
            StageApproverRules.ValidateStages(stages, storedRoles, Org, OrgRoles, Finder(), (k, m) => errors.Add(m), "Stages");
            return errors;
        }

        // ---------- positional storage ----------

        [Test]
        public void StageUserIds_ArePositional_EmptySlotKept_TrailingTrimmed()
        {
            var serialized = ApprovalWorkflowSettingsHelper.SerializeStageUserIds(new[] { null, "pm-1", "" });
            Assert.AreEqual(",pm-1", serialized);
            var parsed = ApprovalWorkflowSettingsHelper.ParseStageUserIds(serialized);
            Assert.AreEqual(2, parsed.Count);
            Assert.IsNull(ApprovalWorkflowHelper.GetStageUserId(parsed, 1));
            Assert.AreEqual("pm-1", ApprovalWorkflowHelper.GetStageUserId(parsed, 2));
            Assert.IsNull(ApprovalWorkflowHelper.GetStageUserId(parsed, 3));
            Assert.AreEqual(string.Empty, ApprovalWorkflowSettingsHelper.SerializeStageUserIds(new string[] { null, " " }));
            Assert.AreEqual(0, ApprovalWorkflowSettingsHelper.ParseStageUserIds("").Count);
            Assert.AreEqual(0, ApprovalWorkflowSettingsHelper.ParseStageUserIds(null).Count);
            // Legacy compact values with every slot set read exactly as before.
            CollectionAssert.AreEqual(new[] { "a", "b" }, ApprovalWorkflowSettingsHelper.ParseStageUserIds("a,b"));
        }

        // ---------- validation ----------

        [Test]
        public void Validate_ValidPersonInRole_IsKept()
        {
            var stages = new List<ApprovalStageSettingsVm> { new ApprovalStageSettingsVm { RoleId = ProcManagerRole, UserId = " pm-1 " } };
            var errors = Validate(stages, new List<int> { ProcManagerRole });
            Assert.IsEmpty(errors);
            Assert.AreEqual("pm-1", stages[0].UserId);
        }

        [Test]
        public void Validate_WrongRole_OnUnchangedRole_IsError()
        {
            var stages = new List<ApprovalStageSettingsVm> { new ApprovalStageSettingsVm { RoleId = ProcManagerRole, UserId = "po-1" } };
            var errors = Validate(stages, new List<int> { ProcManagerRole });
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("does not hold the selected role", errors[0]);
            Assert.IsNull(stages[0].UserId);
        }

        [Test]
        public void Validate_OtherOrgUser_IsError()
        {
            var stages = new List<ApprovalStageSettingsVm> { new ApprovalStageSettingsVm { RoleId = ProcManagerRole, UserId = "nis-pm" } };
            var errors = Validate(stages, new List<int> { ProcManagerRole });
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("not a user in this organisation", errors[0]);
            Assert.IsNull(stages[0].UserId);
        }

        [Test]
        public void Validate_UnknownUser_IsError()
        {
            var stages = new List<ApprovalStageSettingsVm> { new ApprovalStageSettingsVm { RoleId = ProcManagerRole, UserId = "ghost" } };
            Assert.AreEqual(1, Validate(stages, new List<int> { ProcManagerRole }).Count);
        }

        [Test]
        public void Validate_InactiveUser_IsError()
        {
            var stages = new List<ApprovalStageSettingsVm> { new ApprovalStageSettingsVm { RoleId = ProcManagerRole, UserId = "pm-off" } };
            var errors = Validate(stages, new List<int> { ProcManagerRole });
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("inactive", errors[0]);
        }

        [Test]
        public void Validate_RoleNotInOrg_IsError()
        {
            var stages = new List<ApprovalStageSettingsVm> { new ApprovalStageSettingsVm { RoleId = 999, UserId = "pm-1" } };
            var errors = Validate(stages, new List<int>());
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("role does not exist", errors[0]);
        }

        [Test]
        public void Validate_RoleChanged_ClearsMismatchedPersonSilently()
        {
            // Stage 2 role changed Procurement Manager -> Finance; the old PM person no longer matches.
            var stages = new List<ApprovalStageSettingsVm>
            {
                new ApprovalStageSettingsVm { RoleId = ProcOfficerRole, UserId = "po-1" },
                new ApprovalStageSettingsVm { RoleId = FinanceRole, UserId = "pm-1" }
            };
            var errors = Validate(stages, new List<int> { ProcOfficerRole, ProcManagerRole });
            Assert.IsEmpty(errors);
            Assert.AreEqual("po-1", stages[0].UserId);
            Assert.IsNull(stages[1].UserId);
        }

        [Test]
        public void Validate_NoRole_ClearsPerson()
        {
            var stages = new List<ApprovalStageSettingsVm> { new ApprovalStageSettingsVm { RoleId = null, UserId = "pm-1" } };
            Validate(stages, new List<int>());
            Assert.IsNull(stages[0].UserId);
        }

        [Test]
        public void ValidatePoRaiser_Rules()
        {
            var errors = new List<string>();
            Action<string, string> add = (k, m) => errors.Add(m);

            Assert.AreEqual("pm-2", StageApproverRules.ValidatePurchaseOrderRaiser(ProcManagerRole, "pm-2", ProcManagerRole, Org, OrgRoles, Finder(), add));
            Assert.IsNull(StageApproverRules.ValidatePurchaseOrderRaiser(ProcManagerRole, null, null, Org, OrgRoles, Finder(), add));
            Assert.IsEmpty(errors);

            Assert.IsNull(StageApproverRules.ValidatePurchaseOrderRaiser(999, null, null, Org, OrgRoles, Finder(), add));
            Assert.AreEqual(1, errors.Count, "role must exist in the org");

            Assert.IsNull(StageApproverRules.ValidatePurchaseOrderRaiser(ProcManagerRole, "nis-pm", ProcManagerRole, Org, OrgRoles, Finder(), add));
            Assert.AreEqual(2, errors.Count, "other-org person rejected");

            Assert.IsNull(StageApproverRules.ValidatePurchaseOrderRaiser(ProcManagerRole, "pm-off", ProcManagerRole, Org, OrgRoles, Finder(), add));
            Assert.AreEqual(3, errors.Count, "inactive person rejected");

            Assert.IsNull(StageApproverRules.ValidatePurchaseOrderRaiser(ProcManagerRole, "po-1", ProcManagerRole, Org, OrgRoles, Finder(), add));
            Assert.AreEqual(4, errors.Count, "wrong role on unchanged role rejected");

            Assert.IsNull(StageApproverRules.ValidatePurchaseOrderRaiser(FinanceRole, "pm-1", ProcManagerRole, Org, OrgRoles, Finder(), add));
            Assert.AreEqual(4, errors.Count, "role changed: mismatched person cleared, no error");

            Assert.IsNull(StageApproverRules.ValidatePurchaseOrderRaiser(null, "pm-1", ProcManagerRole, Org, OrgRoles, Finder(), add));
            Assert.AreEqual(4, errors.Count, "no role: person cleared");
        }

        [Test]
        public void AutoSelect_Never_EvenWithExactlyOneHolder_DefaultIsAnyUserInRole()
        {
            // rel1009f: role-based stages default to a blank named approver (no auto-select of the sole holder).
            var users = Users().Values.Where(x => x.OrganizationId == Org).ToList();
            Assert.IsNull(StageApproverRules.ResolveAutoSelectUserId(users, ProcOfficerRole), "po-1 is the only holder: still not auto-selected");
            Assert.IsNull(StageApproverRules.ResolveAutoSelectUserId(users, ProcManagerRole), "pm-1 + pm-2 active (pm-off ignored)");
            Assert.IsNull(StageApproverRules.ResolveAutoSelectUserId(users, FinanceRole));
            Assert.IsNull(StageApproverRules.ResolveAutoSelectUserId(users, null));
        }

        [Test]
        public void ValidateStages_BlankPersonStaysBlank_ExplicitPickSaved_ServerNeverFillsIn()
        {
            var errors = new List<string>();
            Action<string, string> add = (k, m) => errors.Add(k + ": " + m);
            var stages = new List<ApprovalStageSettingsVm>
            {
                new ApprovalStageSettingsVm { RoleId = ProcOfficerRole, UserId = null },   // sole holder po-1, left blank
                new ApprovalStageSettingsVm { RoleId = ProcOfficerRole, UserId = "po-1" }  // explicit pick
            };
            StageApproverRules.ValidateStages(stages, new List<int> { ProcOfficerRole, ProcOfficerRole }, Org, OrgRoles, Finder(), add, "Stages");
            Assert.IsNull(stages[0].UserId, "blank stays blank: server does not fill in the sole holder");
            Assert.AreEqual("po-1", stages[1].UserId, "an explicit pick still saves");
            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void Snapshot_DropsPersonNoLongerActiveOrInRole()
        {
            var result = StageApproverRules.SanitizeForSnapshot(
                new List<int> { ProcOfficerRole, ProcManagerRole, ProcManagerRole },
                new List<string> { "po-1", "pm-off", "po-1" },
                Finder());
            CollectionAssert.AreEqual(new[] { "po-1", null, null }, result);
        }

        // ---------- stage enforcement ----------

        [Test]
        public void StageEnforcement_PersonSet_OnlyThatPersonCanAct()
        {
            Assert.IsTrue(ApprovalWorkflowHelper.CanUserActOnStage("req", "pm-1", false, ProcManagerRole, ProcManagerRole, "pm-1"));
            Assert.IsFalse(ApprovalWorkflowHelper.CanUserActOnStage("req", "pm-2", false, ProcManagerRole, ProcManagerRole, "pm-1"));
            Assert.DoesNotThrow(() => ApprovalWorkflowHelper.EnsureUserCanApprove(ProcManagerRole, false, ProcManagerRole, "pm-1", "req", "pm-1", ApprovalProcessCodes.Purchase));
            Assert.Throws<BusinessException>(() => ApprovalWorkflowHelper.EnsureUserCanApprove(ProcManagerRole, false, ProcManagerRole, "pm-1", "req", "pm-2", ApprovalProcessCodes.Purchase));
        }

        [Test]
        public void StageEnforcement_NoPerson_AnyRoleHolderCanAct()
        {
            Assert.IsTrue(ApprovalWorkflowHelper.CanUserActOnStage("req", "pm-1", false, ProcManagerRole, ProcManagerRole, null));
            Assert.IsTrue(ApprovalWorkflowHelper.CanUserActOnStage("req", "pm-2", false, ProcManagerRole, ProcManagerRole, null));
            Assert.IsFalse(ApprovalWorkflowHelper.CanUserActOnStage("req", "po-1", false, ProcOfficerRole, ProcManagerRole, null));
            Assert.DoesNotThrow(() => ApprovalWorkflowHelper.EnsureUserCanApprove(ProcManagerRole, false, ProcManagerRole, null, "req", "pm-2", ApprovalProcessCodes.Purchase));
        }

        [Test]
        public void Requisition_StagePerson_EndToEnd_SubmitSnapshotAndApprove()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Role { Id = ProcOfficerRole, Name = "Procurement Officer", IsActive = true });
            unitOfWork.Seed(new Role { Id = ProcManagerRole, Name = "Procurement Manager", IsActive = true });
            unitOfWork.Seed(new Department
            {
                Id = 50, Name = "Biology Lab", Code = "BIO", IsActive = true, IsRequisitionTarget = true,
                DepartmentKind = DepartmentKind.Room, RequisitionFlowMode = RequisitionFlowMode.Custom,
                CustomStageRoleIds = ProcOfficerRole + "," + ProcManagerRole,
                CustomStageUserIds = ",pm-2" // Stage 1 any Procurement Officer, Stage 2 only pm-2.
            });
            var users = SeedUsers();
            var outbox = new CapturingOutbox();
            var service = BuildRequestService(unitOfWork, users, outbox);

            var id = service.SubmitCompleted(unitOfWork, new PurchaseRequestCreateVm
            {
                DepartmentId = 50, ItemDescription = "Microscope", Justification = "Lab", Quantity = 1, Currency = "KES"
            }, "requester");
            var request = unitOfWork.Repository<PurchaseRequest>().GetById(id);
            Assert.AreEqual(",pm-2", request.ApprovalStageUserIds);

            // Stage 1: no person -> any Procurement Officer approves.
            service.Approve(new PurchaseRequestApprovalVm { PurchaseRequestId = id }, "po-1", ProcOfficerRole, false);
            Assert.AreEqual(2, request.CurrentApprovalStage);
            Assert.IsTrue(outbox.Sent.Any(x => x.Contains("pm-2") && x.Contains("Stage 2")), "only the chosen person is notified");
            Assert.IsFalse(outbox.Sent.Any(x => x.Contains("\"pm-1\"") && x.Contains("Stage 2")), "other role holder not notified");

            // Stage 2: another Procurement Manager is rejected server-side; the chosen person can approve.
            Assert.Throws<BusinessException>(() => service.Approve(new PurchaseRequestApprovalVm { PurchaseRequestId = id }, "pm-1", ProcManagerRole, false));
            service.Approve(new PurchaseRequestApprovalVm { PurchaseRequestId = id }, "pm-2", ProcManagerRole, false);
            Assert.AreEqual(ApprovalStatus.Approved, request.ApprovalStatus);
        }

        [Test]
        public void Requisition_ExistingPendingWithoutPerson_IgnoresLaterConfiguredPerson()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Role { Id = ProcManagerRole, Name = "Procurement Manager", IsActive = true });
            unitOfWork.Seed(new Department
            {
                Id = 51, Name = "Store", Code = "ST", IsActive = true, IsRequisitionTarget = true,
                RequisitionFlowMode = RequisitionFlowMode.Custom, CustomStageRoleIds = ProcManagerRole.ToString(), CustomStageUserIds = "pm-1"
            });
            unitOfWork.Seed(new PurchaseRequest
            {
                Id = 900, DepartmentId = 51, RequestedById = "requester", ApprovalStatus = ApprovalStatus.Pending,
                CurrentApprovalStage = 1, ApprovalStageRoleIds = ProcManagerRole.ToString(), ApprovalStageUserIds = null,
                RequestNumber = "PR-000900", IsActive = true
            });
            var service = BuildRequestService(unitOfWork, SeedUsers(), new CapturingOutbox());
            // Submitted with "any role holder": pm-2 may still approve although pm-1 is configured now.
            Assert.DoesNotThrow(() => service.Approve(new PurchaseRequestApprovalVm { PurchaseRequestId = 900 }, "pm-2", ProcManagerRole, false));
        }

        // ---------- PO raiser resolution ----------

        private static FakeUnitOfWork SeedHierarchy(bool configureSub, bool configureDept)
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Role { Id = ProcOfficerRole, Name = "Procurement Officer", IsActive = true });
            unitOfWork.Seed(new Role { Id = ProcManagerRole, Name = "Procurement Manager", IsActive = true });
            unitOfWork.Seed(new Department
            {
                Id = 1, Name = "Academics", Code = "ACA", IsActive = true, DepartmentKind = DepartmentKind.Administrative,
                RequisitionFlowMode = configureDept ? RequisitionFlowMode.Custom : RequisitionFlowMode.InheritParent,
                CustomStageRoleIds = configureDept ? ProcOfficerRole.ToString() : null,
                PurchaseOrderRaiserRoleId = configureDept ? ProcOfficerRole : (int?)null
            });
            unitOfWork.Seed(new Department
            {
                Id = 2, Name = "Science", Code = "SCI", IsActive = true, ParentDepartmentId = 1, DepartmentKind = DepartmentKind.SubDepartment,
                RequisitionFlowMode = configureSub ? RequisitionFlowMode.Custom : RequisitionFlowMode.InheritParent,
                CustomStageRoleIds = configureSub ? ProcManagerRole.ToString() : null,
                PurchaseOrderRaiserRoleId = configureSub ? ProcManagerRole : (int?)null,
                PurchaseOrderRaiserUserId = configureSub ? "pm-2" : null
            });
            unitOfWork.Seed(new Department
            {
                Id = 3, Name = "Biology Lab", Code = "BIO", IsActive = true, ParentDepartmentId = 2, DepartmentKind = DepartmentKind.Room,
                IsRequisitionTarget = true, RequisitionFlowMode = RequisitionFlowMode.InheritParent
            });
            return unitOfWork;
        }

        [Test]
        public void PoRaiser_InheritedFromNearestCustomAncestor()
        {
            var unitOfWork = SeedHierarchy(configureSub: true, configureDept: true);
            var room = unitOfWork.Repository<Department>().GetById(3);
            var config = ApprovalWorkflowHelper.GetDepartmentRequisitionConfiguration(unitOfWork, room);
            Assert.AreEqual(ProcManagerRole, config.PurchaseOrderRaiserRoleId);
            Assert.AreEqual("pm-2", config.PurchaseOrderRaiserUserId);
            Assert.AreEqual(2, config.PurchaseOrderRaiserSourceDepartmentId);
        }

        [Test]
        public void PoRaiser_InheritFromDepartment_SkipsSub()
        {
            var unitOfWork = SeedHierarchy(configureSub: true, configureDept: true);
            var room = unitOfWork.Repository<Department>().GetById(3);
            room.RequisitionInheritFrom = RequisitionInheritSource.Department;
            var config = ApprovalWorkflowHelper.GetDepartmentRequisitionConfiguration(unitOfWork, room);
            Assert.AreEqual(ProcOfficerRole, config.PurchaseOrderRaiserRoleId);
            Assert.IsNull(config.PurchaseOrderRaiserUserId);
            Assert.AreEqual(1, config.PurchaseOrderRaiserSourceDepartmentId);
        }

        [Test]
        public void PoRaiser_NothingConfigured_IsUnconfigured()
        {
            var unitOfWork = SeedHierarchy(configureSub: false, configureDept: false);
            var room = unitOfWork.Repository<Department>().GetById(3);
            var config = ApprovalWorkflowHelper.GetDepartmentRequisitionConfiguration(unitOfWork, room);
            Assert.IsFalse(PurchaseOrderRaiserRules.IsConfigured(config));
        }

        [Test]
        public void PoRaiser_InheritMode_ClearsOwnSetting_OnSave()
        {
            var entity = new Department { Id = 3, PurchaseOrderRaiserRoleId = ProcManagerRole, PurchaseOrderRaiserUserId = "pm-1", RequisitionFlowMode = RequisitionFlowMode.Custom };
            DepartmentRequisitionApprovalSettingsHelper.ApplyToDepartment(entity, new DepartmentVm
            {
                RequisitionFlowMode = RequisitionFlowMode.InheritParent,
                PurchaseOrderRaiserRoleId = ProcManagerRole,
                PurchaseOrderRaiserUserId = "pm-1"
            });
            Assert.IsNull(entity.PurchaseOrderRaiserRoleId);
            Assert.IsNull(entity.PurchaseOrderRaiserUserId);
        }

        // ---------- PO raiser enforcement ----------

        [Test]
        public void PoRaiserRules_CanRaise()
        {
            var person = new ApprovalProcessConfiguration { PurchaseOrderRaiserRoleId = ProcManagerRole, PurchaseOrderRaiserUserId = "pm-2" };
            Assert.IsTrue(PurchaseOrderRaiserRules.CanRaise(person, "pm-2", ProcManagerRole));
            Assert.IsFalse(PurchaseOrderRaiserRules.CanRaise(person, "pm-1", ProcManagerRole), "same role, not the person");
            Assert.IsFalse(PurchaseOrderRaiserRules.CanRaise(person, "admin", 59), "Company Admin is not exempt");

            var roleOnly = new ApprovalProcessConfiguration { PurchaseOrderRaiserRoleId = ProcManagerRole };
            Assert.IsTrue(PurchaseOrderRaiserRules.CanRaise(roleOnly, "pm-1", ProcManagerRole));
            Assert.IsFalse(PurchaseOrderRaiserRules.CanRaise(roleOnly, "po-1", ProcOfficerRole));

            var none = new ApprovalProcessConfiguration();
            Assert.IsTrue(PurchaseOrderRaiserRules.CanRaise(none, "anyone", ProcOfficerRole));
            Assert.IsTrue(PurchaseOrderRaiserRules.CanRaise(null, "anyone", null));

            var everyone = Users().Values.ToList();
            CollectionAssert.AreEquivalent(new[] { "pm-2" }, PurchaseOrderRaiserRules.ResolveNotifyUserIds(person, everyone));
            CollectionAssert.AreEquivalent(new[] { "pm-1", "pm-2" }, PurchaseOrderRaiserRules.ResolveNotifyUserIds(roleOnly, everyone.Where(x => x.OrganizationId == Org)));
            Assert.IsEmpty(PurchaseOrderRaiserRules.ResolveNotifyUserIds(none, everyone));
        }

        private static PurchaseService BuildPurchaseService(FakeUnitOfWork unitOfWork)
        {
            return new PurchaseService(unitOfWork, new FakeOperationsQueryRepository(), new FakeOrganizationScopeService(organizationId: Org), new NoOpAuditWriter());
        }

        private static void SeedApprovedRequest(FakeUnitOfWork unitOfWork, int id)
        {
            unitOfWork.Seed(new PurchaseRequest
            {
                Id = id, DepartmentId = 3, RequestedById = "requester", ApprovalStatus = ApprovalStatus.Approved,
                RequestNumber = "PR-" + id, IsActive = true, OrganizationId = Org, Quantity = 5 // rel1009i: POs are capped by the requisition quantity
            });
        }

        [Test]
        public void PurchaseService_Create_ConfiguredPerson_AllowedOthersBlocked()
        {
            var unitOfWork = SeedHierarchy(configureSub: true, configureDept: false);
            SeedApprovedRequest(unitOfWork, 700);
            var service = BuildPurchaseService(unitOfWork);

            Assert.Throws<BusinessException>(() => service.Create(new PurchaseRecordVm { PurchaseRequestId = 700, Quantity = 1, UnitCost = 10, Currency = "KES" }, "pm-1", ProcManagerRole));
            Assert.Throws<BusinessException>(() => service.Create(new PurchaseRecordVm { PurchaseRequestId = 700, Quantity = 1, UnitCost = 10, Currency = "KES" }, "company-admin", 59), "Company Admin not exempt");
            Assert.Throws<BusinessException>(() => service.Create(new PurchaseRecordVm { PurchaseRequestId = 700, Quantity = 1, UnitCost = 10, Currency = "KES" }), "no actor = fail closed");
            var id = service.Create(new PurchaseRecordVm { PurchaseRequestId = 700, Quantity = 1, UnitCost = 10, Currency = "KES" }, "pm-2", ProcManagerRole);
            Assert.Greater(id, -1);
        }

        [Test]
        public void PurchaseService_Create_Unconfigured_AnyPurchasesUserAllowed()
        {
            var unitOfWork = SeedHierarchy(configureSub: false, configureDept: false);
            SeedApprovedRequest(unitOfWork, 701);
            var service = BuildPurchaseService(unitOfWork);
            Assert.DoesNotThrow(() => service.Create(new PurchaseRecordVm { PurchaseRequestId = 701, Quantity = 1, UnitCost = 10, Currency = "KES" }, "po-1", ProcOfficerRole));
        }

        [Test]
        public void FinalApproval_ConfiguredRaiser_OnlyRaiserGetsReadyForPoNotification()
        {
            var unitOfWork = SeedHierarchy(configureSub: true, configureDept: false);
            var users = SeedUsers();
            var outbox = new CapturingOutbox();
            var service = BuildRequestService(unitOfWork, users, outbox);
            var id = service.SubmitCompleted(unitOfWork, new PurchaseRequestCreateVm
            {
                DepartmentId = 3, ItemDescription = "Beakers", Justification = "Lab", Quantity = 1, Currency = "KES"
            }, "requester");

            service.Approve(new PurchaseRequestApprovalVm { PurchaseRequestId = id }, "pm-1", ProcManagerRole, false);

            var ready = outbox.Sent.Where(x => x.Contains("Ready for purchase order")).ToList();
            Assert.AreEqual(1, ready.Count);
            StringAssert.Contains("pm-2", ready[0]);
            Assert.IsTrue(outbox.Sent.Any(x => x.Contains("requester") && x.Contains("will be raised by")));
        }

        [Test]
        public void FinalApproval_Unconfigured_KeepsLegacyRequesterMessage()
        {
            var unitOfWork = SeedHierarchy(configureSub: false, configureDept: false);
            var room = unitOfWork.Repository<Department>().GetById(3);
            room.RequisitionFlowMode = RequisitionFlowMode.Custom;
            room.CustomStageRoleIds = ProcManagerRole.ToString();
            var outbox = new CapturingOutbox();
            var service = BuildRequestService(unitOfWork, SeedUsers(), outbox);
            var id = service.SubmitCompleted(unitOfWork, new PurchaseRequestCreateVm
            {
                DepartmentId = 3, ItemDescription = "Beakers", Justification = "Lab", Quantity = 1, Currency = "KES"
            }, "requester");
            service.Approve(new PurchaseRequestApprovalVm { PurchaseRequestId = id }, "pm-1", ProcManagerRole, false);

            Assert.IsFalse(outbox.Sent.Any(x => x.Contains("Ready for purchase order")));
            Assert.IsTrue(outbox.Sent.Any(x => x.Contains("You can record the purchase order when ready.")));
        }

        // ---------- helpers ----------

        private static FakeUserService SeedUsers()
        {
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "requester", IsActive = true, RoleId = 9, Email = "req@x.local" });
            users.Seed(new UserVm { Id = "po-1", IsActive = true, RoleId = ProcOfficerRole, Email = "po1@x.local" });
            users.Seed(new UserVm { Id = "pm-1", IsActive = true, RoleId = ProcManagerRole, Email = "pm1@x.local" });
            users.Seed(new UserVm { Id = "pm-2", IsActive = true, RoleId = ProcManagerRole, Email = "pm2@x.local", FirstName = "Pat", LastName = "Two" });
            return users;
        }

        private static PurchaseRequestService BuildRequestService(FakeUnitOfWork unitOfWork, FakeUserService users, IOutboxWriter outbox)
        {
            return new PurchaseRequestService(
                unitOfWork,
                new NoOpAuditWriter(),
                users,
                new NoOpDepartmentScopeService(),
                new FakeOrganizationScopeService(organizationId: Org),
                outbox,
                new NoOpWebhookService(),
                new FakeApprovalWorkflowEngine(unitOfWork, new NoOpAuditWriter()),
                new FakeOperationsQueryRepository());
        }

        private sealed class CapturingOutbox : IOutboxWriter
        {
            public readonly List<string> Sent = new List<string>();

            public void Enqueue(string messageType, string payloadJson)
            {
                Sent.Add(payloadJson ?? string.Empty);
            }
        }
    }
}

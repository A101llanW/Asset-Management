using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AssetManagement.Application;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Approvals
{
    /// <summary>
    /// rel1009e: one shared resolver for pending-approval notifications and approval eligibility. Several HODs per
    /// department; HOD (and any role) stages are scoped to the request's department, then nearest ancestor, then
    /// no-department holders, then org-wide.
    /// </summary>
    [TestFixture]
    public class StageApproverScopeTests
    {
        private const int Hod = 8;
        private const int ProcOfficer = 9;
        private const int Staff = 3;

        // School(1) -> Science(10) -> Lab Room(11);  School(1) -> Arts(20);  Library(30) has no HOD anywhere up.
        private static FakeUnitOfWork Seed()
        {
            var uow = new FakeUnitOfWork();
            uow.Seed(new Department { Id = 1, Name = "School", IsActive = true, OrganizationId = 1, RequisitionFlowMode = RequisitionFlowMode.Custom, CustomStageRoleIds = Hod.ToString() });
            uow.Seed(new Department { Id = 10, Name = "Science", ParentDepartmentId = 1, IsActive = true, IsRequisitionTarget = true, OrganizationId = 1 });
            uow.Seed(new Department { Id = 11, Name = "Lab Room", ParentDepartmentId = 10, IsActive = true, IsRequisitionTarget = true, OrganizationId = 1 });
            uow.Seed(new Department { Id = 20, Name = "Arts", ParentDepartmentId = 1, IsActive = true, IsRequisitionTarget = true, OrganizationId = 1 });
            uow.Seed(new Department { Id = 30, Name = "Library", IsActive = true, IsRequisitionTarget = true, OrganizationId = 1 });
            uow.Seed(new Role { Id = Hod, Name = "Department Head", IsActive = true, OrganizationId = 1 });
            uow.Seed(new Role { Id = ProcOfficer, Name = "Procurement Officer", IsActive = true, OrganizationId = 1 });
            uow.Seed(new Role { Id = Staff, Name = "Staff", IsActive = true, OrganizationId = 1 });
            return uow;
        }

        private static FakeUserService Users(bool includeLibraryProcNoDept = true)
        {
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "hod-sci-1", RoleId = Hod, DepartmentId = 10, IsActive = true });
            users.Seed(new UserVm { Id = "hod-sci-2", RoleId = Hod, DepartmentId = 10, IsActive = true });
            users.Seed(new UserVm { Id = "hod-sci-old", RoleId = Hod, DepartmentId = 10, IsActive = false });
            users.Seed(new UserVm { Id = "hod-arts", RoleId = Hod, DepartmentId = 20, IsActive = true });
            users.Seed(new UserVm { Id = "staff-sci", RoleId = Staff, DepartmentId = 10, IsActive = true });
            users.Seed(new UserVm { Id = "staff-lab", RoleId = Staff, DepartmentId = 11, IsActive = true });
            users.Seed(new UserVm { Id = "proc-sci", RoleId = ProcOfficer, DepartmentId = 10, IsActive = true });
            users.Seed(new UserVm { Id = "proc-arts", RoleId = ProcOfficer, DepartmentId = 20, IsActive = true });
            if (includeLibraryProcNoDept)
            {
                users.Seed(new UserVm { Id = "proc-nodept", RoleId = ProcOfficer, DepartmentId = null, IsActive = true });
            }

            return users;
        }

        private static StageApproverScope Resolve(FakeUnitOfWork uow, FakeUserService users, int? role, string person, int? dept)
        {
            return StageApproverScopeResolver.ResolveFor(uow, users, role, person, dept);
        }

        [Test]
        public void Resolver_TwoHodsInDepartment_BothChosen_OtherDepartmentAndInactiveExcluded()
        {
            var s = Resolve(Seed(), Users(), Hod, null, 10);
            Assert.AreEqual(StageApproverScopeKind.Department, s.Kind);
            CollectionAssert.AreEquivalent(new[] { "hod-sci-1", "hod-sci-2" }, s.UserIds);
            Assert.IsFalse(s.Contains("hod-arts"));
            Assert.IsFalse(s.Contains("hod-sci-old"));
        }

        [Test]
        public void Resolver_NoHodInRoom_FallsBackToNearestAncestor()
        {
            var s = Resolve(Seed(), Users(), Hod, null, 11);
            Assert.AreEqual(StageApproverScopeKind.AncestorDepartment, s.Kind);
            Assert.AreEqual(10, s.ResolvedDepartmentId);
            CollectionAssert.AreEquivalent(new[] { "hod-sci-1", "hod-sci-2" }, s.UserIds);
        }

        [Test]
        public void Resolver_NoHodInChain_NoDeptlessHod_FallsBackToOrgWide()
        {
            var s = Resolve(Seed(), Users(), Hod, null, 30);
            Assert.AreEqual(StageApproverScopeKind.Organization, s.Kind);
            CollectionAssert.AreEquivalent(new[] { "hod-sci-1", "hod-sci-2", "hod-arts" }, s.UserIds);
        }

        [Test]
        public void Resolver_OrgWideRole_NoHolderInChain_UsesNoDepartmentHoldersBeforeOrgWide()
        {
            var s = Resolve(Seed(), Users(), ProcOfficer, null, 30);
            Assert.AreEqual(StageApproverScopeKind.NoDepartment, s.Kind);
            CollectionAssert.AreEquivalent(new[] { "proc-nodept" }, s.UserIds);

            var withoutDeptless = Resolve(Seed(), Users(includeLibraryProcNoDept: false), ProcOfficer, null, 30);
            Assert.AreEqual(StageApproverScopeKind.Organization, withoutDeptless.Kind);
            CollectionAssert.AreEquivalent(new[] { "proc-sci", "proc-arts" }, withoutDeptless.UserIds);
        }

        [Test]
        public void Resolver_NamedPerson_Unchanged_AndNoDepartmentMeansOrgWide()
        {
            var named = Resolve(Seed(), Users(), Hod, "hod-arts", 10);
            Assert.AreEqual(StageApproverScopeKind.NamedPerson, named.Kind);
            CollectionAssert.AreEqual(new[] { "hod-arts" }, named.UserIds);

            var legacy = Resolve(Seed(), Users(), Hod, null, null);
            Assert.AreEqual(StageApproverScopeKind.Organization, legacy.Kind);
            Assert.AreEqual(3, legacy.UserIds.Count);
        }

        [Test]
        public void Resolver_ParentCycle_DoesNotLoop()
        {
            var uow = Seed();
            uow.Seed(new Department { Id = 40, Name = "A", ParentDepartmentId = 41, IsActive = true, OrganizationId = 1 });
            uow.Seed(new Department { Id = 41, Name = "B", ParentDepartmentId = 40, IsActive = true, OrganizationId = 1 });
            var s = Resolve(uow, Users(), Hod, null, 40);
            Assert.AreEqual(StageApproverScopeKind.Organization, s.Kind);
        }

        [Test]
        public void Eligibility_HodOfOtherDepartment_Refused_SameDepartmentAllowed()
        {
            var scope = Resolve(Seed(), Users(), Hod, null, 10);
            Assert.IsTrue(ApprovalWorkflowHelper.CanUserActOnStage(null, "staff-sci", "hod-sci-2", false, Hod, Hod, null, scope));
            Assert.IsFalse(ApprovalWorkflowHelper.CanUserActOnStage(null, "staff-sci", "hod-arts", false, Hod, Hod, null, scope));
            Assert.Throws<BusinessException>(() => ApprovalWorkflowHelper.EnsureUserCanApprove(
                null, null, Hod, false, Hod, null, "staff-sci", "hod-arts", ApprovalProcessCodes.Purchase, scope));
            Assert.DoesNotThrow(() => ApprovalWorkflowHelper.EnsureUserCanApprove(
                null, null, Hod, false, Hod, null, "staff-sci", "hod-sci-1", ApprovalProcessCodes.Purchase, scope));
            // Super admin bypass unchanged.
            Assert.IsTrue(ApprovalWorkflowHelper.CanUserActOnStage(null, "staff-sci", "admin", true, 59, Hod, null, scope));
        }

        // ---------------- Requisition (PurchaseRequestService) ----------------

        private static PurchaseRequestService Requisitions(FakeUnitOfWork uow, FakeUserService users, CapturingOutbox outbox, string currentUser = null)
        {
            return new PurchaseRequestService(
                uow,
                new NoOpAuditWriter(),
                users,
                new NoOpDepartmentScopeService(),
                new FakeOrganizationScopeService(organizationId: 1),
                outbox,
                new NoOpWebhookService(),
                new FakeApprovalWorkflowEngine(uow, new NoOpAuditWriter()),
                new FakeOperationsQueryRepository(),
                null,
                currentUser == null ? null : new FakeCurrentUserContext(currentUser));
        }

        private static void HodRequisitionFlow(FakeUnitOfWork uow)
        {
            uow.Seed(new SystemSetting { Id = 1, SettingKey = "Approval.Purchase.Enabled", SettingValue = "true", IsActive = true });
            uow.Seed(new SystemSetting { Id = 2, SettingKey = "Approval.Purchase.StageRoleIds", SettingValue = Hod.ToString(), IsActive = true });
        }

        private static int SubmitRequisition(FakeUnitOfWork uow, PurchaseRequestService service, int departmentId, string requester)
        {
            return service.SubmitCompleted(uow, new PurchaseRequestCreateVm
            {
                DepartmentId = departmentId,
                ItemDescription = "Beakers",
                Justification = "Lab restock",
                Quantity = 1,
                Currency = "KES"
            }, requester);
        }

        [Test]
        public void Requisition_BothDepartmentHodsNotified_OtherHodNot_EitherApproves_SecondBlocked()
        {
            var uow = Seed();
            HodRequisitionFlow(uow);
            var users = Users();
            var outbox = new CapturingOutbox();
            var id = SubmitRequisition(uow, Requisitions(uow, users, outbox), 10, "staff-sci");

            var approvers = outbox.RecipientsOf("Purchase approval required");
            CollectionAssert.AreEquivalent(new[] { "hod-sci-1", "hod-sci-2" }, approvers);

            var other = Assert.Throws<BusinessException>(() => Requisitions(uow, users, outbox, "hod-arts")
                .Approve(new PurchaseRequestApprovalVm { PurchaseRequestId = id }, "hod-arts", Hod, false));
            StringAssert.Contains("not configured to approve", other.Message);

            Requisitions(uow, users, outbox, "hod-sci-2").Approve(new PurchaseRequestApprovalVm { PurchaseRequestId = id }, "hod-sci-2", Hod, false);
            Assert.AreEqual(ApprovalStatus.Approved, uow.Repository<PurchaseRequest>().GetById(id).ApprovalStatus);

            var second = Assert.Throws<BusinessException>(() => Requisitions(uow, users, outbox, "hod-sci-1")
                .Approve(new PurchaseRequestApprovalVm { PurchaseRequestId = id }, "hod-sci-1", Hod, false));
            StringAssert.Contains("no longer pending", second.Message);
        }

        [Test]
        public void Requisition_RoomWithoutHod_ParentHodsNotifiedAndCanApprove()
        {
            var uow = Seed();
            HodRequisitionFlow(uow);
            var users = Users();
            var outbox = new CapturingOutbox();
            var id = SubmitRequisition(uow, Requisitions(uow, users, outbox), 11, "staff-lab");

            CollectionAssert.AreEquivalent(new[] { "hod-sci-1", "hod-sci-2" }, outbox.RecipientsOf("Purchase approval required"));
            Assert.Throws<BusinessException>(() => Requisitions(uow, users, outbox, "hod-arts")
                .Approve(new PurchaseRequestApprovalVm { PurchaseRequestId = id }, "hod-arts", Hod, false));
            Requisitions(uow, users, outbox, "hod-sci-1").Approve(new PurchaseRequestApprovalVm { PurchaseRequestId = id }, "hod-sci-1", Hod, false);
            Assert.AreEqual(ApprovalStatus.Approved, uow.Repository<PurchaseRequest>().GetById(id).ApprovalStatus);
        }

        [Test]
        public void Requisition_ParentHodWithStrictDepartmentScope_CanOpenRoomRequisition_OtherHodCannot()
        {
            var uow = Seed();
            HodRequisitionFlow(uow);
            var users = Users();
            var outbox = new CapturingOutbox();
            var id = SubmitRequisition(uow, Requisitions(uow, users, outbox), 11, "staff-lab");

            Func<string, int, PurchaseRequestService> scoped = (user, dept) => new PurchaseRequestService(
                uow, new NoOpAuditWriter(), users, new StrictDepartmentScopeService(dept), new FakeOrganizationScopeService(organizationId: 1),
                outbox, new NoOpWebhookService(), new FakeApprovalWorkflowEngine(uow, new NoOpAuditWriter()), new FakeOperationsQueryRepository(),
                new DenyAllAuthorizationService(), new FakeCurrentUserContext(user));

            Assert.IsNotNull(scoped("hod-sci-1", 10).GetById(id));
            Assert.Throws<BusinessException>(() => scoped("hod-arts", 20).GetById(id));
        }

        // ---------------- Transfer (TransferService) ----------------

        private static TransferService Transfers(FakeUnitOfWork uow, FakeUserService users, CapturingOutbox outbox)
        {
            return new TransferService(
                uow,
                new NoOpAuditWriter(),
                users,
                new NoOpDepartmentScopeService(),
                new FakeOrganizationScopeService(organizationId: 1),
                outbox,
                new NoOpWebhookService(),
                new NoOpWorkflowGuard(),
                new FakeApprovalWorkflowEngine(uow, new NoOpAuditWriter()));
        }

        private static void SeedAsset(FakeUnitOfWork uow, int id, int departmentId, string custodian)
        {
            uow.Seed(new Asset
            {
                Id = id, AssetTag = "AST-" + id, AssetName = "Asset " + id, CategoryId = 1, AssetTypeId = 1, SupplierId = 1,
                DepartmentId = departmentId, CurrentCustodianId = custodian, Currency = "KES", AcquisitionCost = 1000,
                CurrentStatus = AssetStatus.Assigned, PurchaseDate = DateTime.UtcNow, DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow, UsefulLifeMonths = 36, OrganizationId = 1, IsActive = true, CreatedAt = DateTime.UtcNow,
                RequireTransferApproval = true, TransferApprovalStageRoleIds = Hod.ToString()
            });
        }

        [Test]
        public void Transfer_BothDepartmentHodsNotified_OtherHodRefused_FirstWins()
        {
            var uow = Seed();
            var users = Users();
            users.Seed(new UserVm { Id = "staff-sci-2", RoleId = Staff, DepartmentId = 10, IsActive = true });
            SeedAsset(uow, 500, 10, "staff-sci");
            var outbox = new CapturingOutbox();
            var result = Transfers(uow, users, outbox).Transfer(new AssetTransferVm { AssetId = 500, ToUserId = "staff-sci-2", ToDepartmentId = 10, Reason = "swap" }, "staff-sci");
            Assert.IsTrue(result.RequiresApproval);
            CollectionAssert.AreEquivalent(new[] { "hod-sci-1", "hod-sci-2" }, outbox.RecipientsOf("Transfer approval required"));

            var refused = Assert.Throws<BusinessException>(() => Transfers(uow, users, outbox)
                .ApproveTransfer(new TransferApprovalDecisionVm { TransferId = result.TransferId }, "hod-arts", Hod, false));
            StringAssert.Contains("not configured to approve", refused.Message);

            Transfers(uow, users, outbox).ApproveTransfer(new TransferApprovalDecisionVm { TransferId = result.TransferId }, "hod-sci-1", Hod, false);
            Assert.AreEqual(ApprovalStatus.Approved, uow.Repository<AssetTransfer>().GetById(result.TransferId).ApprovalStatus);
            Assert.Throws<BusinessException>(() => Transfers(uow, users, outbox)
                .ApproveTransfer(new TransferApprovalDecisionVm { TransferId = result.TransferId }, "hod-sci-2", Hod, false));
        }

        [Test]
        public void Transfer_RoomAsset_ParentHodsNotified()
        {
            var uow = Seed();
            var users = Users();
            users.Seed(new UserVm { Id = "staff-lab-2", RoleId = Staff, DepartmentId = 11, IsActive = true });
            SeedAsset(uow, 501, 11, "staff-lab");
            var outbox = new CapturingOutbox();
            var result = Transfers(uow, users, outbox).Transfer(new AssetTransferVm { AssetId = 501, ToUserId = "staff-lab-2", ToDepartmentId = 11, Reason = "swap" }, "staff-lab");
            CollectionAssert.AreEquivalent(new[] { "hod-sci-1", "hod-sci-2" }, outbox.RecipientsOf("Transfer approval required"));
            Transfers(uow, users, outbox).ApproveTransfer(new TransferApprovalDecisionVm { TransferId = result.TransferId }, "hod-sci-2", Hod, false);
        }

        // ---------------- PO raiser ----------------

        [Test]
        public void PoRaiser_RoleOnly_ScopedToRequisitionDepartment()
        {
            var uow = Seed();
            var users = Users();
            var config = new ApprovalProcessConfiguration { PurchaseOrderRaiserRoleId = ProcOfficer };
            var scope = PurchaseOrderRaiserRules.ResolveRaiserScope(uow, users, config, 20);
            CollectionAssert.AreEquivalent(new[] { "proc-arts" }, scope.UserIds);
            Assert.IsTrue(PurchaseOrderRaiserRules.CanRaise(config, "proc-arts", ProcOfficer, scope));
            Assert.IsFalse(PurchaseOrderRaiserRules.CanRaise(config, "proc-sci", ProcOfficer, scope));
            // No scope supplied = legacy role check.
            Assert.IsTrue(PurchaseOrderRaiserRules.CanRaise(config, "proc-sci", ProcOfficer));
        }

        internal sealed class CapturingOutbox : IOutboxWriter
        {
            private static readonly Regex UserIdPattern = new Regex("\"userId\":\"([^\"]*)\"");
            private static readonly Regex SubjectPattern = new Regex("\"subject\":\"([^\"]*)\"");
            public readonly List<string> Payloads = new List<string>();

            public void Enqueue(string messageType, string payloadJson)
            {
                Payloads.Add(payloadJson);
            }

            public IList<string> RecipientsOf(string subjectPrefix)
            {
                return Payloads
                    .Where(p => SubjectPattern.Match(p).Groups[1].Value.StartsWith(subjectPrefix, StringComparison.Ordinal))
                    .Select(p => UserIdPattern.Match(p).Groups[1].Value)
                    .ToList();
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.Services.LabelPrint;
using AssetManagement.Application.ViewModels;
using AssetManagement.Tests.Helpers;
using AssetManagement.Web.Controllers;
using NUnit.Framework;

namespace AssetManagement.Tests.Assets
{
    /// <summary>
    /// rel1022a: label print log, print batches, clear and reprint. Uses the dry-run dispatcher and an in-memory
    /// store only: nothing here opens a socket, calls Zebra Browser Print or touches a database.
    /// </summary>
    [TestFixture]
    public class LabelPrintLogServiceTests
    {
        private const int Org = 1;
        private static readonly DateTime Now = new DateTime(2026, 10, 22, 7, 30, 0, DateTimeKind.Utc); // 10:30 EAT

        private FakeLabelPrintBatchStore _store;
        private DryRunLabelPrintDispatcher _dispatcher;
        private RecordingAuditWriter _audit;
        private LabelPrintLogService _service;

        [SetUp]
        public void SetUp()
        {
            _audit = new RecordingAuditWriter();
            _store = new FakeLabelPrintBatchStore(_audit);
            _store.AddAsset(Org, 101, "A-101", "Desk");
            _store.AddAsset(Org, 102, "A-102", "Chair");
            _store.AddAsset(Org, 103, "A-103", "Table");
            _store.AddAsset(Org, 104, "A-104", "Old lamp", isActive: false);
            _store.AddAsset(2, 201, "B-201", "Other tenant asset");
            _dispatcher = new DryRunLabelPrintDispatcher();
            _service = new LabelPrintLogService(_store, _dispatcher, _audit, new FakeOrganizationScopeService(Org), () => Now);
        }

        // ---- per-label logging ----

        [Test]
        public void RecordPrints_FirstPrint_WritesOneAssetAuditRowWithTagAndDispatchesToTheMock()
        {
            var result = _service.RecordPrints(new LabelPrintRecordRequestVm { AssetIds = new List<int> { 101 }, Source = "Single", Mode = "Zebra", CodeType = "QR", TemplateId = "t1" }, "user-a");

            Assert.AreEqual(1, result.Items.Count);
            Assert.IsFalse(result.Items[0].IsReprint);
            Assert.AreEqual(1, _audit.Rows.Count);
            var row = _audit.Rows[0];
            Assert.AreEqual(LabelPrintAuditActions.Print, row.Action);
            Assert.AreEqual("Asset", row.EntityType);
            Assert.AreEqual("101", row.EntityId);
            StringAssert.Contains("Tag=A-101", row.NewValues);
            StringAssert.Contains("Print=First print", row.NewValues);
            StringAssert.Contains("Source=Single", row.NewValues);
            StringAssert.Contains("Code=QR", row.NewValues);
            StringAssert.Contains("Template=t1", row.NewValues);
            StringAssert.Contains("Dispatch=DryRun", row.NewValues);

            Assert.AreEqual(1, _dispatcher.Jobs.Count);
            Assert.AreEqual("A-101", _dispatcher.Jobs[0].AssetTag);
            Assert.AreEqual("user-a", _dispatcher.Jobs[0].RequestedByUserId);
            Assert.AreEqual(Now, _dispatcher.Jobs[0].RequestedAtUtc);
        }

        [Test]
        public void RecordPrints_AssetPrintedBefore_IsLoggedAsReprint()
        {
            _service.RecordPrints(Request(101), "user-a");
            var second = _service.RecordPrints(Request(101), "user-a");

            Assert.IsTrue(second.Items[0].IsReprint);
            Assert.AreEqual(LabelPrintAuditActions.Reprint, _audit.Rows[1].Action);
            StringAssert.Contains("Print=Reprint", _audit.Rows[1].NewValues);
            StringAssert.Contains("Count=2", _audit.Rows[1].NewValues);
        }

        [Test]
        public void RecordPrints_ExplicitReprintFlag_IsLoggedAsReprintEvenOnFirstPrint()
        {
            var request = Request(102);
            request.Reprint = true;
            var result = _service.RecordPrints(request, "user-a");
            Assert.IsTrue(result.Items[0].IsReprint);
            Assert.AreEqual(LabelPrintAuditActions.Reprint, _audit.Rows.Single().Action);
        }

        [Test]
        public void RecordPrints_BrowserDialogMode_IsRecorded()
        {
            var request = Request(101);
            request.Mode = "browserdialog";
            _service.RecordPrints(request, "user-a");
            StringAssert.Contains("Mode=BrowserDialog", _audit.Rows.Single().NewValues);
        }

        [Test]
        public void RecordPrints_UnknownInactiveAndOtherTenantIds_AreSkippedWithoutAudit()
        {
            var result = _service.RecordPrints(new LabelPrintRecordRequestVm { AssetIds = new List<int> { 101, 104, 201, 999 } }, "user-a");
            Assert.AreEqual(1, result.Items.Count);
            Assert.AreEqual(3, result.SkippedCount);
            Assert.AreEqual(1, _audit.Rows.Count);
            Assert.AreEqual(1, _dispatcher.Jobs.Count);
        }

        [Test]
        public void RecordPrints_DuplicateIds_LogOnce()
        {
            _service.RecordPrints(new LabelPrintRecordRequestVm { AssetIds = new List<int> { 101, 101, 0, -5 } }, "user-a");
            Assert.AreEqual(1, _audit.Rows.Count);
        }

        [Test]
        public void RecordPrints_EmptyOrTooMany_Throws()
        {
            Assert.Throws<BusinessException>(() => _service.RecordPrints(new LabelPrintRecordRequestVm(), "user-a"));
            Assert.Throws<BusinessException>(() => _service.RecordPrints(null, "user-a"));
            var many = new LabelPrintRecordRequestVm { AssetIds = Enumerable.Range(1, LabelPrintLogService.MaxRecordPerCall + 1).ToList() };
            Assert.Throws<BusinessException>(() => _service.RecordPrints(many, "user-a"));
        }

        [Test]
        public void RecordPrints_WithBatch_CountsTheLabelOnTheBatchAndTagsTheAuditRow()
        {
            var batch = _service.CreateBatch(BatchRequest(101, 102), "user-a");
            _service.RecordPrints(new LabelPrintRecordRequestVm { AssetIds = new List<int> { 101 }, BatchId = batch.Id, Source = "Single" }, "user-a");

            var stored = _store.Batches[batch.Id];
            Assert.AreEqual(1, stored.PrintedCount);
            Assert.AreEqual(LabelPrintLogService.StatusPrinting, stored.Status);
            Assert.AreEqual(1, _store.Items[batch.Id].Single(i => i.AssetId == 101).PrintCount);
            var printRow = _audit.Rows.Last();
            StringAssert.Contains("Batch=" + batch.Id, printRow.NewValues);
            StringAssert.Contains("Source=Bulk", printRow.NewValues);
        }

        [Test]
        public void RecordPrints_UnknownOrOtherTenantBatch_Throws()
        {
            Assert.Throws<BusinessException>(() => _service.RecordPrints(new LabelPrintRecordRequestVm { AssetIds = new List<int> { 101 }, BatchId = 999 }, "user-a"));
            var otherTenantBatch = _store.CreateBatch(new LabelPrintBatchCreate { OrganizationId = 2, Source = "BulkPrint", CreatedById = "x", CreatedAtUtc = Now, Assets = new List<LabelPrintAssetInfo> { new LabelPrintAssetInfo { AssetId = 201, AssetTag = "B-201" } } });
            Assert.Throws<BusinessException>(() => _service.RecordPrints(new LabelPrintRecordRequestVm { AssetIds = new List<int> { 101 }, BatchId = otherTenantBatch }, "user-a"));
            Assert.AreEqual(0, _audit.Rows.Count);
        }

        [Test]
        public void RecordPrints_InReprintBatch_IsAlwaysAReprint()
        {
            var original = _service.CreateBatch(BatchRequest(103), "user-a");
            var reprintBatch = _service.CreateBatch(new LabelPrintBatchCreateVm { AssetIds = new List<int> { 103 }, ReprintOfBatchId = original.Id }, "user-a");
            _audit.Rows.Clear();

            var result = _service.RecordPrints(new LabelPrintRecordRequestVm { AssetIds = new List<int> { 103 }, BatchId = reprintBatch.Id }, "user-a");
            Assert.IsTrue(result.Items[0].IsReprint, "asset 103 was never printed, but the batch is a reprint batch");
            Assert.AreEqual(LabelPrintAuditActions.Reprint, _audit.Rows.Single().Action);
        }

        // ---- batches ----

        [Test]
        public void CreateBatch_IsOneRecordWithWhoWhenGroupFilterAndAssetList()
        {
            var vm = _service.CreateBatch(new LabelPrintBatchCreateVm
            {
                AssetIds = new List<int> { 102, 101 },
                Source = "AssetRegister",
                GroupLabel = "Office chairs",
                FilterDescription = "Asset register, grouped by sub-type; Status: In Store"
            }, "user-a");

            Assert.AreEqual(1, _store.Batches.Count);
            var stored = _store.Batches[vm.Id];
            Assert.AreEqual(Org, stored.OrganizationId);
            Assert.AreEqual("AssetRegister", stored.Source);
            Assert.AreEqual("Office chairs", stored.GroupLabel);
            Assert.AreEqual("Asset register, grouped by sub-type; Status: In Store", stored.FilterDescription);
            Assert.AreEqual("user-a", stored.CreatedById);
            Assert.AreEqual(Now, stored.CreatedAtUtc);
            Assert.AreEqual(2, stored.AssetCount);
            Assert.IsFalse(stored.IsReprint);
            CollectionAssert.AreEqual(new[] { 102, 101 }, _store.Items[vm.Id].Select(i => i.AssetId).ToArray(), "selection order is kept");

            Assert.AreEqual("Asset register", vm.SourceLabel);
            Assert.AreEqual(2, vm.Items.Count);
            Assert.AreEqual("2026-10-22 10:30 EAT", vm.CreatedAtDisplay);
            var audit = _audit.Rows.Single();
            Assert.AreEqual(LabelPrintLogService.AuditActionBatchCreate, audit.Action);
            Assert.AreEqual("LabelPrintBatch", audit.EntityType);
            Assert.AreEqual(vm.Id.ToString(), audit.EntityId);
            Assert.AreEqual(0, _dispatcher.Jobs.Count, "queueing a batch prints nothing");
        }

        [Test]
        public void CreateBatch_DropsInactiveAndOtherTenantAssets_AndRejectsWhenNoneLeft()
        {
            var vm = _service.CreateBatch(BatchRequest(101, 104, 201), "user-a");
            CollectionAssert.AreEqual(new[] { 101 }, vm.Items.Select(i => i.AssetId).ToArray());
            StringAssert.Contains("Skipped=2", _audit.Rows.Single().NewValues);

            Assert.Throws<BusinessException>(() => _service.CreateBatch(BatchRequest(104, 201), "user-a"));
            Assert.Throws<BusinessException>(() => _service.CreateBatch(BatchRequest(), "user-a"));
        }

        [Test]
        public void CreateBatch_MoreThanTheCap_Throws()
        {
            var ids = Enumerable.Range(1, LabelPrintLogService.MaxBatchAssets + 1).ToArray();
            Assert.Throws<BusinessException>(() => _service.CreateBatch(BatchRequest(ids), "user-a"));
            Assert.AreEqual(0, _store.Batches.Count);
        }

        [Test]
        public void CreateBatch_UnknownSource_FallsBackToBulkPrint()
        {
            var vm = _service.CreateBatch(new LabelPrintBatchCreateVm { AssetIds = new List<int> { 101 }, Source = "Hacker" }, "user-a");
            Assert.AreEqual(LabelPrintLogService.SourceBulkPrint, _store.Batches[vm.Id].Source);
        }

        [Test]
        public void CreateBatch_Reprint_IsLinkedLabelledAndAudited()
        {
            var original = _service.CreateBatch(new LabelPrintBatchCreateVm { AssetIds = new List<int> { 101, 102 }, GroupLabel = "Room 4", FilterDescription = "Grouped by room" }, "user-a");
            var reprint = _service.CreateBatch(new LabelPrintBatchCreateVm { AssetIds = new List<int> { 101, 102 }, ReprintOfBatchId = original.Id, GroupLabel = "ignored" }, "user-b");

            var stored = _store.Batches[reprint.Id];
            Assert.IsTrue(stored.IsReprint);
            Assert.AreEqual(original.Id, stored.ReprintOfBatchId);
            Assert.AreEqual(LabelPrintLogService.SourceReprint, stored.Source);
            Assert.AreEqual("Reprint of batch #" + original.Id + ": Room 4", stored.GroupLabel);
            Assert.AreEqual("Grouped by room", stored.FilterDescription, "the original filter is kept");
            Assert.AreEqual("user-b", stored.CreatedById);
            var audit = _audit.Rows.Last();
            Assert.AreEqual(LabelPrintLogService.AuditActionBatchReprint, audit.Action);
            StringAssert.Contains("ReprintOf=" + original.Id, audit.NewValues);
        }

        [Test]
        public void CreateBatch_ReprintOfUnknownOrOtherTenantBatch_Throws()
        {
            var otherTenantBatch = _store.CreateBatch(new LabelPrintBatchCreate { OrganizationId = 2, Source = "BulkPrint", CreatedById = "x", CreatedAtUtc = Now, Assets = new List<LabelPrintAssetInfo>() });
            Assert.Throws<BusinessException>(() => _service.CreateBatch(new LabelPrintBatchCreateVm { AssetIds = new List<int> { 101 }, ReprintOfBatchId = 999 }, "user-a"));
            Assert.Throws<BusinessException>(() => _service.CreateBatch(new LabelPrintBatchCreateVm { AssetIds = new List<int> { 101 }, ReprintOfBatchId = otherTenantBatch }, "user-a"));
        }

        [Test]
        public void CompleteBatch_SetsCompletedOrStopped()
        {
            var a = _service.CreateBatch(BatchRequest(101), "user-a");
            var b = _service.CreateBatch(BatchRequest(102), "user-a");
            _service.CompleteBatch(a.Id, false, "user-a");
            _service.CompleteBatch(b.Id, true, "user-a");
            Assert.AreEqual(LabelPrintLogService.StatusCompleted, _store.Batches[a.Id].Status);
            Assert.AreEqual(LabelPrintLogService.StatusStopped, _store.Batches[b.Id].Status);
            Assert.AreEqual(Now, _store.Batches[a.Id].CompletedAtUtc);
            Assert.Throws<BusinessException>(() => _service.CompleteBatch(999, false, "user-a"));
        }

        [Test]
        public void GetBatch_ListsOnlyActiveItems_AndIsTenantScoped()
        {
            var vm = _service.CreateBatch(BatchRequest(101, 102), "user-a");
            _store.SetActive(102, false);
            var loaded = _service.GetBatch(vm.Id, "user-a", false);
            CollectionAssert.AreEqual(new[] { 101 }, loaded.Items.Select(i => i.AssetId).ToArray());

            var otherTenantBatch = _store.CreateBatch(new LabelPrintBatchCreate { OrganizationId = 2, Source = "BulkPrint", CreatedById = "x", CreatedAtUtc = Now, Assets = new List<LabelPrintAssetInfo>() });
            Assert.IsNull(_service.GetBatch(otherTenantBatch, "user-a", true));
        }

        // ---- clear (soft-hide) ----

        [Test]
        public void ClearBatch_ByCreator_HidesTheEntryAndKeepsPerAssetAudit()
        {
            var vm = _service.CreateBatch(BatchRequest(101, 102), "user-a");
            _service.RecordPrints(new LabelPrintRecordRequestVm { AssetIds = new List<int> { 101, 102 }, BatchId = vm.Id }, "user-a");
            var printRowsBefore = _audit.Rows.Count(r => LabelPrintAuditActions.IsPrintAction(r.Action));

            Assert.IsTrue(_service.ClearBatch(vm.Id, "user-a", false));

            var stored = _store.Batches[vm.Id];
            Assert.IsTrue(stored.IsCleared);
            Assert.AreEqual("user-a", stored.ClearedById);
            Assert.AreEqual(Now, stored.ClearedAtUtc);
            Assert.AreEqual(2, _store.Items[vm.Id].Count, "batch items are kept");
            Assert.AreEqual(printRowsBefore, _audit.Rows.Count(r => LabelPrintAuditActions.IsPrintAction(r.Action)), "per-asset print audit is kept");
            Assert.AreEqual(LabelPrintLogService.AuditActionBatchClear, _audit.Rows.Last().Action);

            Assert.AreEqual(0, _service.GetBatches(false, 25, "user-a", false).Count, "cleared entries are hidden by default");
            var withCleared = _service.GetBatches(true, 25, "user-a", false);
            Assert.AreEqual(1, withCleared.Count);
            Assert.IsTrue(withCleared[0].IsCleared);
            Assert.IsFalse(withCleared[0].CanClear);
        }

        [Test]
        public void ClearBatch_ByAnotherUserWithoutManageRight_IsDenied()
        {
            var vm = _service.CreateBatch(BatchRequest(101), "user-a");
            Assert.Throws<BusinessException>(() => _service.ClearBatch(vm.Id, "user-b", false));
            Assert.IsFalse(_store.Batches[vm.Id].IsCleared);
            Assert.IsFalse(_service.GetBatches(false, 25, "user-b", false).Single().CanClear);
        }

        [Test]
        public void ClearBatch_ByManager_IsAllowed_AndSecondClearIsANoOp()
        {
            var vm = _service.CreateBatch(BatchRequest(101), "user-a");
            Assert.IsTrue(_service.GetBatches(false, 25, "manager", true).Single().CanClear);
            Assert.IsTrue(_service.ClearBatch(vm.Id, "manager", true));
            var auditCount = _audit.Rows.Count;
            Assert.IsFalse(_service.ClearBatch(vm.Id, "manager", true));
            Assert.AreEqual(auditCount, _audit.Rows.Count, "already cleared: no second audit row");
            Assert.AreEqual("manager", _store.Batches[vm.Id].ClearedById);
        }

        [Test]
        public void ClearBatch_UnknownBatch_Throws()
        {
            Assert.Throws<BusinessException>(() => _service.ClearBatch(999, "user-a", true));
        }

        [Test]
        public void ReprintOfAClearedBatch_StillWorks_AndLogsReprints()
        {
            var vm = _service.CreateBatch(BatchRequest(101, 102), "user-a");
            _service.RecordPrints(new LabelPrintRecordRequestVm { AssetIds = new List<int> { 101, 102 }, BatchId = vm.Id }, "user-a");
            _service.ClearBatch(vm.Id, "user-a", false);

            var reprint = _service.CreateBatch(new LabelPrintBatchCreateVm { AssetIds = _service.GetBatch(vm.Id, "user-a", false).Items.Select(i => i.AssetId).ToList(), ReprintOfBatchId = vm.Id }, "user-a");
            var result = _service.RecordPrints(new LabelPrintRecordRequestVm { AssetIds = new List<int> { 101, 102 }, BatchId = reprint.Id }, "user-a");

            Assert.IsTrue(result.Items.All(i => i.IsReprint));
            Assert.AreEqual(2, _audit.Rows.Count(r => r.Action == LabelPrintAuditActions.Reprint));
            Assert.AreEqual(2, _store.Batches[reprint.Id].PrintedCount);
        }

        // ---- department scope (review fix S9) ----

        private LabelPrintLogService ScopedService(FakeDeptScope scope)
        {
            return new LabelPrintLogService(_store, _dispatcher, _audit, new FakeOrganizationScopeService(Org), scope, () => Now);
        }

        private void AddScopedAssets()
        {
            _store.AddAsset(Org, 301, "D10-1", "Dept 10 desk", departmentId: 10);
            _store.AddAsset(Org, 302, "D10-2", "Dept 10 chair", departmentId: 10);
            _store.AddAsset(Org, 401, "D20-1", "Dept 20 desk", departmentId: 20);
            _store.AddAsset(Org, 501, "C30-1", "Class 30 desk", departmentId: 30, departmentIsActiveClass: true);
        }

        [Test]
        public void Scope_CreateBatch_DropsAssetsOutsideTheUsersDepartment()
        {
            AddScopedAssets();
            var vm = ScopedService(new FakeDeptScope { ScopedDepartmentId = 10 })
                .CreateBatch(new LabelPrintBatchCreateVm { AssetIds = new List<int> { 301, 401, 302 } }, "user-a");

            CollectionAssert.AreEqual(new[] { 301, 302 }, vm.Items.Select(i => i.AssetId).ToArray());
        }

        [Test]
        public void Scope_RecordPrints_SkipsAssetsOutsideTheUsersDepartment()
        {
            AddScopedAssets();
            var result = ScopedService(new FakeDeptScope { ScopedDepartmentId = 10 })
                .RecordPrints(new LabelPrintRecordRequestVm { AssetIds = new List<int> { 301, 401 } }, "user-a");

            Assert.AreEqual(1, result.Items.Count);
            Assert.AreEqual(301, result.Items[0].AssetId);
            Assert.AreEqual(1, result.SkippedCount);
        }

        [Test]
        public void Scope_GetBatchAndGetBatches_HideBatchesWithOutOfScopeAssets()
        {
            AddScopedAssets();
            var orgWide = ScopedService(new FakeDeptScope { BypassesDepartmentScope = true });
            var mine = orgWide.CreateBatch(new LabelPrintBatchCreateVm { AssetIds = new List<int> { 301, 302 } }, "admin");
            var mixed = orgWide.CreateBatch(new LabelPrintBatchCreateVm { AssetIds = new List<int> { 301, 401 } }, "admin");
            var other = orgWide.CreateBatch(new LabelPrintBatchCreateVm { AssetIds = new List<int> { 401 } }, "admin");

            var scoped = ScopedService(new FakeDeptScope { ScopedDepartmentId = 10 });
            Assert.IsNotNull(scoped.GetBatch(mine.Id, "user-a", false));
            Assert.IsNull(scoped.GetBatch(mixed.Id, "user-a", false));
            Assert.IsNull(scoped.GetBatch(other.Id, "user-a", false));
            CollectionAssert.AreEquivalent(new[] { mine.Id }, scoped.GetBatches(false, 25, "user-a", false).Select(b => b.Id).ToArray());
            Assert.Throws<BusinessException>(() => scoped.ClearBatch(other.Id, "user-a", true));
            Assert.Throws<BusinessException>(() => scoped.CompleteBatch(other.Id, false, "user-a"));

            Assert.AreEqual(3, orgWide.GetBatches(false, 25, "admin", true).Count);
        }

        [Test]
        public void Scope_ClassDepartmentAssets_FollowIncludesClassDepartmentAssets()
        {
            AddScopedAssets();
            var withClass = ScopedService(new FakeDeptScope { ScopedDepartmentId = 10, IncludesClassDepartmentAssets = true })
                .CreateBatch(new LabelPrintBatchCreateVm { AssetIds = new List<int> { 301, 501 } }, "user-a");
            CollectionAssert.AreEqual(new[] { 301, 501 }, withClass.Items.Select(i => i.AssetId).ToArray());

            var withoutClass = ScopedService(new FakeDeptScope { ScopedDepartmentId = 10 })
                .CreateBatch(new LabelPrintBatchCreateVm { AssetIds = new List<int> { 301, 501 } }, "user-a");
            CollectionAssert.AreEqual(new[] { 301 }, withoutClass.Items.Select(i => i.AssetId).ToArray());
        }

        [Test]
        public void Scope_UserWithNoDepartment_SeesNothing()
        {
            AddScopedAssets();
            var scope = new FakeDeptScope { ScopedDepartmentId = null };
            Assert.IsFalse(LabelPrintLogService.IsAssetInScope(scope, 10, false));
            Assert.Throws<BusinessException>(() => ScopedService(scope)
                .CreateBatch(new LabelPrintBatchCreateVm { AssetIds = new List<int> { 301 } }, "user-a"));
        }

        [Test]
        public void Scope_PrintCandidates_IsNotOrganizationWide()
        {
            var dir = TestContext.CurrentContext.TestDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "AssetManagementModule.sln")))
            {
                dir = Path.GetDirectoryName(dir);
            }

            Assert.IsNotNull(dir, "repo root");
            var controller = File.ReadAllText(Path.Combine(dir, "src", "AssetManagement.Web", "Controllers", "AssetScanController.cs"));
            var start = controller.IndexOf("public JsonResult PrintCandidates(", StringComparison.Ordinal);
            var end = controller.IndexOf("GetAssetListPage(", start, StringComparison.Ordinal);
            var body = controller.Substring(start, end - start);
            StringAssert.Contains("OrganizationWide = false", body);
            StringAssert.DoesNotContain("OrganizationWide = true", body);
        }

        private sealed class FakeDeptScope : IDepartmentScopeService
        {
            public bool BypassesDepartmentScope { get; set; }
            public bool IncludesClassDepartmentAssets { get; set; }
            public int? ScopedDepartmentId { get; set; }
            public IQueryable<AssetManagement.Domain.Entities.Asset> ApplyAssetScope(IQueryable<AssetManagement.Domain.Entities.Asset> query) { return query; }
            public IQueryable<AssetManagement.Domain.Entities.Department> ApplyDepartmentScope(IQueryable<AssetManagement.Domain.Entities.Department> query) { return query; }
            public void EnsureCanAccessAsset(AssetManagement.Domain.Entities.Asset asset) { }
            public void EnsureCanAccessDepartment(AssetManagement.Domain.Entities.Department department) { }
            public void EnsureCanAccessDepartmentId(int departmentId) { }
            public void EnsureCanCreateForRequisitionTarget(AssetManagement.Domain.Entities.Department department) { }
            public int CountVisibleDepartments(bool activeOnly = true) { return 0; }
        }

        // ---- history ----

        [Test]
        public void GetPrintHistory_MapsCountsLastPrintedAndEntriesInEat()
        {
            _store.History[101] = new AssetPrintHistory
            {
                PrintCount = 3,
                ReprintCount = 2,
                LastPrintedAtUtc = Now,
                Entries = new List<AssetPrintHistoryEntry>
                {
                    new AssetPrintHistoryEntry { Action = LabelPrintAuditActions.Reprint, IsReprint = true, TimestampUtc = Now, ActorUserId = "u1", ActorName = "Jane Doe", Details = "Tag=A-101" },
                    new AssetPrintHistoryEntry { Action = LabelPrintAuditActions.Print, IsReprint = false, TimestampUtc = Now.AddDays(-1), ActorUserId = "u2" }
                }
            };

            var vm = _service.GetPrintHistory(101, 20);
            Assert.AreEqual(3, vm.PrintCount);
            Assert.AreEqual(2, vm.ReprintCount);
            Assert.AreEqual("2026-10-22 10:30 EAT", vm.LastPrintedDisplay);
            Assert.AreEqual(2, vm.Entries.Count);
            Assert.AreEqual("Reprint", vm.Entries[0].ActionLabel);
            Assert.AreEqual("Jane Doe", vm.Entries[0].ActorName);
            Assert.AreEqual("u2", vm.Entries[1].ActorName, "falls back to the user id");
            Assert.AreEqual("2026-10-21 10:30 EAT", vm.Entries[1].WhenDisplay);
        }

        [Test]
        public void GetPrintHistory_NeverPrinted_IsEmpty()
        {
            var vm = _service.GetPrintHistory(102, 20);
            Assert.AreEqual(0, vm.PrintCount);
            Assert.IsNull(vm.LastPrintedDisplay);
            Assert.AreEqual(0, vm.Entries.Count);
        }

        // ---- context and plumbing ----

        [Test]
        public void EveryOperation_WithoutOrganizationContext_Throws()
        {
            var noOrg = new LabelPrintLogService(_store, _dispatcher, _audit, new FakeOrganizationScopeService(0), () => Now);
            Assert.Throws<BusinessException>(() => noOrg.RecordPrints(Request(101), "u"));
            Assert.Throws<BusinessException>(() => noOrg.CreateBatch(BatchRequest(101), "u"));
            Assert.Throws<BusinessException>(() => noOrg.CompleteBatch(1, false, "u"));
            Assert.Throws<BusinessException>(() => noOrg.GetBatch(1, "u", true));
            Assert.Throws<BusinessException>(() => noOrg.GetBatches(false, 10, "u", true));
            Assert.Throws<BusinessException>(() => noOrg.ClearBatch(1, "u", true));
            Assert.Throws<BusinessException>(() => noOrg.GetPrintHistory(101, 10));
            Assert.AreEqual(0, _dispatcher.Jobs.Count);
        }

        [Test]
        public void ProductionDispatcher_NeverSendsAnything()
        {
            var result = new ClientBrowserPrintDispatcher().Dispatch(new LabelPrintJob { AssetId = 1, AssetTag = "A-1" });
            Assert.IsFalse(result.SentByServer);
            Assert.AreEqual("Client", result.Channel);
            // No state besides the channel name constant: it cannot hold a socket, a printer name or an HTTP client.
            Assert.AreEqual(0, typeof(ClientBrowserPrintDispatcher).GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .Count(f => !f.IsLiteral));
        }

        [Test]
        public void DryRunDispatcher_RecordsJobs()
        {
            var dryRun = new DryRunLabelPrintDispatcher();
            var result = dryRun.Dispatch(new LabelPrintJob { AssetId = 7, AssetTag = "A-7", IsReprint = true });
            Assert.IsFalse(result.SentByServer);
            Assert.AreEqual("DryRun", result.Channel);
            Assert.AreEqual(7, dryRun.Jobs.Single().AssetId);
        }

        [Test]
        public void AuditLabels_AndAssetAuditTrailFilter_KnowThePrintActions()
        {
            Assert.IsTrue(AssetAuditTrailFilterHelper.IsBusinessEvent(LabelPrintAuditActions.Print), "prints show on the asset's Audit Trail tab");
            Assert.IsTrue(AssetAuditTrailFilterHelper.IsBusinessEvent(LabelPrintAuditActions.Reprint));
            Assert.AreNotEqual(LabelPrintAuditActions.Print, AuditDisplayLabelHelper.FormatAction(LabelPrintAuditActions.Print), "has a readable label");
            Assert.IsTrue(LabelPrintAuditActions.IsPrintAction("assets.labelprint"));
            Assert.IsFalse(LabelPrintAuditActions.IsPrintAction("Assets.Update"));
        }

        [Test]
        public void SourceLabel_IsReadable()
        {
            Assert.AreEqual("Asset register", LabelPrintLogService.SourceLabel("AssetRegister"));
            Assert.AreEqual("Reprint", LabelPrintLogService.SourceLabel("Reprint"));
            Assert.AreEqual("Bulk label print", LabelPrintLogService.SourceLabel("BulkPrint"));
        }

        // ---- controller surface (reflection: the test project has no System.Web.Mvc reference) ----

        [TestCase("RecordPrint")]
        [TestCase("CreateBatch")]
        [TestCase("CompleteBatch")]
        [TestCase("ClearBatch")]
        [TestCase("QueueFromRegister")]
        public void PrintLogPosts_ArePostOnly_AntiForgery_AndPermissionChecked(string actionName)
        {
            var method = typeof(AssetLabelsController).GetMethod(actionName);
            Assert.IsNotNull(method, actionName);
            foreach (var name in new[] { "HttpPostAttribute", "ValidateAntiForgeryTokenAttribute", "AuthorizeAttribute", "TenantAuthorizeAttribute", "PermissionAuthorizeAttribute" })
            {
                Assert.IsTrue(Has(method, name), actionName + " is missing " + name);
            }

            Assert.AreEqual("Assets.View", Permission(method));
        }

        [TestCase("Batches")]
        [TestCase("BatchItems")]
        public void PrintLogReads_AreGetOnly_AndPermissionChecked(string actionName)
        {
            var method = typeof(AssetLabelsController).GetMethod(actionName);
            Assert.IsNotNull(method, actionName);
            Assert.IsTrue(Has(method, "HttpGetAttribute"));
            Assert.IsTrue(Has(method, "TenantAuthorizeAttribute"));
            Assert.AreEqual("Assets.View", Permission(method));
        }

        // ---- printer safety: the new client code never talks to a printer itself ----

        [TestCase("label-print-log.js")]
        [TestCase("label-print-batch-log.js")]
        [TestCase("asset-group-selection.js")]
        public void NewScripts_NeverContactAPrinter(string file)
        {
            var path = FindWebFile(Path.Combine("Scripts", "app", file));
            if (path == null)
            {
                Assert.Ignore("Source tree not found next to the test binaries.");
            }

            var text = File.ReadAllText(path);
            foreach (var forbidden in new[] { "9100", "9101", "BrowserPrint", "window.print", ".send(", "printZplDirectly", "^XA" })
            {
                Assert.IsFalse(text.Contains(forbidden), file + " must not contain " + forbidden);
            }
        }

        private static string FindWebFile(string relative)
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "src", "AssetManagement.Web", relative);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }

            return null;
        }

        private static bool Has(MethodInfo method, string attributeName)
        {
            return method.GetCustomAttributesData().Any(a => a.Constructor.DeclaringType.Name == attributeName);
        }

        private static object Permission(MethodInfo method)
        {
            var attribute = method.GetCustomAttributesData().FirstOrDefault(a => a.Constructor.DeclaringType.Name == "PermissionAuthorizeAttribute");
            return attribute == null ? null : attribute.ConstructorArguments[0].Value;
        }

        private static LabelPrintRecordRequestVm Request(params int[] ids)
        {
            return new LabelPrintRecordRequestVm { AssetIds = ids.ToList() };
        }

        private static LabelPrintBatchCreateVm BatchRequest(params int[] ids)
        {
            return new LabelPrintBatchCreateVm { AssetIds = ids.ToList(), GroupLabel = "Test group" };
        }

        private sealed class AuditRow
        {
            public string Action;
            public string EntityType;
            public string EntityId;
            public string NewValues;
        }

        private sealed class RecordingAuditWriter : IAuditWriter
        {
            public readonly List<AuditRow> Rows = new List<AuditRow>();

            public void Write(string action, string entityType, string entityId, string oldValues, string newValues)
            {
                Rows.Add(new AuditRow { Action = action, EntityType = entityType, EntityId = entityId, NewValues = newValues });
            }
        }

        /// <summary>In-memory ILabelPrintBatchStore with the same tenant rules as the SQL store.</summary>
        private sealed class FakeLabelPrintBatchStore : ILabelPrintBatchStore
        {
            private readonly Dictionary<int, Tuple<int, LabelPrintAssetInfo, bool>> _assets = new Dictionary<int, Tuple<int, LabelPrintAssetInfo, bool>>();
            private readonly RecordingAuditWriter _audit; // prior prints come from the audit rows, like the SQL store
            private int _nextId = 1;

            public FakeLabelPrintBatchStore(RecordingAuditWriter audit)
            {
                _audit = audit;
            }

            public readonly Dictionary<int, LabelPrintBatchInfo> Batches = new Dictionary<int, LabelPrintBatchInfo>();
            public readonly Dictionary<int, List<LabelPrintBatchItemInfo>> Items = new Dictionary<int, List<LabelPrintBatchItemInfo>>();
            public readonly Dictionary<int, AssetPrintHistory> History = new Dictionary<int, AssetPrintHistory>();

            public void AddAsset(int org, int id, string tag, string name, bool isActive = true, int? departmentId = null, bool departmentIsActiveClass = false)
            {
                _assets[id] = Tuple.Create(org, new LabelPrintAssetInfo { AssetId = id, AssetTag = tag, AssetName = name, DepartmentId = departmentId, DepartmentIsActiveClass = departmentIsActiveClass }, isActive);
            }

            public void SetActive(int id, bool isActive)
            {
                _assets[id] = Tuple.Create(_assets[id].Item1, _assets[id].Item2, isActive);
            }

            public IList<LabelPrintAssetInfo> GetPrintableAssets(int organizationId, ICollection<int> assetIds)
            {
                return assetIds.Where(id => _assets.ContainsKey(id) && _assets[id].Item1 == organizationId && _assets[id].Item3)
                    .Select(id => _assets[id].Item2).ToList();
            }

            public IDictionary<int, int> GetPriorPrintCounts(int organizationId, ICollection<int> assetIds)
            {
                return _audit.Rows
                    .Where(r => r.EntityType == "Asset" && LabelPrintAuditActions.IsPrintAction(r.Action))
                    .Select(r => int.Parse(r.EntityId))
                    .Where(assetIds.Contains)
                    .GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
            }

            public int CreateBatch(LabelPrintBatchCreate batch)
            {
                var id = _nextId++;
                Batches[id] = new LabelPrintBatchInfo
                {
                    Id = id,
                    OrganizationId = batch.OrganizationId,
                    Source = batch.Source,
                    GroupLabel = batch.GroupLabel,
                    FilterDescription = batch.FilterDescription,
                    AssetCount = batch.Assets.Count,
                    Status = LabelPrintLogService.StatusQueued,
                    IsReprint = batch.IsReprint,
                    ReprintOfBatchId = batch.ReprintOfBatchId,
                    TemplateId = batch.TemplateId,
                    CodeType = batch.CodeType,
                    CreatedById = batch.CreatedById,
                    CreatedAtUtc = batch.CreatedAtUtc
                };
                Items[id] = batch.Assets.Select(a => new LabelPrintBatchItemInfo { AssetId = a.AssetId, AssetTag = a.AssetTag, AssetName = a.AssetName, DepartmentId = a.DepartmentId, DepartmentIsActiveClass = a.DepartmentIsActiveClass }).ToList();
                return id;
            }

            public LabelPrintBatchInfo GetBatch(int organizationId, int batchId)
            {
                LabelPrintBatchInfo batch;
                return Batches.TryGetValue(batchId, out batch) && batch.OrganizationId == organizationId ? batch : null;
            }

            public IList<LabelPrintBatchInfo> GetBatches(int organizationId, bool includeCleared, int take)
            {
                return Batches.Values.Where(b => b.OrganizationId == organizationId && (includeCleared || !b.IsCleared))
                    .OrderByDescending(b => b.Id).Take(take).ToList();
            }

            public IList<LabelPrintBatchItemInfo> GetBatchItems(int organizationId, int batchId)
            {
                if (GetBatch(organizationId, batchId) == null)
                {
                    return new List<LabelPrintBatchItemInfo>();
                }

                foreach (var item in Items[batchId])
                {
                    item.AssetIsActive = _assets.ContainsKey(item.AssetId) && _assets[item.AssetId].Item3 && _assets[item.AssetId].Item1 == organizationId;
                }

                return Items[batchId];
            }

            public void MarkItemPrinted(int organizationId, int batchId, int assetId, string assetTag, DateTime printedAtUtc)
            {
                var batch = GetBatch(organizationId, batchId);
                var item = Items[batchId].FirstOrDefault(i => i.AssetId == assetId);
                if (item == null)
                {
                    item = new LabelPrintBatchItemInfo { AssetId = assetId, AssetTag = assetTag };
                    Items[batchId].Add(item);
                }

                item.PrintCount++;
                item.LastPrintedAtUtc = printedAtUtc;
                batch.PrintedCount++;
                batch.AssetCount = Items[batchId].Count;
                batch.LastPrintedAtUtc = printedAtUtc;
                batch.Status = LabelPrintLogService.StatusPrinting;
            }

            public void CompleteBatch(int organizationId, int batchId, bool stoppedEarly, DateTime completedAtUtc)
            {
                var batch = GetBatch(organizationId, batchId);
                batch.Status = stoppedEarly ? LabelPrintLogService.StatusStopped : LabelPrintLogService.StatusCompleted;
                batch.CompletedAtUtc = completedAtUtc;
            }

            public bool ClearBatch(int organizationId, int batchId, string clearedById, DateTime clearedAtUtc)
            {
                var batch = GetBatch(organizationId, batchId);
                if (batch == null || batch.IsCleared)
                {
                    return false;
                }

                batch.IsCleared = true;
                batch.ClearedById = clearedById;
                batch.ClearedAtUtc = clearedAtUtc;
                return true;
            }

            public AssetPrintHistory GetPrintHistory(int organizationId, int assetId, int take)
            {
                AssetPrintHistory history;
                return History.TryGetValue(assetId, out history) ? history : new AssetPrintHistory();
            }
        }
    }
}

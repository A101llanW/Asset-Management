using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Services
{
    /// <summary>
    /// rel1022a: label print log. Every printed (or reprinted) label is one AuditLog row on the asset
    /// (Assets.LabelPrint / Assets.LabelReprint) with the user, the time and the tag; a group send is one LabelPrintBatch.
    /// Each label passes through <see cref="ILabelPrintDispatcher"/>; the production dispatcher does not contact any device
    /// (the browser prints through Zebra Browser Print as before).
    /// </summary>
    public class LabelPrintLogService : ILabelPrintLogService
    {
        public const string AuditActionPrint = LabelPrintAuditActions.Print;
        public const string AuditActionReprint = LabelPrintAuditActions.Reprint;
        public const string AuditActionBatchCreate = "LabelPrintBatch.Create";
        public const string AuditActionBatchReprint = "LabelPrintBatch.Reprint";
        public const string AuditActionBatchClear = "LabelPrintBatch.Clear";
        public const string AuditEntityAsset = "Asset";
        public const string AuditEntityBatch = "LabelPrintBatch";

        public const string SourceSingle = "Single";
        public const string SourceBulk = "Bulk";
        public const string SourceBulkPrint = "BulkPrint";
        public const string SourceAssetRegister = "AssetRegister";
        public const string SourceReprint = "Reprint";

        public const string ModeZebra = "Zebra";
        public const string ModeBrowserDialog = "BrowserDialog";

        public const string StatusQueued = "Queued";
        public const string StatusPrinting = "Printing";
        public const string StatusCompleted = "Completed";
        public const string StatusStopped = "Stopped";

        /// <summary>Most labels recorded in one call (the bulk page records one label at a time).</summary>
        public const int MaxRecordPerCall = 100;

        /// <summary>Most assets in one batch (a group send or a bulk queue).</summary>
        public const int MaxBatchAssets = 500;

        public const int MaxGroupLabelLength = 400;
        public const int MaxFilterLength = 2000;

        private readonly ILabelPrintBatchStore _store;
        private readonly ILabelPrintDispatcher _dispatcher;
        private readonly IAuditWriter _auditWriter;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly Func<DateTime> _utcNow;

        public LabelPrintLogService(
            ILabelPrintBatchStore store,
            ILabelPrintDispatcher dispatcher,
            IAuditWriter auditWriter,
            IOrganizationScopeService organizationScope)
            : this(store, dispatcher, auditWriter, organizationScope, null)
        {
        }

        public LabelPrintLogService(
            ILabelPrintBatchStore store,
            ILabelPrintDispatcher dispatcher,
            IAuditWriter auditWriter,
            IOrganizationScopeService organizationScope,
            Func<DateTime> utcNow)
        {
            if (store == null)
            {
                throw new ArgumentNullException("store");
            }

            if (dispatcher == null)
            {
                throw new ArgumentNullException("dispatcher");
            }

            _store = store;
            _dispatcher = dispatcher;
            _auditWriter = auditWriter;
            _organizationScope = organizationScope;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public LabelPrintRecordResultVm RecordPrints(LabelPrintRecordRequestVm request, string userId)
        {
            if (request == null)
            {
                throw new BusinessException("Print details are required.");
            }

            var organizationId = RequireOrganizationId();
            var ids = DistinctPositive(request.AssetIds);
            if (ids.Count == 0)
            {
                throw new BusinessException("Select at least one asset.");
            }

            if (ids.Count > MaxRecordPerCall)
            {
                throw new BusinessException("At most " + MaxRecordPerCall + " labels can be recorded at once.");
            }

            LabelPrintBatchInfo batch = null;
            if (request.BatchId.HasValue && request.BatchId.Value > 0)
            {
                batch = _store.GetBatch(organizationId, request.BatchId.Value);
                if (batch == null || batch.OrganizationId != organizationId)
                {
                    throw new BusinessException("The print batch was not found.");
                }
            }

            var assets = _store.GetPrintableAssets(organizationId, ids) ?? new List<LabelPrintAssetInfo>();
            var byId = assets.GroupBy(a => a.AssetId).ToDictionary(g => g.Key, g => g.First());
            var prior = _store.GetPriorPrintCounts(organizationId, ids) ?? new Dictionary<int, int>();
            var mode = NormalizeMode(request.Mode);
            var source = batch != null ? SourceBulk : NormalizeRecordSource(request.Source);
            var result = new LabelPrintRecordResultVm();
            foreach (var assetId in ids)
            {
                LabelPrintAssetInfo asset;
                if (!byId.TryGetValue(assetId, out asset))
                {
                    result.SkippedCount++;
                    continue;
                }

                int priorCount;
                prior.TryGetValue(assetId, out priorCount);
                var isReprint = request.Reprint || (batch != null && batch.IsReprint) || priorCount > 0;
                var now = _utcNow();
                var job = new LabelPrintJob
                {
                    OrganizationId = organizationId,
                    AssetId = assetId,
                    AssetTag = asset.AssetTag,
                    BatchId = batch == null ? (int?)null : batch.Id,
                    IsReprint = isReprint,
                    Mode = mode,
                    TemplateId = Trim(request.TemplateId, 100),
                    CodeType = Trim(request.CodeType, 20),
                    RequestedByUserId = userId,
                    RequestedAtUtc = now
                };
                var dispatch = _dispatcher.Dispatch(job) ?? new LabelPrintDispatchResult { Channel = "Unknown" };

                WriteAudit(
                    isReprint ? AuditActionReprint : AuditActionPrint,
                    AuditEntityAsset,
                    assetId.ToString(CultureInfo.InvariantCulture),
                    BuildPrintAuditDetails(job, source, dispatch.Channel, priorCount + 1));

                if (batch != null)
                {
                    _store.MarkItemPrinted(organizationId, batch.Id, assetId, asset.AssetTag, now);
                }

                result.Items.Add(new LabelPrintRecordedItemVm { AssetId = assetId, AssetTag = asset.AssetTag, IsReprint = isReprint });
                prior[assetId] = priorCount + 1;
            }

            return result;
        }

        public LabelPrintBatchVm CreateBatch(LabelPrintBatchCreateVm request, string userId)
        {
            if (request == null)
            {
                throw new BusinessException("Batch details are required.");
            }

            var organizationId = RequireOrganizationId();
            var ids = DistinctPositive(request.AssetIds);
            if (ids.Count == 0)
            {
                throw new BusinessException("Select at least one asset to send to printing.");
            }

            if (ids.Count > MaxBatchAssets)
            {
                throw new BusinessException("At most " + MaxBatchAssets + " assets can be sent to printing in one batch. Narrow the selection and try again.");
            }

            LabelPrintBatchInfo original = null;
            if (request.ReprintOfBatchId.HasValue && request.ReprintOfBatchId.Value > 0)
            {
                original = _store.GetBatch(organizationId, request.ReprintOfBatchId.Value);
                if (original == null || original.OrganizationId != organizationId)
                {
                    throw new BusinessException("The batch to reprint was not found.");
                }
            }

            var found = _store.GetPrintableAssets(organizationId, ids) ?? new List<LabelPrintAssetInfo>();
            var byId = found.GroupBy(a => a.AssetId).ToDictionary(g => g.Key, g => g.First());
            var ordered = ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
            if (ordered.Count == 0)
            {
                throw new BusinessException("None of the selected assets can be printed (they were not found or are inactive).");
            }

            var isReprint = original != null;
            var groupLabel = Trim(request.GroupLabel, MaxGroupLabelLength);
            if (isReprint)
            {
                groupLabel = Trim("Reprint of batch #" + original.Id + (string.IsNullOrWhiteSpace(original.GroupLabel) ? string.Empty : ": " + original.GroupLabel), MaxGroupLabelLength);
            }

            var now = _utcNow();
            var create = new LabelPrintBatchCreate
            {
                OrganizationId = organizationId,
                Source = isReprint ? SourceReprint : NormalizeBatchSource(request.Source),
                GroupLabel = groupLabel,
                FilterDescription = Trim(isReprint && string.IsNullOrWhiteSpace(request.FilterDescription) ? original.FilterDescription : request.FilterDescription, MaxFilterLength),
                IsReprint = isReprint,
                ReprintOfBatchId = isReprint ? (int?)original.Id : null,
                TemplateId = Trim(request.TemplateId, 100),
                CodeType = Trim(request.CodeType, 20),
                CreatedById = userId,
                CreatedAtUtc = now,
                Assets = ordered
            };
            var batchId = _store.CreateBatch(create);
            if (batchId <= 0)
            {
                throw new BusinessException("The print batch could not be saved.");
            }

            WriteAudit(
                isReprint ? AuditActionBatchReprint : AuditActionBatchCreate,
                AuditEntityBatch,
                batchId.ToString(CultureInfo.InvariantCulture),
                "Source=" + create.Source
                    + "; Assets=" + ordered.Count
                    + (isReprint ? "; ReprintOf=" + original.Id : string.Empty)
                    + (string.IsNullOrWhiteSpace(create.GroupLabel) ? string.Empty : "; Group=" + create.GroupLabel)
                    + (ordered.Count < ids.Count ? "; Skipped=" + (ids.Count - ordered.Count) : string.Empty));

            var info = _store.GetBatch(organizationId, batchId) ?? new LabelPrintBatchInfo
            {
                Id = batchId,
                OrganizationId = organizationId,
                Source = create.Source,
                GroupLabel = create.GroupLabel,
                FilterDescription = create.FilterDescription,
                AssetCount = ordered.Count,
                Status = StatusQueued,
                IsReprint = isReprint,
                ReprintOfBatchId = create.ReprintOfBatchId,
                CreatedById = userId,
                CreatedAtUtc = now
            };
            var vm = ToVm(info, userId, true);
            vm.Items = ordered.Select(a => new LabelPrintBatchItemVm { AssetId = a.AssetId, AssetTag = a.AssetTag, AssetName = a.AssetName }).ToList();
            return vm;
        }

        public void CompleteBatch(int batchId, bool stoppedEarly, string userId)
        {
            var organizationId = RequireOrganizationId();
            var batch = _store.GetBatch(organizationId, batchId);
            if (batch == null || batch.OrganizationId != organizationId)
            {
                throw new BusinessException("The print batch was not found.");
            }

            _store.CompleteBatch(organizationId, batchId, stoppedEarly, _utcNow());
        }

        public LabelPrintBatchVm GetBatch(int batchId, string userId, bool canManageAll)
        {
            var organizationId = RequireOrganizationId();
            var batch = _store.GetBatch(organizationId, batchId);
            if (batch == null || batch.OrganizationId != organizationId)
            {
                return null;
            }

            var vm = ToVm(batch, userId, canManageAll);
            vm.Items = (_store.GetBatchItems(organizationId, batchId) ?? new List<LabelPrintBatchItemInfo>())
                .Where(i => i.AssetIsActive)
                .Select(i => new LabelPrintBatchItemVm { AssetId = i.AssetId, AssetTag = i.AssetTag, AssetName = i.AssetName, PrintCount = i.PrintCount })
                .ToList();
            return vm;
        }

        public IList<LabelPrintBatchVm> GetBatches(bool includeCleared, int take, string userId, bool canManageAll)
        {
            var organizationId = RequireOrganizationId();
            var safeTake = take <= 0 ? 25 : Math.Min(take, 100);
            return (_store.GetBatches(organizationId, includeCleared, safeTake) ?? new List<LabelPrintBatchInfo>())
                .Where(b => b.OrganizationId == organizationId && (includeCleared || !b.IsCleared))
                .Select(b => ToVm(b, userId, canManageAll))
                .ToList();
        }

        public bool ClearBatch(int batchId, string userId, bool canManageAll)
        {
            var organizationId = RequireOrganizationId();
            var batch = _store.GetBatch(organizationId, batchId);
            if (batch == null || batch.OrganizationId != organizationId)
            {
                throw new BusinessException("The print batch was not found.");
            }

            if (!CanClear(batch, userId, canManageAll))
            {
                throw new BusinessException("Only the person who sent this batch, or a user who can edit assets, can clear it.");
            }

            if (batch.IsCleared)
            {
                return false;
            }

            var cleared = _store.ClearBatch(organizationId, batchId, userId, _utcNow());
            if (cleared)
            {
                // Soft-hide only: the batch items and every per-asset print audit row are kept.
                WriteAudit(AuditActionBatchClear, AuditEntityBatch, batchId.ToString(CultureInfo.InvariantCulture), "Cleared from the print batch log; per-asset print history kept.");
            }

            return cleared;
        }

        public AssetPrintHistoryVm GetPrintHistory(int assetId, int take)
        {
            var organizationId = RequireOrganizationId();
            var history = _store.GetPrintHistory(organizationId, assetId, take <= 0 ? 20 : Math.Min(take, 100)) ?? new AssetPrintHistory();
            return new AssetPrintHistoryVm
            {
                PrintCount = history.PrintCount,
                ReprintCount = history.ReprintCount,
                LastPrintedDisplay = history.LastPrintedAtUtc.HasValue ? FormatUtc(history.LastPrintedAtUtc.Value) : null,
                Entries = (history.Entries ?? new List<AssetPrintHistoryEntry>()).Select(e => new AssetPrintHistoryEntryVm
                {
                    WhenDisplay = FormatUtc(e.TimestampUtc),
                    ActionLabel = e.IsReprint ? "Reprint" : "Print",
                    IsReprint = e.IsReprint,
                    ActorName = string.IsNullOrWhiteSpace(e.ActorName) ? e.ActorUserId : e.ActorName,
                    Details = e.Details
                }).ToList()
            };
        }

        public static bool CanClear(LabelPrintBatchInfo batch, string userId, bool canManageAll)
        {
            if (batch == null)
            {
                return false;
            }

            return canManageAll
                || (!string.IsNullOrWhiteSpace(userId) && string.Equals(batch.CreatedById, userId, StringComparison.OrdinalIgnoreCase));
        }

        public static string BuildPrintAuditDetails(LabelPrintJob job, string source, string channel, int printNumber)
        {
            var parts = new List<string>
            {
                "Tag=" + (job.AssetTag ?? string.Empty),
                "Print=" + (job.IsReprint ? "Reprint" : "First print"),
                "Mode=" + (job.Mode ?? ModeZebra),
                "Source=" + (source ?? SourceSingle)
            };
            if (job.BatchId.HasValue)
            {
                parts.Add("Batch=" + job.BatchId.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrWhiteSpace(job.CodeType))
            {
                parts.Add("Code=" + job.CodeType);
            }

            if (!string.IsNullOrWhiteSpace(job.TemplateId))
            {
                parts.Add("Template=" + job.TemplateId);
            }

            parts.Add("Count=" + printNumber.ToString(CultureInfo.InvariantCulture));
            parts.Add("Dispatch=" + (channel ?? string.Empty));
            return string.Join("; ", parts);
        }

        public static string SourceLabel(string source)
        {
            switch (source)
            {
                case SourceAssetRegister:
                    return "Asset register";
                case SourceReprint:
                    return "Reprint";
                default:
                    return "Bulk label print";
            }
        }

        private LabelPrintBatchVm ToVm(LabelPrintBatchInfo b, string userId, bool canManageAll)
        {
            return new LabelPrintBatchVm
            {
                Id = b.Id,
                Source = b.Source,
                SourceLabel = SourceLabel(b.Source),
                GroupLabel = b.GroupLabel,
                FilterDescription = b.FilterDescription,
                AssetCount = b.AssetCount,
                PrintedCount = b.PrintedCount,
                Status = b.Status,
                IsReprint = b.IsReprint,
                ReprintOfBatchId = b.ReprintOfBatchId,
                CreatedById = b.CreatedById,
                CreatedByName = string.IsNullOrWhiteSpace(b.CreatedByName) ? b.CreatedById : b.CreatedByName,
                CreatedAtDisplay = FormatUtc(b.CreatedAtUtc),
                LastPrintedAtDisplay = b.LastPrintedAtUtc.HasValue ? FormatUtc(b.LastPrintedAtUtc.Value) : null,
                IsCleared = b.IsCleared,
                ClearedByName = string.IsNullOrWhiteSpace(b.ClearedByName) ? b.ClearedById : b.ClearedByName,
                ClearedAtDisplay = b.ClearedAtUtc.HasValue ? FormatUtc(b.ClearedAtUtc.Value) : null,
                CanClear = !b.IsCleared && CanClear(b, userId, canManageAll)
            };
        }

        private void WriteAudit(string action, string entityType, string entityId, string newValues)
        {
            if (_auditWriter != null)
            {
                _auditWriter.Write(action, entityType, entityId, null, newValues);
            }
        }

        private int RequireOrganizationId()
        {
            var organizationId = _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue || organizationId.Value <= 0)
            {
                throw new BusinessException("Label printing needs an organization context.");
            }

            return organizationId.Value;
        }

        private static List<int> DistinctPositive(IEnumerable<int> ids)
        {
            return (ids ?? Enumerable.Empty<int>()).Where(x => x > 0).Distinct().ToList();
        }

        private static string NormalizeMode(string mode)
        {
            return string.Equals(mode, ModeBrowserDialog, StringComparison.OrdinalIgnoreCase) ? ModeBrowserDialog : ModeZebra;
        }

        private static string NormalizeRecordSource(string source)
        {
            return string.Equals(source, SourceBulk, StringComparison.OrdinalIgnoreCase) ? SourceBulk : SourceSingle;
        }

        private static string NormalizeBatchSource(string source)
        {
            return string.Equals(source, SourceAssetRegister, StringComparison.OrdinalIgnoreCase) ? SourceAssetRegister : SourceBulkPrint;
        }

        private static string Trim(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= max ? trimmed : trimmed.Substring(0, max);
        }

        private static string FormatUtc(DateTime utc)
        {
            return KenyaTimeHelper.FormatUtc(utc, KenyaTimeHelper.ShortTimestampFormat) + " " + KenyaTimeHelper.DisplayAbbreviation;
        }
    }
}

using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    /// <summary>
    /// rel1022a: label print logging (per-asset audit trail), print batches (one row per group send) and the
    /// asset print history. Printing itself stays client-side (Zebra Browser Print); see <see cref="ILabelPrintDispatcher"/>.
    /// </summary>
    public interface ILabelPrintLogService
    {
        LabelPrintRecordResultVm RecordPrints(LabelPrintRecordRequestVm request, string userId);

        LabelPrintBatchVm CreateBatch(LabelPrintBatchCreateVm request, string userId);

        void CompleteBatch(int batchId, bool stoppedEarly, string userId);

        /// <summary>Batch with its asset list; null when it is not this tenant's.</summary>
        LabelPrintBatchVm GetBatch(int batchId, string userId, bool canManageAll);

        IList<LabelPrintBatchVm> GetBatches(bool includeCleared, int take, string userId, bool canManageAll);

        /// <summary>Soft-hide one batch entry. Only its creator or a user who can manage all batches (Assets.Edit) may clear it.</summary>
        bool ClearBatch(int batchId, string userId, bool canManageAll);

        AssetPrintHistoryVm GetPrintHistory(int assetId, int take);
    }
}

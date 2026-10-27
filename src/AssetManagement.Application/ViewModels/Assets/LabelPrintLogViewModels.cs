using System.Collections.Generic;

namespace AssetManagement.Application.ViewModels
{
    /// <summary>A label (or several) that the browser has just printed / sent to the print dialog.</summary>
    public class LabelPrintRecordRequestVm
    {
        public IList<int> AssetIds { get; set; } = new List<int>();
        public int? BatchId { get; set; }
        /// <summary>Single (Details / PrintLabel page) or Bulk (Bulk Label Print page).</summary>
        public string Source { get; set; }
        /// <summary>Zebra (Browser Print) or BrowserDialog.</summary>
        public string Mode { get; set; }
        public string TemplateId { get; set; }
        public string CodeType { get; set; }
        /// <summary>The user chose "Reprint" explicitly. Otherwise a print is a reprint when the asset was printed before.</summary>
        public bool Reprint { get; set; }
    }

    public class LabelPrintRecordedItemVm
    {
        public int AssetId { get; set; }
        public string AssetTag { get; set; }
        public bool IsReprint { get; set; }
    }

    public class LabelPrintRecordResultVm
    {
        public IList<LabelPrintRecordedItemVm> Items { get; set; } = new List<LabelPrintRecordedItemVm>();
        public int SkippedCount { get; set; }
    }

    public class LabelPrintBatchCreateVm
    {
        public IList<int> AssetIds { get; set; } = new List<int>();
        /// <summary>BulkPrint or AssetRegister (Reprint is set by the service when ReprintOfBatchId is given).</summary>
        public string Source { get; set; }
        public string GroupLabel { get; set; }
        public string FilterDescription { get; set; }
        public int? ReprintOfBatchId { get; set; }
        public string TemplateId { get; set; }
        public string CodeType { get; set; }
    }

    public class LabelPrintBatchItemVm
    {
        public int AssetId { get; set; }
        public string AssetTag { get; set; }
        public string AssetName { get; set; }
        public int PrintCount { get; set; }
    }

    public class LabelPrintBatchVm
    {
        public int Id { get; set; }
        public string Source { get; set; }
        public string SourceLabel { get; set; }
        public string GroupLabel { get; set; }
        public string FilterDescription { get; set; }
        public int AssetCount { get; set; }
        public int PrintedCount { get; set; }
        public string Status { get; set; }
        public bool IsReprint { get; set; }
        public int? ReprintOfBatchId { get; set; }
        public string CreatedById { get; set; }
        public string CreatedByName { get; set; }
        public string CreatedAtDisplay { get; set; }
        public string LastPrintedAtDisplay { get; set; }
        public bool IsCleared { get; set; }
        public string ClearedByName { get; set; }
        public string ClearedAtDisplay { get; set; }
        public bool CanClear { get; set; }
        public IList<LabelPrintBatchItemVm> Items { get; set; } = new List<LabelPrintBatchItemVm>();
    }

    public class AssetPrintHistoryEntryVm
    {
        public string WhenDisplay { get; set; }
        public string ActionLabel { get; set; }
        public bool IsReprint { get; set; }
        public string ActorName { get; set; }
        public string Details { get; set; }
    }

    public class AssetPrintHistoryVm
    {
        public int PrintCount { get; set; }
        public int ReprintCount { get; set; }
        public string LastPrintedDisplay { get; set; }
        public IList<AssetPrintHistoryEntryVm> Entries { get; set; } = new List<AssetPrintHistoryEntryVm>();
    }
}

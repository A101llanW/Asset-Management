using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Application.ViewModels
{
    public class BatchAssignItemVm
    {
        public int AssetId { get; set; }

        [StringLength(128)]
        public string ToUserId { get; set; }
    }

    public class BatchAssignRequestVm
    {
        public IList<BatchAssignItemVm> Items { get; set; }

        public int? ToDepartmentId { get; set; }

        [StringLength(128)]
        public string HandedOverById { get; set; }

        [StringLength(1000)]
        public string HandoverNotes { get; set; }
    }

    public class BatchAssignRowResultVm
    {
        public int AssetId { get; set; }

        public string AssetTag { get; set; }

        public bool Success { get; set; }

        public string Message { get; set; }
    }

    public class BatchAssignResultVm
    {
        public int ProcessedCount { get; set; }

        public int SkippedCount { get; set; }

        public IList<BatchAssignRowResultVm> Rows { get; set; }
    }

    public class BatchAssignRowVm
    {
        public int AssetId { get; set; }

        public string AssetTag { get; set; }

        public string SerialNumber { get; set; }

        public string AssetName { get; set; }

        [Display(Name = "Custodian")]
        [StringLength(128)]
        public string ToUserId { get; set; }

        public bool CanAssign { get; set; }

        public string BlockReason { get; set; }
    }

    public class BatchAssignPageVm
    {
        public IList<BatchAssignRowVm> Rows { get; set; }

        [Display(Name = "Department")]
        public int? ToDepartmentId { get; set; }

        [Display(Name = "Handover Notes")]
        [StringLength(1000)]
        public string HandoverNotes { get; set; }

        public string ReturnUrl { get; set; }

        public BatchAssignResultVm LastResult { get; set; }
    }
}

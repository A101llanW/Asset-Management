using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AssetManagement.Application.Helpers;

namespace AssetManagement.Application.ViewModels
{
    /// <summary>rel1009i: the receiving screen for one purchase order (all of its lines).</summary>
    public class ReceiveOrderVm
    {
        public int PurchaseRecordId { get; set; }

        public string PurchaseOrderNumber { get; set; }

        public string SupplierName { get; set; }

        public int? PurchaseRequestId { get; set; }

        public string PurchaseRequestNumber { get; set; }

        public int? RequisitionDepartmentId { get; set; }

        public string RequisitionDepartmentName { get; set; }

        public bool IsLegacy { get; set; }

        public string Status { get; set; }

        public int Ordered { get; set; }

        public int Received { get; set; }

        public int Outstanding { get; set; }

        /// <summary>Quantity on the linked requisition (0 when not linked).</summary>
        public int RequisitionQuantity { get; set; }

        /// <summary>Quantity on all POs of the linked requisition.</summary>
        public int RequisitionOrderedQuantity { get; set; }

        /// <summary>The linked requisition has more on order than was requested (ordered &gt; requested).</summary>
        public bool IsRequisitionOverOrdered
        {
            get { return PurchaseRequestId.HasValue && PurchaseOrderQuantityRules.IsOverOrdered(RequisitionQuantity, RequisitionOrderedQuantity); }
        }

        [Required]
        public DateTime ReceivedDate { get; set; }

        [StringLength(1000)]
        public string Notes { get; set; }

        public IList<ReceiveOrderLineVm> Lines { get; set; } = new List<ReceiveOrderLineVm>();
    }

    public class ReceiveOrderLineVm
    {
        public const string DestinationStore = "Store";
        public const string DestinationDepartment = "Department";
        public const string TagModeGenerate = "Generate";
        public const string TagModeEnter = "Enter";

        /// <summary>0 = the single virtual line of a legacy PO.</summary>
        public int PurchaseOrderLineId { get; set; }

        public int LineNumber { get; set; }

        public string Description { get; set; }

        public int Ordered { get; set; }

        public int Received { get; set; }

        public int Outstanding { get; set; }

        public decimal UnitCost { get; set; }

        public string AssetCategoryName { get; set; }

        public int? AssetTypeId { get; set; }

        public string AssetTypeName { get; set; }

        public int? AssetSubTypeId { get; set; }

        public string AssetSubTypeName { get; set; }

        public string ClassificationSourceLabel { get; set; }

        /// <summary>No source has a classification: the receiver must choose the type (override).</summary>
        public bool NeedsClassification { get; set; }

        /// <summary>A source set the type but not the sub-type, and the type has sub-types: the receiver must choose one.</summary>
        public bool NeedsSubType { get; set; }

        /// <summary>A source set the type, which has no active sub-types defined: received without one.</summary>
        public bool TypeHasNoSubTypes { get; set; }

        // ---- posted per line ----

        public int QuantityReceived { get; set; }

        /// <summary>Used ONLY when NeedsClassification; ignored server-side when a source has the classification.</summary>
        public int? OverrideAssetTypeId { get; set; }

        public int? OverrideAssetSubTypeId { get; set; }

        /// <summary>Bulk paste: one serial per line (blank = no serials).</summary>
        public string SerialNumbersText { get; set; }

        public string TagMode { get; set; }

        /// <summary>Bulk paste of asset tags when TagMode = Enter (one per unit).</summary>
        public string AssetTagsText { get; set; }

        public string Condition { get; set; }

        public string DestinationKind { get; set; }

        public int? DestinationDepartmentId { get; set; }
    }
}

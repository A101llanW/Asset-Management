using System;
using System.Collections.Generic;

namespace AssetManagement.Application.ViewModels
{
    /// <summary>rel1009i: a PO line on Create, prefilled from a requisition line.</summary>
    public class PurchaseOrderLineInputVm
    {
        /// <summary>NULL for a single-item requisition (no line items).</summary>
        public int? PurchaseRequestLineId { get; set; }

        public int LineNumber { get; set; }

        /// <summary>Migration 108: the requisition line's item / asset name (copied to the PO line; never taken from the post).</summary>
        public string ItemName { get; set; }

        public string Description { get; set; }

        public int RequisitionQuantity { get; set; }

        public int AlreadyOrdered { get; set; }

        public int Outstanding { get; set; }

        public int Quantity { get; set; }

        public decimal UnitCost { get; set; }

        public int? AssetTypeId { get; set; }

        public int? AssetSubTypeId { get; set; }

        public string ClassificationLabel { get; set; }

        /// <summary>True when the requisition (the line's tagged asset or type / sub-type) fixes the classification (not editable on the PO).</summary>
        public bool ClassificationLocked { get; set; }
    }

    public class PurchaseRequisitionOptionVm
    {
        public int Id { get; set; }

        public string RequestNumber { get; set; }

        public string DepartmentName { get; set; }

        public string ItemDescription { get; set; }

        public int RequisitionQuantity { get; set; }

        public int Outstanding { get; set; }

        public string Label { get; set; }
    }

    public class PurchaseOrderLineStatusVm
    {
        /// <summary>0 for the single virtual line of a legacy PO (no PurchaseOrderLine rows).</summary>
        public int Id { get; set; }

        public int? PurchaseRequestLineId { get; set; }

        public int LineNumber { get; set; }

        /// <summary>Migration 108: default name for the received asset (PO line, then requisition line); null on older rows.</summary>
        public string ItemName { get; set; }

        public string Description { get; set; }

        public int Ordered { get; set; }

        public int Received { get; set; }

        public int Outstanding { get; set; }

        public decimal UnitCost { get; set; }

        public int? AssetCategoryId { get; set; }

        public string AssetCategoryName { get; set; }

        public int? AssetTypeId { get; set; }

        public string AssetTypeName { get; set; }

        public int? AssetSubTypeId { get; set; }

        public string AssetSubTypeName { get; set; }

        public string Brand { get; set; }

        public string Model { get; set; }

        /// <summary>PO line, requisition asset, supplier catalog; NULL when no source has a classification.</summary>
        public string ClassificationSource { get; set; }

        public bool HasClassification
        {
            get { return AssetTypeId.HasValue && AssetTypeId.Value > 0; }
        }
    }

    public class PurchaseOrderStatusVm
    {
        public int PurchaseRecordId { get; set; }

        public int? PurchaseRequestId { get; set; }

        public string PurchaseRequestNumber { get; set; }

        public bool IsLinked
        {
            get { return PurchaseRequestId.HasValue; }
        }

        /// <summary>True when the PO has no PurchaseOrderLine rows (created before rel1009i).</summary>
        public bool IsLegacy { get; set; }

        public int Ordered { get; set; }

        public int Received { get; set; }

        public int Outstanding { get; set; }

        public string Status { get; set; }

        public IList<PurchaseOrderLineStatusVm> Lines { get; set; } = new List<PurchaseOrderLineStatusVm>();
    }

    public class RequisitionLinkedOrderVm
    {
        public int PurchaseRecordId { get; set; }

        public string PurchaseOrderNumber { get; set; }

        public string SupplierName { get; set; }

        public DateTime PurchaseDate { get; set; }

        public int Quantity { get; set; }

        public int Received { get; set; }

        public string Status { get; set; }
    }

    public class RequisitionOrderSummaryVm
    {
        public int PurchaseRequestId { get; set; }

        public int RequisitionQuantity { get; set; }

        public int OrderedQuantity { get; set; }

        public int ReceivedQuantity { get; set; }

        public int OutstandingToOrder { get; set; }

        public string FulfilmentStatus { get; set; }

        /// <summary>More is on order than the requisition asked for (ordered &gt; requested).</summary>
        public bool IsOverOrdered
        {
            get { return AssetManagement.Application.Helpers.PurchaseOrderQuantityRules.IsOverOrdered(RequisitionQuantity, OrderedQuantity); }
        }

        public int OverOrderedBy
        {
            get { return IsOverOrdered ? OrderedQuantity - RequisitionQuantity : 0; }
        }

        public IList<RequisitionLinkedOrderVm> Orders { get; set; } = new List<RequisitionLinkedOrderVm>();
    }
}

namespace AssetManagement.Application.ViewModels
{
    /// <summary>rel1009i: an asset type or sub-type option (GroupName = category or type name; ParentId = type id for a sub-type).</summary>
    public class AssetClassOptionVm
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string GroupName { get; set; }

        public int? ParentId { get; set; }
    }
}

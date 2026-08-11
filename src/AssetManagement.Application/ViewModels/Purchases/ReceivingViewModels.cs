using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Application.ViewModels
{
    public class AssetReceivingListVm
    {
        public int Id { get; set; }

        public int AssetId { get; set; }

        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public DateTime ReceivedDate { get; set; }

        public int QuantityReceived { get; set; }

        public string ConditionOnReceipt { get; set; }

        public string ReceivedById { get; set; }

        public string Notes { get; set; }
    }

    public class PurchaseReceiveDetailVm
    {
        public int PurchaseRecordId { get; set; }

        public string PurchaseOrderNumber { get; set; }

        public string SupplierName { get; set; }

        public string ItemDescription { get; set; }

        public int PurchaseQuantity { get; set; }

        public int QuantityReceived { get; set; }

        public int RemainingQuantity { get; set; }

        public int? SuggestedAssetId { get; set; }

        public int? AssetSubTypeId { get; set; }

        public string AssetSubTypeName { get; set; }

        public bool RequiresSubTypeAssignment { get; set; }

        public bool RequiresCatalogMatchConfirmation { get; set; }

        public int? CatalogMatchAssetId { get; set; }

        public string CatalogMatchLabel { get; set; }

        public string CatalogMatchItemName { get; set; }

        public int? ContextCategoryId { get; set; }

        public int? ContextAssetTypeId { get; set; }

        public string ContextBrand { get; set; }

        public string ContextModel { get; set; }

        public string SuggestedAssetName { get; set; }

        public decimal UnitCost { get; set; }

        public string Currency { get; set; }

        public int? ContextDepartmentId { get; set; }

        public int? RequisitionDepartmentId { get; set; }

        public string RequisitionDepartmentName { get; set; }

        public IList<AssetReceivingListVm> Receivings { get; set; } = new List<AssetReceivingListVm>();
    }

    public class ReceiveAssetOptionVm
    {
        public int Id { get; set; }

        public string Label { get; set; }
    }

    public class ReceiveAssetLookupVm
    {
        public IList<ReceiveAssetOptionVm> Assets { get; set; } = new List<ReceiveAssetOptionVm>();

        public int? SelectedAssetId { get; set; }
    }

    public class ReceiveAssetUnitVm
    {
        [StringLength(120)]
        public string SerialNumber { get; set; }
    }

    public class ReceiveCreatedAssetVm
    {
        public int AssetId { get; set; }

        public string AssetTag { get; set; }

        public string SerialNumber { get; set; }
    }

    public class ReceiveResultVm
    {
        public int ReceivingId { get; set; }

        public IList<ReceiveCreatedAssetVm> CreatedAssets { get; set; } = new List<ReceiveCreatedAssetVm>();
    }

    public class AssetReceiveVm
    {
        public int PurchaseRecordId { get; set; }

        public int AssetId { get; set; }

        public int? AssetSubTypeId { get; set; }

        public bool CatalogMatchConfirmed { get; set; }

        /// <summary>When true, received stock/assets are placed in the requisition department pool instead of company custody.</summary>
        public bool AssignToRequisitionDepartment { get; set; }

        /// <summary>RequisitionDepartment | CompanyCustody — required when a requisition department is linked.</summary>
        public string ReceivePlacementChoice { get; set; }

        [Required]
        public DateTime ReceivedDate { get; set; }

        [StringLength(200)]
        public string ConditionOnReceipt { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Quantity received must be at least 1.")]
        public int QuantityReceived { get; set; }

        [StringLength(1000)]
        public string Notes { get; set; }

        public IList<ReceiveAssetUnitVm> NewAssetUnits { get; set; } = new List<ReceiveAssetUnitVm>();
    }
}

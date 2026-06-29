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

    public class AssetReceiveVm
    {
        public int PurchaseRecordId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Asset is required.")]
        public int AssetId { get; set; }

        [Required]
        public DateTime ReceivedDate { get; set; }

        [StringLength(200)]
        public string ConditionOnReceipt { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Quantity received must be at least 1.")]
        public int QuantityReceived { get; set; }

        [StringLength(1000)]
        public string Notes { get; set; }
    }
}

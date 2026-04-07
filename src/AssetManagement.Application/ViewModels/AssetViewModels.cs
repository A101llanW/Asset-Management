using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.ViewModels
{
    public class AssetFilterVm
    {
        public string Search { get; set; }

        public int? DepartmentId { get; set; }

        public int? CategoryId { get; set; }

        public AssetStatus? Status { get; set; }
    }

    public class AssetListVm
    {
        public int Id { get; set; }

        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public string CategoryName { get; set; }

        public string DepartmentName { get; set; }

        public string CurrentCustodianId { get; set; }

        public AssetStatus CurrentStatus { get; set; }

        public decimal CurrentBookValue { get; set; }
    }

    public class AssetCreateVm
    {
        [Required]
        [StringLength(150)]
        public string AssetName { get; set; }

        [Required]
        [StringLength(60)]
        public string AssetTag { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [Required]
        public int AssetTypeId { get; set; }

        [Required]
        [StringLength(120)]
        public string Brand { get; set; }

        [Required]
        [StringLength(120)]
        public string Model { get; set; }

        [StringLength(120)]
        public string SerialNumber { get; set; }

        public string Description { get; set; }

        [Required]
        public DateTime PurchaseDate { get; set; }

        [Required]
        [Range(0.01, 999999999)]
        public decimal AcquisitionCost { get; set; }

        public decimal TaxAmount { get; set; }

        [Required]
        [StringLength(10)]
        public string Currency { get; set; }

        [Required]
        public int SupplierId { get; set; }

        [Required]
        public int DepartmentId { get; set; }

        public string ConditionOnReceipt { get; set; }

        public int UsefulLifeMonths { get; set; }

        public decimal SalvageValue { get; set; }

        public DepreciationMethod DepreciationMethod { get; set; }

        public DateTime DepreciationStartDate { get; set; }

        public decimal ReplacementValue { get; set; }

        public bool IsInsured { get; set; }

        public decimal? InsuredValue { get; set; }

        public DateTime? WarrantyStartDate { get; set; }

        public DateTime? WarrantyEndDate { get; set; }

        public AssetStatus CurrentStatus { get; set; }
    }

    public class AssetEditVm : AssetCreateVm
    {
        [Required]
        public int Id { get; set; }
    }

    public class AssetDetailsVm
    {
        public int Id { get; set; }

        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public string SerialNumber { get; set; }

        public string Brand { get; set; }

        public string Model { get; set; }

        public string DepartmentName { get; set; }

        public string CategoryName { get; set; }

        public string SupplierName { get; set; }

        public AssetStatus CurrentStatus { get; set; }

        public decimal AcquisitionCost { get; set; }

        public decimal CurrentBookValue { get; set; }

        public decimal AccumulatedDepreciation { get; set; }

        public decimal ReplacementValue { get; set; }

        public string PolicyReference { get; set; }

        public DateTime? WarrantyEndDate { get; set; }

        public IEnumerable<AssetCustodyTimelineVm> CustodyHistory { get; set; } = new List<AssetCustodyTimelineVm>();
    }

    public class AssetCustodyTimelineVm
    {
        public DateTime ActionDate { get; set; }

        public string ActionType { get; set; }

        public string FromEntity { get; set; }

        public string ToEntity { get; set; }

        public string ConditionBefore { get; set; }

        public string ConditionAfter { get; set; }

        public string Reason { get; set; }

        public string ApprovedById { get; set; }

        public string Notes { get; set; }
    }

    public class AssetDisposalRequestVm
    {
        [Required]
        public int AssetId { get; set; }

        [Required]
        [StringLength(500)]
        public string DisposalReason { get; set; }

        [Required]
        public DisposalMethod DisposalMethod { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }
    }

    public class AssetDisposalApprovalVm
    {
        [Required]
        public int AssetId { get; set; }

        [Range(0, 999999999)]
        public decimal? DisposalAmount { get; set; }

        public DateTime? DisposalDate { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }
    }
}

using System.Collections.Generic;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.ViewModels
{
    public class AssetScanLookupVm
    {
        public bool Found { get; set; }

        public string Message { get; set; }

        public int? AssetId { get; set; }

        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public string DepartmentName { get; set; }

        public AssetStatus CurrentStatus { get; set; }

        public string BarcodeOrQRCode { get; set; }

        public string SerialNumber { get; set; }

        public string Brand { get; set; }

        public string Model { get; set; }

        public string CategoryName { get; set; }

        public string CustodianName { get; set; }

        public string CustodianUserId { get; set; }

        public string CustodianPersonName { get; set; }

        public string CustodianEmail { get; set; }

        public string AssetTypeName { get; set; }

        public string AssetSubTypeName { get; set; }

        public AssetCondition? Condition { get; set; }

        public System.DateTime? PurchaseDate { get; set; }

        public System.DateTime? ReceivedDate { get; set; }

        public int? DepartmentId { get; set; }

        public DepartmentSection? Section { get; set; }

        public bool OrganizationUsesPrep { get; set; }

        public string PhotoFilePath { get; set; }

        public string PhotoContentType { get; set; }

        public decimal? AcquisitionCost { get; set; }

        public string Currency { get; set; }

        public string SupplierName { get; set; }

        public IList<AssetManagement.Application.DTOs.AssetScanLocationLevel> LocationPath { get; set; }
    }

    /// <summary>Who is looking at a scan result (rel1027). Anonymous = signed out.</summary>
    public class AssetScanViewer
    {
        public bool IsSignedIn { get; set; }

        /// <summary>Assets.View: may open Asset Details and see the custodian's e-mail when no name is set.</summary>
        public bool CanViewAssetDetails { get; set; }

        /// <summary>Financials.View (signed in): acquisition cost.</summary>
        public bool CanViewFinancials { get; set; }

        /// <summary>Suppliers.View (signed in): supplier name.</summary>
        public bool CanViewSuppliers { get; set; }

        public static readonly AssetScanViewer Anonymous = new AssetScanViewer();
    }

    public class AssetScanCardRow
    {
        public AssetScanCardRow(string key, string label, string value)
        {
            Key = key;
            Label = label;
            Value = value;
        }

        public string Key { get; private set; }

        public string Label { get; private set; }

        public string Value { get; private set; }
    }

    /// <summary>What the scan page shows for one asset, already filtered for the viewer.</summary>
    public class AssetScanCardVm
    {
        public string AssetName { get; set; }

        public string AssetTag { get; set; }

        public string StatusText { get; set; }

        public string ConditionText { get; set; }

        public string CustodianText { get; set; }

        public bool HasPhoto { get; set; }

        public IList<AssetScanCardRow> Rows { get; set; } = new List<AssetScanCardRow>();

        /// <summary>Only filled for signed-in users with Financials.View / Suppliers.View.</summary>
        public IList<AssetScanCardRow> RestrictedRows { get; set; } = new List<AssetScanCardRow>();
    }

    public class AssetQuickActionsVm
    {
        public int AssetId { get; set; }

        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public AssetStatus CurrentStatus { get; set; }

        public string DepartmentName { get; set; }

        public bool CanAssign { get; set; }

        public bool CanTransfer { get; set; }

        public bool CanReturn { get; set; }

        public bool CanReportIncident { get; set; }

        public bool CanViewAssetDetails { get; set; }

        public IList<string> ActionUrls { get; set; }
    }

    public class AssetTcoVm
    {
        public int AssetId { get; set; }

        /// <summary>Null = "Not set"; excluded from TotalCostOfOwnership.</summary>
        public decimal? AcquisitionCost { get; set; }

        public decimal TaxAmount { get; set; }

        public decimal MaintenanceTotal { get; set; }

        public decimal InsuranceExposure { get; set; }

        public decimal TotalCostOfOwnership { get; set; }
    }
}

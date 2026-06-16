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

        public IList<string> ActionUrls { get; set; }
    }

    public class AssetTcoVm
    {
        public int AssetId { get; set; }

        public decimal AcquisitionCost { get; set; }

        public decimal TaxAmount { get; set; }

        public decimal MaintenanceTotal { get; set; }

        public decimal InsuranceExposure { get; set; }

        public decimal TotalCostOfOwnership { get; set; }
    }
}

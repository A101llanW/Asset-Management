using System;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.ViewModels
{
    public class AssetExportRowVm
    {
        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public AssetStatus CurrentStatus { get; set; }

        public string CategoryName { get; set; }

        public string DepartmentName { get; set; }

        public string CurrentCustodianId { get; set; }

        public decimal AcquisitionCost { get; set; }

        public DateTime PurchaseDate { get; set; }

        public string SerialNumber { get; set; }
    }

    public class AssetExportResultVm
    {
        public int RowCount { get; set; }

        public bool Truncated { get; set; }

        public string WarningMessage { get; set; }
    }
}

using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.DTOs
{
    public class AssetScanLookupResult
    {
        public int Id { get; set; }

        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public AssetStatus CurrentStatus { get; set; }

        public string BarcodeOrQRCode { get; set; }

        public string DepartmentName { get; set; }

        public string SerialNumber { get; set; }

        public string Brand { get; set; }

        public string Model { get; set; }

        public string CategoryName { get; set; }

        public string CustodianName { get; set; }
    }
}

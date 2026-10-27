using System;
using System.Collections.Generic;
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

        // rel1027 scan page: richer card. Sensitive fields (email, cost, supplier) are filtered per viewer by AssetScanCardBuilder.
        public string CustodianUserId { get; set; }

        /// <summary>First + last name only (never the e-mail).</summary>
        public string CustodianPersonName { get; set; }

        public string CustodianEmail { get; set; }

        public string AssetTypeName { get; set; }

        public string AssetSubTypeName { get; set; }

        public AssetCondition? Condition { get; set; }

        public DateTime? PurchaseDate { get; set; }

        /// <summary>First goods-receiving date for the asset, if it came in through Receiving.</summary>
        public DateTime? ReceivedDate { get; set; }

        public int? DepartmentId { get; set; }

        public DepartmentSection? Section { get; set; }

        /// <summary>True when the organisation has at least one active Prep unit (the Section row is shown only then).</summary>
        public bool OrganizationUsesPrep { get; set; }

        public string PhotoFilePath { get; set; }

        public string PhotoContentType { get; set; }

        public string PhotoDocumentType { get; set; }

        public decimal? AcquisitionCost { get; set; }

        public string Currency { get; set; }

        public string SupplierName { get; set; }

        /// <summary>The asset's unit and its parents, top level first (department ... room).</summary>
        public List<AssetScanLocationLevel> LocationPath { get; set; } = new List<AssetScanLocationLevel>();
    }

    public class AssetScanLocationLevel
    {
        public string Name { get; set; }

        public DepartmentKind Kind { get; set; }
    }
}

using System;
using System.Collections.Generic;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Domain.Entities
{
    public class Asset : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public string AssetName { get; set; }

        public string AssetTag { get; set; }

        public int CategoryId { get; set; }

        public int AssetTypeId { get; set; }

        public string Brand { get; set; }

        public string Model { get; set; }

        public string SerialNumber { get; set; }

        public string BarcodeOrQRCode { get; set; }

        public string Specifications { get; set; }

        public AssetCondition Condition { get; set; }

        public AssetStatus CurrentStatus { get; set; }

        public string Description { get; set; }

        public DateTime PurchaseDate { get; set; }

        public decimal AcquisitionCost { get; set; }

        public decimal TaxAmount { get; set; }

        public string Currency { get; set; }

        public int? SupplierId { get; set; }

        public int? DepartmentId { get; set; }

        public string CurrentCustodianId { get; set; }

        public string ConditionOnReceipt { get; set; }

        public int? UsefulLifeMonths { get; set; }

        public decimal SalvageValue { get; set; }

        public DepreciationMethod DepreciationMethod { get; set; }

        public DateTime DepreciationStartDate { get; set; }

        public decimal CurrentBookValue { get; set; }

        public decimal AccumulatedDepreciation { get; set; }

        public string ImpairmentNotes { get; set; }

        public DateTime? WarrantyStartDate { get; set; }

        public DateTime? WarrantyEndDate { get; set; }

        public string PolicyReference { get; set; }

        public decimal? InsuredValue { get; set; }

        public string ImagePath { get; set; }

        public bool IsLeased { get; set; }

        public bool IsInsured { get; set; }

        public bool RequireTransferApproval { get; set; }

        public string TransferApprovalStageRoleIds { get; set; }

        public string TransferApprovalStageUserIds { get; set; }

        public bool RequireDisposalApproval { get; set; }

        public string DisposalApprovalStageRoleIds { get; set; }

        public string DisposalApprovalStageUserIds { get; set; }

        public byte[] RowVersion { get; set; }

        public virtual AssetCategory Category { get; set; }

        public virtual AssetType AssetType { get; set; }

        public virtual Supplier Supplier { get; set; }

        public virtual Department Department { get; set; }

        public virtual ICollection<AssetDocument> Documents { get; set; } = new HashSet<AssetDocument>();

        public virtual ICollection<AssetAssignment> Assignments { get; set; } = new HashSet<AssetAssignment>();

        public virtual ICollection<AssetTransfer> Transfers { get; set; } = new HashSet<AssetTransfer>();

        public virtual ICollection<AssetReturn> Returns { get; set; } = new HashSet<AssetReturn>();

        public virtual ICollection<AssetMaintenanceRecord> MaintenanceRecords { get; set; } = new HashSet<AssetMaintenanceRecord>();

        public virtual ICollection<AssetIncident> Incidents { get; set; } = new HashSet<AssetIncident>();

        public virtual ICollection<InsurancePolicy> InsurancePolicies { get; set; } = new HashSet<InsurancePolicy>();

        public virtual ICollection<InsuranceClaim> InsuranceClaims { get; set; } = new HashSet<InsuranceClaim>();

        public virtual ICollection<DepreciationRecord> DepreciationRecords { get; set; } = new HashSet<DepreciationRecord>();

        public virtual ICollection<DisposalRecord> DisposalRecords { get; set; } = new HashSet<DisposalRecord>();

        public virtual ICollection<AssetCustodyEvent> CustodyEvents { get; set; } = new HashSet<AssetCustodyEvent>();
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.ViewModels
{
    public class MaintenanceRecordListVm
    {
        public int Id { get; set; }

        public int AssetId { get; set; }

        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public string MaintenanceTicketNumber { get; set; }

        public string MaintenanceType { get; set; }

        public string ReportedIssue { get; set; }

        public MaintenanceStatus Status { get; set; }

        public DateTime ServiceDate { get; set; }

        public DateTime? CompletionDate { get; set; }

        public string Outcome { get; set; }
    }

    public class MaintenanceDetailsVm
    {
        public int Id { get; set; }

        public int AssetId { get; set; }

        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public string MaintenanceTicketNumber { get; set; }

        public string MaintenanceType { get; set; }

        public string ReportedIssue { get; set; }

        public MaintenanceStatus Status { get; set; }

        public DateTime ServiceDate { get; set; }

        public DateTime? CompletionDate { get; set; }

        public string AssignedTechnicianOrVendor { get; set; }

        public decimal Cost { get; set; }

        public string Outcome { get; set; }

        public string PreviousOwnerUserId { get; set; }

        public string PreviousOwnerName { get; set; }

        public bool CanComplete { get; set; }
    }

    public class MaintenanceCompleteVm
    {
        public int Id { get; set; }

        public int AssetId { get; set; }

        public string MaintenanceTicketNumber { get; set; }

        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public string PreviousOwnerUserId { get; set; }

        public string PreviousOwnerName { get; set; }

        [Required]
        [Display(Name = "Returned from repair date")]
        public DateTime CompletionDate { get; set; }

        [Required]
        [StringLength(1000)]
        [Display(Name = "Repair outcome")]
        public string Outcome { get; set; }

        [Required]
        [Display(Name = "After repair")]
        public string Disposition { get; set; }

        [Display(Name = "Condition after repair")]
        [StringLength(200)]
        public string ConditionAfter { get; set; }

        [Display(Name = "Assign to department")]
        public int? ToDepartmentId { get; set; }

        [Display(Name = "Assign to user")]
        [StringLength(128)]
        public string ToUserId { get; set; }

        [Display(Name = "Handover notes")]
        [StringLength(1000)]
        public string HandoverNotes { get; set; }
    }

    public class IncidentListVm
    {
        public int Id { get; set; }

        public string IncidentNumber { get; set; }

        public int AssetId { get; set; }

        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public string IncidentType { get; set; }

        public IncidentSeverity Severity { get; set; }

        public DateTime IncidentDate { get; set; }

        public string ResolutionStatus { get; set; }
    }

    public class IncidentDetailsVm
    {
        public int Id { get; set; }

        public string IncidentNumber { get; set; }

        public int AssetId { get; set; }

        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public string IncidentType { get; set; }

        public IncidentSeverity Severity { get; set; }

        public DateTime IncidentDate { get; set; }

        public string Description { get; set; }

        public string WitnessComments { get; set; }

        public string PoliceCaseReference { get; set; }

        public string LiabilityNotes { get; set; }

        public string ResolutionStatus { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class ClaimListVm
    {
        public int Id { get; set; }

        public string ClaimNumber { get; set; }

        public int AssetId { get; set; }

        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public string ClaimType { get; set; }

        public string Insurer { get; set; }

        public ClaimStatus ClaimStatus { get; set; }

        public DateTime ClaimDate { get; set; }
    }

    public class ClaimDetailsVm
    {
        public int Id { get; set; }

        public string ClaimNumber { get; set; }

        public int AssetId { get; set; }

        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public int? IncidentId { get; set; }

        public string IncidentNumber { get; set; }

        public string ClaimType { get; set; }

        public string Insurer { get; set; }

        public string Assessor { get; set; }

        public ClaimStatus ClaimStatus { get; set; }

        public decimal ApprovedAmount { get; set; }

        public DateTime ClaimDate { get; set; }

        public DateTime? SettlementDate { get; set; }

        public string SettlementNotes { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class AssetDocumentVm
    {
        public int Id { get; set; }

        public int AssetId { get; set; }

        public string DocumentType { get; set; }

        public string FileName { get; set; }

        public string ContentType { get; set; }

        public long FileSizeBytes { get; set; }

        public string UploadedByName { get; set; }

        public DateTime UploadedAt { get; set; }
    }

    public class AssetDocumentUploadVm
    {
        public int AssetId { get; set; }

        public string DocumentType { get; set; }
    }
}

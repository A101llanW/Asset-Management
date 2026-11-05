using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Application.ViewModels
{
    public class SettingsVm
    {
        [Required]
        [StringLength(500)]
        [Display(Name = "Attachment Root Path")]
        public string AttachmentRootPath { get; set; }

        [Range(0, 3650)]
        [Display(Name = "Warranty Alert Threshold (Days)")]
        public int WarrantyThresholdDays { get; set; }

        [Range(0, 3650)]
        [Display(Name = "Insurance Alert Threshold (Days)")]
        public int InsuranceThresholdDays { get; set; }

        [Range(0, 3650)]
        [Display(Name = "Loan Return Alert Threshold (Days)")]
        public int MaintenanceThresholdDays { get; set; }

        [Required]
        [StringLength(10)]
        [Display(Name = "Default Currency")]
        public string DefaultCurrency { get; set; }

        [Display(Name = "Require Transfer Approval")]
        public bool RequireTransferApproval { get; set; }

        [Display(Name = "Require Disposal Approval")]
        public bool RequireDisposalApproval { get; set; }

        public IList<ApprovalProcessSettingsVm> ApprovalProcesses { get; set; } = new List<ApprovalProcessSettingsVm>();

        public LabelPrinterSettingsVm LabelPrinter { get; set; } = new LabelPrinterSettingsVm();

        /// <summary>Migration 112: company Head of KG (any active user). Blank = not set.</summary>
        [Display(Name = "Head of KG")]
        [StringLength(128)]
        public string KgHeadUserId { get; set; }

        /// <summary>Migration 112: company Head of Prep.</summary>
        [Display(Name = "Head of Prep")]
        [StringLength(128)]
        public string PrepHeadUserId { get; set; }

        /// <summary>Migration 112: company Head of Senior school.</summary>
        [Display(Name = "Head of Senior school")]
        [StringLength(128)]
        public string SeniorHeadUserId { get; set; }
    }
}
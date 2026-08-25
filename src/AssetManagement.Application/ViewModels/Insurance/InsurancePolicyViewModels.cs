using System;
using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Application.ViewModels
{
    public class InsurancePolicyListVm
    {
        public int Id { get; set; }

        public int AssetId { get; set; }

        public string InsurerName { get; set; }

        public string PolicyNumber { get; set; }

        public DateTime PolicyStartDate { get; set; }

        public DateTime PolicyEndDate { get; set; }

        public decimal InsuredValue { get; set; }

        public bool ClaimEligibility { get; set; }
    }

    public class InsurancePolicyEditVm
    {
        public int Id { get; set; }

        [Required]
        public int AssetId { get; set; }

        [Required]
        [StringLength(200)]
        public string InsurerName { get; set; }

        [Required]
        [StringLength(100)]
        public string PolicyNumber { get; set; }

        [Required]
        public DateTime PolicyStartDate { get; set; }

        [Required]
        public DateTime PolicyEndDate { get; set; }

        [Range(0, 999999999)]
        public decimal InsuredValue { get; set; }

        public DateTime? ValuationDate { get; set; }

        public bool ClaimEligibility { get; set; }

        [Range(0, 999999999)]
        public decimal DeductibleAmount { get; set; }

        [StringLength(500)]
        public string ClaimNotes { get; set; }
    }
}

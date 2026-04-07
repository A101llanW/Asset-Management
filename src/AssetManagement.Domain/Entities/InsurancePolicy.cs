using System;
using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class InsurancePolicy : AuditableEntity
    {
        public int Id { get; set; }

        public int AssetId { get; set; }

        public string InsurerName { get; set; }

        public string PolicyNumber { get; set; }

        public DateTime PolicyStartDate { get; set; }

        public DateTime PolicyEndDate { get; set; }

        public decimal InsuredValue { get; set; }

        public decimal ReplacementValue { get; set; }

        public DateTime? ValuationDate { get; set; }

        public bool ClaimEligibility { get; set; }

        public decimal DeductibleAmount { get; set; }

        public string ClaimNotes { get; set; }

        public virtual Asset Asset { get; set; }
    }
}

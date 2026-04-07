using System;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Domain.Entities
{
    public class DepreciationRecord : AuditableEntity
    {
        public int Id { get; set; }

        public int AssetId { get; set; }

        public DateTime PeriodStartDate { get; set; }

        public DateTime PeriodEndDate { get; set; }

        public DepreciationMethod Method { get; set; }

        public decimal OpeningBookValue { get; set; }

        public decimal DepreciationAmount { get; set; }

        public decimal ClosingBookValue { get; set; }

        public decimal AccumulatedDepreciation { get; set; }

        public bool IsPosted { get; set; }

        public DateTime? PostedAt { get; set; }

        public virtual Asset Asset { get; set; }
    }
}

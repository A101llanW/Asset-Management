using System;
using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class AssetReceiving : AuditableEntity
    {
        public int Id { get; set; }

        public int PurchaseRecordId { get; set; }

        public int AssetId { get; set; }

        public DateTime ReceivedDate { get; set; }

        public string ConditionOnReceipt { get; set; }

        public int QuantityReceived { get; set; }

        public string ReceivedById { get; set; }

        public string Notes { get; set; }

        public virtual PurchaseRecord PurchaseRecord { get; set; }

        public virtual Asset Asset { get; set; }
    }
}

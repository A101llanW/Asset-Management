using System;
using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class AssetReturn : AuditableEntity
    {
        public int Id { get; set; }

        public int AssetId { get; set; }

        public string ReturnedById { get; set; }

        public string ReceivedById { get; set; }

        public DateTime ReturnDate { get; set; }

        public string ReturnCondition { get; set; }

        public bool MissingAccessories { get; set; }

        public string DamageNotes { get; set; }

        public string Notes { get; set; }

        public byte[] RowVersion { get; set; }

        public virtual Asset Asset { get; set; }
    }
}

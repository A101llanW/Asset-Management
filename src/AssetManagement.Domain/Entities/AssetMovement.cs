using System;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Domain.Entities
{
    /// <summary>
    /// One row per change of an asset's department and/or custodian (migration 093). Append-only;
    /// OccurredAtUtc is always the real UTC time the change was saved (display converts to the org time zone).
    /// </summary>
    public class AssetMovement : ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public int AssetId { get; set; }

        public int? FromDepartmentId { get; set; }

        public int? ToDepartmentId { get; set; }

        public string FromCustodianUserId { get; set; }

        public string ToCustodianUserId { get; set; }

        public AssetMovementType MovementType { get; set; }

        public string BatchId { get; set; }

        public string PerformedByUserId { get; set; }

        public DateTime OccurredAtUtc { get; set; }

        public string Notes { get; set; }

        public string SourceRef { get; set; }
    }
}

using System;
using System.Collections.Generic;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.ViewModels
{
    public class AssetMovementFilterVm
    {
        /// <summary>Local (org time zone) calendar day, inclusive.</summary>
        public DateTime? FromDate { get; set; }

        /// <summary>Local (org time zone) calendar day, inclusive.</summary>
        public DateTime? ToDate { get; set; }

        public int? DepartmentId { get; set; }

        public AssetMovementType? MovementType { get; set; }

        public string AssetSearch { get; set; }

        public string BatchId { get; set; }

        public int? AssetId { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 50;
    }

    public class AssetMovementRowVm
    {
        public int Id { get; set; }
        public int AssetId { get; set; }
        public string AssetTag { get; set; }
        public string AssetName { get; set; }
        public int? FromDepartmentId { get; set; }
        public string FromDepartmentName { get; set; }
        public int? ToDepartmentId { get; set; }
        public string ToDepartmentName { get; set; }
        public string FromCustodianUserId { get; set; }
        public string FromCustodianName { get; set; }
        public string ToCustodianUserId { get; set; }
        public string ToCustodianName { get; set; }
        public AssetMovementType MovementType { get; set; }
        public string MovementTypeLabel { get; set; }
        public string BatchId { get; set; }
        public string PerformedByUserId { get; set; }
        public string PerformedByName { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string Notes { get; set; }
        public string SourceRef { get; set; }
    }

    public class AssetMovementQueryResult
    {
        public IList<AssetMovementRowVm> Rows { get; set; } = new List<AssetMovementRowVm>();
        public int TotalCount { get; set; }
        /// <summary>True when the AssetMovement table does not exist yet (migration 093 not applied).</summary>
        public bool Unavailable { get; set; }
    }

    public class AssetMovementPageVm
    {
        public AssetMovementFilterVm Filter { get; set; }
        public IList<AssetMovementRowVm> Rows { get; set; } = new List<AssetMovementRowVm>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool Unavailable { get; set; }
    }
}

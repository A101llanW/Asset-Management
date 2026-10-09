using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.ViewModels
{
    /// <summary>Where a fulfilled request is delivered (migration 097).</summary>
    public enum AssetRequestTargetKind
    {
        User = 0,
        Room = 1
    }

    public class AssetRequestFilterVm
    {
        public string Search { get; set; }

        public AssetRequestStatus? Status { get; set; }

        public int? DepartmentId { get; set; }

        public string RequestedById { get; set; }
    }

    public class AssetRequestListPageVm
    {
        public IList<AssetRequestListVm> Items { get; set; } = new List<AssetRequestListVm>();

        public int TotalCount { get; set; }

        public string Search { get; set; }

        public string Sort { get; set; }

        public string Direction { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }

    public class AssetRequestListVm
    {
        public int Id { get; set; }

        public string RequestedByName { get; set; }

        public string DepartmentName { get; set; }

        public string CategoryName { get; set; }

        public string RequestedAssetTag { get; set; }

        public string RequestedAssetName { get; set; }

        public AssetRequestStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class AssetRequestCreateVm
    {
        public bool RequestForSelf { get; set; } = true;

        public string RequestedForUserId { get; set; }

        [Required(ErrorMessage = "Department is required.")]
        public int? DepartmentId { get; set; }

        [Required(ErrorMessage = "Asset category is required.")]
        public int? CategoryId { get; set; }

        [Required(ErrorMessage = "Please select the asset you want to request.")]
        public int? RequestedAssetId { get; set; }

        [Required]
        [StringLength(500)]
        public string Justification { get; set; }

        /// <summary>User (default) or a Class/Room location.</summary>
        public AssetRequestTargetKind TargetKind { get; set; }

        /// <summary>Class/Room department when TargetKind is Room.</summary>
        public int? TargetDepartmentId { get; set; }
    }

    public class AssetRequestDetailsVm
    {
        public int Id { get; set; }

        public string RequestedById { get; set; }

        public string RequestedByName { get; set; }

        public int? DepartmentId { get; set; }

        public string DepartmentName { get; set; }

        public int? CategoryId { get; set; }

        public string CategoryName { get; set; }

        public int? RequestedAssetId { get; set; }

        public string RequestedAssetName { get; set; }

        public string RequestedAssetTag { get; set; }

        public string Justification { get; set; }

        public AssetRequestStatus Status { get; set; }

        public int? FulfilledAssetId { get; set; }

        public string FulfilledAssetTag { get; set; }

        public string ReviewedByName { get; set; }

        public DateTime? ReviewedAt { get; set; }

        public string ReviewNotes { get; set; }

        public DateTime CreatedAt { get; set; }

        public AssetRequestTargetKind TargetKind { get; set; }

        public int? TargetDepartmentId { get; set; }

        public string TargetDepartmentName { get; set; }
    }

    public class AssetRequestFulfillVm
    {
        public int RequestId { get; set; }

        [Required]
        public int AssetId { get; set; }

        public int? ToDepartmentId { get; set; }

        /// <summary>Required for a user target; empty for a room target (person in charge or pool).</summary>
        public string ToUserId { get; set; }

        public string HandoverNotes { get; set; }

        /// <summary>Room target with a person in charge: "Pic" or "Pool" (same prompt as Assignments).</summary>
        public string PicPlacementChoice { get; set; }

        /// <summary>Move / Keep when the asset sits in another department (b21ac7f rules).</summary>
        public string DepartmentDisposition { get; set; }

        /// <summary>rel1009h: "Assign" (default, deliver to the user/room target) or "Store" (receive into store, no assignee).</summary>
        public string FulfilMode { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.ViewModels
{
    /// <summary>
    /// Assets Index "Group by room" (view=room). Level 1 = one row per unit the filtered assets sit in
    /// (rooms first), level 2 = categories inside a unit, level 3 = the individual assets.
    /// </summary>
    public static class AssetRoomGroupView
    {
        public const string ViewMode = "room";

        public const string UnassignedLabel = "No room / Unassigned";

        /// <summary>Room key used by the level 2/3 endpoints for assets with no department.</summary>
        public const int UnassignedRoomId = 0;

        public const int DefaultAssetPageSize = 25;

        public const int MaxAssetPageSize = 100;

        public static bool IsRoomView(string view)
        {
            return string.Equals((view ?? string.Empty).Trim(), ViewMode, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Rooms (DepartmentKind.Room) first, then every other unit, each by name (then id for a
        /// stable page order); assets with no unit ("No room / Unassigned") last. Paging happens after
        /// sorting, so it is done here in memory over the small per-unit aggregate (one row per unit).
        /// </summary>
        public static AssetRoomGroupPageVm SortAndPage(IEnumerable<AssetRoomGroupVm> groups, int page, int pageSize)
        {
            var all = (groups ?? Enumerable.Empty<AssetRoomGroupVm>())
                .Where(x => x != null)
                .OrderBy(x => x.DepartmentId.HasValue ? 0 : 1)
                .ThenBy(x => x.IsRoom ? 0 : 1)
                .ThenBy(x => x.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.DepartmentId ?? 0)
                .ToList();

            var safePageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)all.Count / safePageSize));
            var safePage = Math.Min(Math.Max(page, 1), totalPages);

            return new AssetRoomGroupPageVm
            {
                Items = all.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToList(),
                TotalCount = all.Count,
                RoomCount = all.Count(x => x.IsRoom),
                TotalAssetCount = all.Sum(x => x.AssetCount),
                PricedTotal = all.Sum(x => x.PricedTotal),
                UnpricedCount = all.Sum(x => x.UnpricedCount),
                Page = safePage,
                PageSize = safePageSize
            };
        }

        public static string FormatKind(int? departmentKind)
        {
            if (!departmentKind.HasValue)
            {
                return null;
            }

            switch ((DepartmentKind)departmentKind.Value)
            {
                case DepartmentKind.Room:
                    return "Room";
                case DepartmentKind.Class:
                    return "Classroom";
                case DepartmentKind.Grade:
                    return "Year";
                case DepartmentKind.SubDepartment:
                    return "Sub-department";
                default:
                    return "Department";
            }
        }
    }

    public class AssetRoomGroupVm
    {
        /// <summary>Null when the assets have no department ("No room / Unassigned").</summary>
        public int? DepartmentId { get; set; }

        public string Name { get; set; }

        public int? DepartmentKind { get; set; }

        public string ParentName { get; set; }

        public int AssetCount { get; set; }

        /// <summary>Assets with their own price (AcquisitionCost not NULL).</summary>
        public int PricedCount { get; set; }

        /// <summary>Sum of the assets' own prices; unpriced assets are excluded, never counted as 0 or a sub-type price.</summary>
        public decimal PricedTotal { get; set; }

        public int UnpricedCount
        {
            get { return Math.Max(0, AssetCount - PricedCount); }
        }

        public bool IsRoom
        {
            get { return DepartmentKind.HasValue && DepartmentKind.Value == (int)AssetManagement.Domain.Enums.DepartmentKind.Room; }
        }

        public int RoomId
        {
            get { return DepartmentId ?? AssetRoomGroupView.UnassignedRoomId; }
        }

        public string DisplayName
        {
            get
            {
                if (!DepartmentId.HasValue)
                {
                    return AssetRoomGroupView.UnassignedLabel;
                }

                return string.IsNullOrWhiteSpace(Name) ? "Unit #" + DepartmentId.Value : AssetManagement.Application.Helpers.GradeDisplay.ToYear(Name);
            }
        }

        public string KindLabel
        {
            get { return AssetRoomGroupView.FormatKind(DepartmentKind); }
        }
    }

    public class AssetRoomGroupPageVm
    {
        public IList<AssetRoomGroupVm> Items { get; set; } = new List<AssetRoomGroupVm>();

        /// <summary>Number of level-1 rows (units) across all pages.</summary>
        public int TotalCount { get; set; }

        public int RoomCount { get; set; }

        public int TotalAssetCount { get; set; }

        public decimal PricedTotal { get; set; }

        public int UnpricedCount { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalPages
        {
            get { return Math.Max(1, (int)Math.Ceiling((double)TotalCount / Math.Max(1, PageSize))); }
        }
    }

    public class AssetRoomCategoryGroupVm
    {
        public int CategoryId { get; set; }

        public string Name { get; set; }

        public int AssetCount { get; set; }

        public int PricedCount { get; set; }

        public decimal PricedTotal { get; set; }

        public int UnpricedCount
        {
            get { return Math.Max(0, AssetCount - PricedCount); }
        }
    }

    public class AssetRoomAssetVm
    {
        public int Id { get; set; }

        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public string AssetSubTypeName { get; set; }

        public string CurrentCustodianId { get; set; }

        public string CurrentCustodianName { get; set; }

        public AssetStatus CurrentStatus { get; set; }

        /// <summary>The asset's own price; null = "Not set" (never filled from the sub-type).</summary>
        public decimal? Price { get; set; }
    }

    public class AssetRoomAssetPageVm
    {
        public IList<AssetRoomAssetVm> Items { get; set; } = new List<AssetRoomAssetVm>();

        public int TotalCount { get; set; }

        public int Skip { get; set; }

        public int Take { get; set; }
    }
}

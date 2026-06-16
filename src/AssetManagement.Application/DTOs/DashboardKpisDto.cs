using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.DTOs
{
    public class DashboardKpisDto
    {
        public int TotalAssets { get; set; }

        public int AssignedAssets { get; set; }

        public int UnassignedAssets { get; set; }

        public int AssetsUnderMaintenance { get; set; }

        public int LostDamagedStolenAssets { get; set; }

        public decimal TotalAcquisitionValue { get; set; }

        public decimal TotalCostOfOwnership { get; set; }

        public IList<DepartmentValueVm> TopDepartmentValues { get; set; }

        public IList<DashboardTrendPointVm> AssignmentsPerMonth { get; set; }
    }
}

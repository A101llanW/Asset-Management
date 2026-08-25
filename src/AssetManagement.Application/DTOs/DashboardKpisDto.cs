using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.DTOs
{
    public class DashboardKpisDto
    {
        public int TotalAssets { get; set; }

        public int AssignedAssets { get; set; }

        public int UnassignedAssets { get; set; }

        public int MaintenanceAndDamagedAssets { get; set; }

        public int LostAssets { get; set; }

        public int LostDamagedStolenAssets { get; set; }

        public decimal TotalAcquisitionValue { get; set; }

        public decimal TotalCurrentBookValue { get; set; }

        public decimal TotalAccumulatedDepreciation { get; set; }

        public decimal AverageAnnualDepreciationRatePercent { get; set; }

        public decimal DepreciationToDatePercent { get; set; }

        public decimal TotalCostOfOwnership { get; set; }

        public IList<DepartmentValueVm> TopDepartmentValues { get; set; }

        public IList<DashboardTrendPointVm> AssignmentsPerMonth { get; set; }
    }
}

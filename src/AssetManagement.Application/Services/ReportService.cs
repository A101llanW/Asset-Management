using System;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public class ReportService : IReportService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ReportService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public DashboardVm GetDashboard()
        {
            var assets = _unitOfWork.Repository<Asset>().GetAll().ToList();
            var now = DateTime.UtcNow;

            return new DashboardVm
            {
                TotalAssets = assets.Count,
                AssignedAssets = assets.Count(x => x.CurrentStatus == AssetStatus.Assigned),
                UnassignedAssets = assets.Count(x => x.CurrentStatus == AssetStatus.InStore || x.CurrentStatus == AssetStatus.Returned),
                AssetsUnderMaintenance = assets.Count(x => x.CurrentStatus == AssetStatus.UnderMaintenance),
                LostDamagedStolenAssets = assets.Count(x => x.CurrentStatus == AssetStatus.Lost || x.CurrentStatus == AssetStatus.Stolen || x.CurrentStatus == AssetStatus.Damaged),
                TotalAcquisitionValue = assets.Sum(x => x.AcquisitionCost),
                TotalCurrentBookValue = assets.Sum(x => x.CurrentBookValue),
                ExpiringWarrantyCount = assets.Count(x => x.WarrantyEndDate.HasValue && x.WarrantyEndDate.Value <= now.AddDays(60)),
                ExpiringInsuranceCount = _unitOfWork.Repository<InsurancePolicy>().GetAll().Count(x => x.PolicyEndDate <= now.AddDays(60))
            };
        }
    }
}

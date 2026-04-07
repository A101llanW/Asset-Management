using System;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public class DepreciationService : IDepreciationService
    {
        private readonly IUnitOfWork _unitOfWork;

        public DepreciationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public void RunMonthlyDepreciation()
        {
            var assets = _unitOfWork.Repository<Asset>().GetAll().Where(x => x.IsActive).ToList();
            var now = DateTime.UtcNow;
            var periodStart = new DateTime(now.Year, now.Month, 1);
            var periodEnd = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));

            foreach (var asset in assets)
            {
                if (asset.CurrentStatus == AssetStatus.Disposed || asset.CurrentStatus == AssetStatus.Retired)
                {
                    continue;
                }

                if (asset.DepreciationMethod != DepreciationMethod.StraightLine || asset.UsefulLifeMonths <= 0)
                {
                    continue;
                }

                if (asset.DepreciationStartDate > periodEnd)
                {
                    continue;
                }

                var alreadyPosted = _unitOfWork.Repository<DepreciationRecord>()
                    .Find(x => x.AssetId == asset.Id && x.PeriodStartDate == periodStart && x.PeriodEndDate == periodEnd)
                    .Any();
                if (alreadyPosted)
                {
                    continue;
                }

                if (asset.CurrentBookValue <= asset.SalvageValue)
                {
                    continue;
                }

                var depreciableAmount = asset.AcquisitionCost - asset.SalvageValue;
                var monthly = Math.Round(depreciableAmount / asset.UsefulLifeMonths, 2);
                if (monthly <= 0)
                {
                    continue;
                }

                var openingBookValue = asset.CurrentBookValue;
                var nextBookValue = openingBookValue - monthly;
                if (nextBookValue < asset.SalvageValue)
                {
                    monthly = openingBookValue - asset.SalvageValue;
                    nextBookValue = asset.SalvageValue;
                }

                if (monthly <= 0)
                {
                    continue;
                }

                asset.CurrentBookValue = nextBookValue;
                asset.AccumulatedDepreciation += monthly;
                asset.UpdatedAt = now;
                _unitOfWork.Repository<Asset>().Update(asset);

                _unitOfWork.Repository<DepreciationRecord>().Add(new DepreciationRecord
                {
                    AssetId = asset.Id,
                    Method = DepreciationMethod.StraightLine,
                    PeriodStartDate = periodStart,
                    PeriodEndDate = periodEnd,
                    OpeningBookValue = openingBookValue,
                    DepreciationAmount = monthly,
                    ClosingBookValue = nextBookValue,
                    AccumulatedDepreciation = asset.AccumulatedDepreciation,
                    IsPosted = true,
                    PostedAt = now,
                    CreatedAt = now
                });
            }

            _unitOfWork.SaveChanges();
        }
    }
}

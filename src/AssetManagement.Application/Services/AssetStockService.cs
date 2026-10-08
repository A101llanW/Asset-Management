using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
namespace AssetManagement.Application.Services
{
    public class AssetStockService : IAssetStockService
    {
        private static readonly AssetStatus[] AvailableStatuses =
        {
            AssetStatus.InStore,
            AssetStatus.Returned,
            AssetStatus.Received
        };
        private readonly IUnitOfWork _unitOfWork;
        public AssetStockService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public int GetAvailableQuantity(int subTypeId, int? departmentId)
        {
            var subType = _unitOfWork.Repository<AssetSubType>().GetById(subTypeId);
            if (subType == null || !subType.IsActive)
            {
                return 0;
            }
            return CountAvailableAssets(subTypeId, departmentId);
        }
        public int GetAvailableQuantityForAsset(int assetId, int? departmentId)
        {
            var asset = _unitOfWork.Repository<Asset>().GetById(assetId);
            if (asset == null || !asset.AssetSubTypeId.HasValue)
            {
                return 0;
            }
            var resolvedDepartmentId = departmentId ?? asset.DepartmentId;
            return GetAvailableQuantity(asset.AssetSubTypeId.Value, resolvedDepartmentId);
        }
        private int CountAvailableAssets(int subTypeId, int? departmentId)
        {
            // One SQL COUNT (org-scoped by the repository). This used to be GetAll().Where(...).Count(),
            // which read every asset row of the organisation into memory on Asset Create/Edit.
            var statuses = AvailableStatuses;
            var repository = _unitOfWork.Repository<Asset>();
            if (departmentId.HasValue)
            {
                var deptId = departmentId.Value;
                return repository.Count(x => x.IsActive
                    && x.AssetSubTypeId == subTypeId
                    && statuses.Contains(x.CurrentStatus)
                    && x.DepartmentId == deptId);
            }

            return repository.Count(x => x.IsActive
                && x.AssetSubTypeId == subTypeId
                && statuses.Contains(x.CurrentStatus));
        }
    }
}

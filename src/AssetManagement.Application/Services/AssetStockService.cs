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
            var query = _unitOfWork.Repository<Asset>().GetAll()
                .Where(x => x.IsActive
                    && x.AssetSubTypeId == subTypeId
                    && AvailableStatuses.Contains(x.CurrentStatus));
            if (departmentId.HasValue)
            {
                query = query.Where(x => x.DepartmentId == departmentId.Value);
            }
            return query.Count();
        }
    }
}

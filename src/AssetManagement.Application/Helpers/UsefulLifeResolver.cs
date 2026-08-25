using AssetManagement.Application.Contracts;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Helpers
{
    public static class UsefulLifeResolver
    {
        public static int? Resolve(IUnitOfWork unitOfWork, int assetTypeId, int categoryId)
        {
            if (unitOfWork == null)
            {
                return null;
            }

            var assetType = unitOfWork.Repository<AssetType>().GetById(assetTypeId);
            if (assetType != null && assetType.UsefulLifeMonths.HasValue && assetType.UsefulLifeMonths.Value > 0)
            {
                return assetType.UsefulLifeMonths.Value;
            }

            var category = unitOfWork.Repository<AssetCategory>().GetById(categoryId);
            if (category != null && category.DefaultUsefulLifeMonths.HasValue && category.DefaultUsefulLifeMonths.Value > 0)
            {
                return category.DefaultUsefulLifeMonths.Value;
            }

            return null;
        }

        public static string DescribeSource(IUnitOfWork unitOfWork, int assetTypeId, int categoryId)
        {
            if (unitOfWork == null)
            {
                return "not configured";
            }

            var assetType = unitOfWork.Repository<AssetType>().GetById(assetTypeId);
            if (assetType != null && assetType.UsefulLifeMonths.HasValue && assetType.UsefulLifeMonths.Value > 0)
            {
                return "asset type";
            }

            var category = unitOfWork.Repository<AssetCategory>().GetById(categoryId);
            if (category != null && category.DefaultUsefulLifeMonths.HasValue && category.DefaultUsefulLifeMonths.Value > 0)
            {
                return "category";
            }

            return "not configured";
        }
    }
}

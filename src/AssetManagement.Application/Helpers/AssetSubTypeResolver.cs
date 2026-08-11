using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Helpers
{
    public class AssetSubTypeResolver
    {
        private readonly IAssetSubTypeService _assetSubTypeService;

        public AssetSubTypeResolver(IAssetSubTypeService assetSubTypeService)
        {
            _assetSubTypeService = assetSubTypeService;
        }

        public AssetSubTypeResolutionResult Resolve(int assetTypeId, string brand, string model, int? existingSubTypeId = null)
        {
            if (existingSubTypeId.HasValue && existingSubTypeId.Value > 0)
            {
                var existing = _assetSubTypeService.GetById(existingSubTypeId.Value);
                if (existing != null && existing.AssetTypeId == assetTypeId)
                {
                    return AssetSubTypeResolutionResult.Matched(existing);
                }
            }

            var normalizedBrand = AssetSubTypeNormalizer.NormalizeBrand(brand);
            var normalizedModel = AssetSubTypeNormalizer.NormalizeModel(model);
            if (string.IsNullOrEmpty(normalizedBrand) && string.IsNullOrEmpty(normalizedModel))
            {
                return AssetSubTypeResolutionResult.NotRequired();
            }

            var match = _assetSubTypeService.Lookup(assetTypeId, normalizedBrand, normalizedModel);
            if (match != null)
            {
                return AssetSubTypeResolutionResult.Matched(match);
            }

            return AssetSubTypeResolutionResult.Unresolved(normalizedBrand, normalizedModel);
        }

        public void ApplyToAsset(Asset asset, AssetSubTypeVm subType)
        {
            if (asset == null || subType == null)
            {
                return;
            }

            asset.AssetSubTypeId = subType.Id;
            asset.AssetTypeId = subType.AssetTypeId;
            asset.Brand = subType.Brand;
            asset.Model = subType.Model;
        }
    }

    public class AssetSubTypeResolutionResult
    {
        public bool IsMatched { get; private set; }

        public bool RequiresAssignment { get; private set; }

        public AssetSubTypeVm SubType { get; private set; }

        public string Brand { get; private set; }

        public string Model { get; private set; }

        public static AssetSubTypeResolutionResult Matched(AssetSubTypeVm subType)
        {
            return new AssetSubTypeResolutionResult
            {
                IsMatched = true,
                SubType = subType
            };
        }

        public static AssetSubTypeResolutionResult Unresolved(string brand, string model)
        {
            return new AssetSubTypeResolutionResult
            {
                RequiresAssignment = true,
                Brand = brand,
                Model = model
            };
        }

        public static AssetSubTypeResolutionResult NotRequired()
        {
            return new AssetSubTypeResolutionResult();
        }
    }
}

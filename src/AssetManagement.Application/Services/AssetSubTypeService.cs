using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
namespace AssetManagement.Application.Services
{
    public class AssetSubTypeService : IAssetSubTypeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAssetStockService _assetStockService;
        private readonly IAuditWriter _auditWriter;

        public AssetSubTypeService(
            IUnitOfWork unitOfWork,
            IAssetStockService assetStockService,
            IAuditWriter auditWriter = null)
        {
            _unitOfWork = unitOfWork;
            _assetStockService = assetStockService;
            _auditWriter = auditWriter;
        }
        public AssetSubTypeVm GetById(int id)
        {
            var entity = _unitOfWork.Repository<AssetSubType>().GetById(id);
            return entity == null ? null : MapDetail(entity);
        }
        public IEnumerable<AssetSubTypeListItemVm> GetByAssetTypeId(int assetTypeId, bool activeOnly = true)
        {
            var items = _unitOfWork.Repository<AssetSubType>().GetAll()
                .Where(x => x.AssetTypeId == assetTypeId)
                .OrderBy(x => x.Name)
                .ToList();
            if (activeOnly)
            {
                items = items.Where(x => x.IsActive).ToList();
            }
            return items.Select(MapListItem).ToList();
        }
        public AssetSubTypeVm Lookup(int assetTypeId, string brand, string model)
        {
            var normalizedBrand = AssetSubTypeNormalizer.NormalizeBrand(brand);
            var normalizedModel = AssetSubTypeNormalizer.NormalizeModel(model);
            if (string.IsNullOrEmpty(normalizedBrand) && string.IsNullOrEmpty(normalizedModel))
            {
                return null;
            }
            var match = _unitOfWork.Repository<AssetSubType>().GetAll()
                .FirstOrDefault(x => x.IsActive
                    && x.AssetTypeId == assetTypeId
                    && AssetSubTypeNormalizer.BrandModelEquals(x.Brand, x.Model, normalizedBrand, normalizedModel));
            return match == null ? null : MapDetail(match);
        }
        public int Create(AssetSubTypeEditVm model)
        {
            ValidateModel(model, null);
            var entity = MapToEntity(model);
            entity.CreatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<AssetSubType>().Add(entity);
            _unitOfWork.SaveChanges();
            _auditWriter?.Write("AssetSubTypes.Create", nameof(AssetSubType), entity.Id.ToString(), null, entity.Name);
            return entity.Id;
        }
        public void Update(AssetSubTypeEditVm model)
        {
            var entity = _unitOfWork.Repository<AssetSubType>().GetById(model.Id);
            if (entity == null)
            {
                throw new BusinessException("Asset sub-type not found.");
            }
            ValidateModel(model, entity.Id);
            ApplyModel(entity, model);
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<AssetSubType>().Update(entity);
            _unitOfWork.SaveChanges();
            _auditWriter?.Write("AssetSubTypes.Edit", nameof(AssetSubType), entity.Id.ToString(), null, entity.Name);
        }
        public int CreateFromAsset(AssetSubTypeCreateFromAssetVm model)
        {
            if (model == null)
            {
                throw new BusinessException("Sub-type details are required.");
            }
            var editModel = new AssetSubTypeEditVm
            {
                AssetTypeId = model.AssetTypeId,
                Name = model.Name,
                Brand = model.Brand,
                ItemModel = model.Model,
                Specifications = model.Specifications,
                Sku = model.Sku,
                IsActive = true
            };
            return Create(editModel);
        }
        public int CountAssets(int subTypeId)
        {
            var subType = _unitOfWork.Repository<AssetSubType>().GetById(subTypeId);
            if (subType == null)
            {
                return 0;
            }
            return _unitOfWork.Repository<Asset>().GetAll()
                .Count(x => x.IsActive && x.AssetSubTypeId == subTypeId);
        }
        public int GetTotalQuantityOnHand(int subTypeId)
        {
            return CountAssets(subTypeId);
        }
        private AssetSubTypeListItemVm MapListItem(AssetSubType entity)
        {
            return new AssetSubTypeListItemVm
            {
                Id = entity.Id,
                Name = AssetSubTypeNormalizer.NormalizeName(entity.Name),
                Brand = entity.Brand,
                Model = entity.Model,
                IsActive = entity.IsActive,
                StockCount = _assetStockService.GetAvailableQuantity(entity.Id, null)
            };
        }
        private AssetSubTypeVm MapDetail(AssetSubType entity)
        {
            var assetType = entity.AssetType ?? _unitOfWork.Repository<AssetType>().GetById(entity.AssetTypeId);
            var category = assetType == null
                ? null
                : assetType.AssetCategory ?? _unitOfWork.Repository<AssetCategory>().GetById(assetType.AssetCategoryId);
            return new AssetSubTypeVm
            {
                Id = entity.Id,
                AssetTypeId = entity.AssetTypeId,
                AssetTypeName = assetType == null ? null : assetType.Name,
                AssetCategoryId = assetType == null ? 0 : assetType.AssetCategoryId,
                AssetCategoryName = category == null ? null : category.Name,
                Name = AssetSubTypeNormalizer.NormalizeName(entity.Name),
                Brand = entity.Brand,
                Model = entity.Model,
                Specifications = entity.Specifications,
                Sku = entity.Sku,
                IsActive = entity.IsActive,
                StockCount = _assetStockService.GetAvailableQuantity(entity.Id, null)
            };
        }
        private static AssetSubType MapToEntity(AssetSubTypeEditVm model)
        {
            var entity = new AssetSubType();
            ApplyModel(entity, model);
            return entity;
        }
        private static void ApplyModel(AssetSubType entity, AssetSubTypeEditVm model)
        {
            entity.AssetTypeId = model.AssetTypeId;
            entity.Name = AssetSubTypeNormalizer.NormalizeName(model.Name);
            entity.Brand = AssetSubTypeNormalizer.NormalizeBrand(model.Brand);
            entity.Model = AssetSubTypeNormalizer.NormalizeModel(model.ItemModel);
            entity.Specifications = model.Specifications;
            entity.Sku = model.Sku;
            entity.IsActive = model.IsActive;
        }
        private void ValidateModel(AssetSubTypeEditVm model, int? currentId)
        {
            if (model == null)
            {
                throw new BusinessException("Sub-type details are required.");
            }
            if (model.AssetTypeId <= 0)
            {
                throw new BusinessException("Please select an asset type.");
            }
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                throw new BusinessException("Sub-type name is required.");
            }
            var brand = AssetSubTypeNormalizer.NormalizeBrand(model.Brand);
            var modelName = AssetSubTypeNormalizer.NormalizeModel(model.ItemModel);
            var duplicate = _unitOfWork.Repository<AssetSubType>().GetAll()
                .Any(x => x.IsActive
                    && x.Id != currentId.GetValueOrDefault()
                    && x.AssetTypeId == model.AssetTypeId
                    && AssetSubTypeNormalizer.BrandModelEquals(x.Brand, x.Model, brand, modelName));
            if (duplicate)
            {
                throw new BusinessException("This asset type already has a sub-type with the same brand and model.");
            }
        }
    }
}

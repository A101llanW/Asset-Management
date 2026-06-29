using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services
{
    public class SupplierCatalogService : ISupplierCatalogService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrganizationScopeService _organizationScope;

        public SupplierCatalogService(IUnitOfWork unitOfWork, IOrganizationScopeService organizationScope)
        {
            _unitOfWork = unitOfWork;
            _organizationScope = organizationScope;
        }

        public IEnumerable<SupplierCatalogItemVm> GetBySupplier(int supplierId)
        {
            EnsureSupplierExists(supplierId);
            return _unitOfWork.Repository<SupplierCatalogItem>()
                .Find(x => x.SupplierId == supplierId)
                .OrderBy(x => x.ItemName)
                .Select(MapItem)
                .ToList();
        }

        public SupplierCatalogItemVm GetById(int id)
        {
            var entity = _unitOfWork.Repository<SupplierCatalogItem>().GetById(id);
            return entity == null ? null : MapItem(entity);
        }

        public int Create(SupplierCatalogItemVm model)
        {
            ValidateModel(model);
            EnsureSupplierExists(model.SupplierId);
            var now = DateTime.UtcNow;
            var entity = new SupplierCatalogItem
            {
                SupplierId = model.SupplierId,
                ItemName = model.ItemName.Trim(),
                ItemDescription = NormalizeText(model.ItemDescription),
                Sku = NormalizeText(model.Sku),
                AssetCategoryId = model.AssetCategoryId,
                AssetTypeId = ResolveAssetTypeId(model.AssetCategoryId, model.AssetTypeId),
                TaggedAssetId = ResolveTaggedAssetId(model.TaggedAssetId, model.AssetCategoryId, model.AssetTypeId),
                UnitPrice = model.UnitPrice,
                Currency = NormalizeCurrency(model.Currency),
                MinimumOrderQuantity = model.MinimumOrderQuantity,
                LeadTimeDays = model.LeadTimeDays,
                EffectiveFrom = model.EffectiveFrom,
                EffectiveTo = model.EffectiveTo,
                IsActive = true,
                CreatedAt = now
            };
            _unitOfWork.Repository<SupplierCatalogItem>().Add(entity);
            _unitOfWork.SaveChanges();
            return entity.Id;
        }

        public void Update(SupplierCatalogItemVm model)
        {
            ValidateModel(model);
            var entity = _unitOfWork.Repository<SupplierCatalogItem>().GetById(model.Id);
            if (entity == null)
            {
                throw new BusinessException("Catalog item not found.");
            }

            entity.ItemName = model.ItemName.Trim();
            entity.ItemDescription = NormalizeText(model.ItemDescription);
            entity.Sku = NormalizeText(model.Sku);
            entity.AssetCategoryId = model.AssetCategoryId;
            entity.AssetTypeId = ResolveAssetTypeId(model.AssetCategoryId, model.AssetTypeId);
            entity.TaggedAssetId = ResolveTaggedAssetId(model.TaggedAssetId, model.AssetCategoryId, model.AssetTypeId);
            entity.UnitPrice = model.UnitPrice;
            entity.Currency = NormalizeCurrency(model.Currency);
            entity.MinimumOrderQuantity = model.MinimumOrderQuantity;
            entity.LeadTimeDays = model.LeadTimeDays;
            entity.EffectiveFrom = model.EffectiveFrom;
            entity.EffectiveTo = model.EffectiveTo;
            entity.IsActive = model.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<SupplierCatalogItem>().Update(entity);
            _unitOfWork.SaveChanges();
        }

        public void Deactivate(int id)
        {
            var entity = _unitOfWork.Repository<SupplierCatalogItem>().GetById(id);
            if (entity == null)
            {
                throw new BusinessException("Catalog item not found.");
            }

            entity.IsActive = false;
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<SupplierCatalogItem>().Update(entity);
            _unitOfWork.SaveChanges();
        }

        public void AddItemsForSupplier(int supplierId, IEnumerable<SupplierCatalogItemVm> items)
        {
            if (items == null)
            {
                return;
            }

            EnsureSupplierExists(supplierId);
            var now = DateTime.UtcNow;
            foreach (var model in items)
            {
                ValidateModel(model);
                var entity = new SupplierCatalogItem
                {
                    SupplierId = supplierId,
                    ItemName = model.ItemName.Trim(),
                    ItemDescription = NormalizeText(model.ItemDescription),
                    Sku = NormalizeText(model.Sku),
                    AssetCategoryId = model.AssetCategoryId,
                    AssetTypeId = ResolveAssetTypeId(model.AssetCategoryId, model.AssetTypeId),
                    TaggedAssetId = ResolveTaggedAssetId(model.TaggedAssetId, model.AssetCategoryId, model.AssetTypeId),
                    UnitPrice = model.UnitPrice,
                    Currency = NormalizeCurrency(model.Currency),
                    MinimumOrderQuantity = model.MinimumOrderQuantity,
                    LeadTimeDays = model.LeadTimeDays,
                    EffectiveFrom = model.EffectiveFrom,
                    EffectiveTo = model.EffectiveTo,
                    IsActive = true,
                    CreatedAt = now
                };
                _unitOfWork.Repository<SupplierCatalogItem>().Add(entity);
            }
        }

        public SupplierPriceComparisonResultVm GetPriceComparison(int? purchaseRequestId, string itemDescription, int? categoryId = null)
        {
            var result = new SupplierPriceComparisonResultVm();
            PurchaseRequest request = null;
            if (purchaseRequestId.HasValue)
            {
                request = _unitOfWork.Repository<PurchaseRequest>().GetById(purchaseRequestId.Value);
                if (request != null)
                {
                    result.RequisitionEstimatedUnitCost = request.EstimatedUnitCost;
                    result.Currency = request.Currency;
                    if (string.IsNullOrWhiteSpace(itemDescription))
                    {
                        itemDescription = request.ItemDescription;
                    }
                }
            }

            result.ItemDescription = itemDescription;
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            int? matchCategoryId = categoryId;
            int? matchAssetTypeId = null;
            int? matchTaggedAssetId = null;
            if (request != null && request.TargetAssetId.HasValue)
            {
                matchTaggedAssetId = request.TargetAssetId;
                var targetAsset = _unitOfWork.Repository<Asset>().GetById(request.TargetAssetId.Value);
                if (targetAsset != null)
                {
                    matchCategoryId = targetAsset.CategoryId;
                    matchAssetTypeId = targetAsset.AssetTypeId;
                }
            }

            var catalogRows = BuildCatalogMatches(
                organizationId,
                itemDescription,
                matchCategoryId,
                matchAssetTypeId,
                matchTaggedAssetId);
            if (catalogRows.Count > 0)
            {
                result.HasCatalogMatches = true;
                result.Rows = catalogRows;
            }
            else
            {
                var historical = BuildHistoricalMatches(organizationId, itemDescription, result.Currency);
                result.HasHistoricalFallback = historical.Count > 0;
                result.Rows = historical;
            }

            ApplyPriceBadges(result.Rows);
            if (string.IsNullOrWhiteSpace(result.Currency) && result.Rows.Count > 0)
            {
                result.Currency = result.Rows[0].Currency;
            }

            return result;
        }

        private List<SupplierPriceComparisonRowVm> BuildCatalogMatches(
            int? organizationId,
            string itemDescription,
            int? categoryId,
            int? assetTypeId,
            int? taggedAssetId)
        {
            var now = DateTime.UtcNow.Date;
            var suppliers = _unitOfWork.Repository<Supplier>().GetAll()
                .Where(x => x.IsActive)
                .ToDictionary(x => x.Id, x => x);
            var categories = _unitOfWork.Repository<AssetCategory>().GetAll().ToDictionary(x => x.Id, x => x.Name);
            var assetTypes = _unitOfWork.Repository<AssetType>().GetAll().ToDictionary(x => x.Id, x => x.Name);
            var tokens = Tokenize(itemDescription);

            var offers = _unitOfWork.Repository<SupplierCatalogItem>().GetAll()
                .Where(x => x.IsActive)
                .Where(x => !organizationId.HasValue || !x.OrganizationId.HasValue || x.OrganizationId == organizationId)
                .Where(x => suppliers.ContainsKey(x.SupplierId))
                .Where(x => !x.EffectiveFrom.HasValue || x.EffectiveFrom.Value.Date <= now)
                .Where(x => !x.EffectiveTo.HasValue || x.EffectiveTo.Value.Date >= now)
                .ToList();

            var rows = new List<SupplierPriceComparisonRowVm>();
            foreach (var offer in offers)
            {
                if (!MatchesOffer(offer, tokens, categoryId, assetTypeId, taggedAssetId))
                {
                    continue;
                }

                var supplier = suppliers[offer.SupplierId];
                rows.Add(new SupplierPriceComparisonRowVm
                {
                    CatalogItemId = offer.Id,
                    SupplierId = supplier.Id,
                    SupplierName = supplier.SupplierName,
                    ItemLabel = BuildItemLabel(offer, categories, assetTypes),
                    UnitPrice = offer.UnitPrice,
                    Currency = string.IsNullOrWhiteSpace(offer.Currency) ? ApprovalWorkflowSettingsHelper.GetDefaultCurrencyCode(_unitOfWork.Repository<SystemSetting>().GetAll()) : offer.Currency,
                    LeadTimeDays = offer.LeadTimeDays ?? supplier.DefaultLeadTimeDays,
                    IsPreferred = supplier.IsPreferred,
                    IsHistorical = false
                });
            }

            return rows.OrderBy(x => x.UnitPrice).ToList();
        }

        private List<SupplierPriceComparisonRowVm> BuildHistoricalMatches(int? organizationId, string itemDescription, string currency)
        {
            var tokens = Tokenize(itemDescription);
            var suppliers = _unitOfWork.Repository<Supplier>().GetAll()
                .Where(x => x.IsActive)
                .ToDictionary(x => x.Id, x => x);
            var purchaseRecords = _unitOfWork.Repository<PurchaseRecord>().GetAll()
                .Where(x => x.IsActive)
                .Where(x => !organizationId.HasValue || !x.OrganizationId.HasValue || x.OrganizationId == organizationId)
                .ToList();
            var requests = _unitOfWork.Repository<PurchaseRequest>().GetAll().ToDictionary(x => x.Id, x => x);

            var grouped = new Dictionary<int, List<decimal>>();
            foreach (var record in purchaseRecords)
            {
                if (!suppliers.ContainsKey(record.SupplierId))
                {
                    continue;
                }

                var description = string.Empty;
                if (record.PurchaseRequestId.HasValue && requests.ContainsKey(record.PurchaseRequestId.Value))
                {
                    var req = requests[record.PurchaseRequestId.Value];
                    description = req.ItemDescription ?? req.Justification;
                }

                if (!MatchesText(description, tokens))
                {
                    continue;
                }

                if (!grouped.ContainsKey(record.SupplierId))
                {
                    grouped[record.SupplierId] = new List<decimal>();
                }

                grouped[record.SupplierId].Add(record.UnitCost);
            }

            var defaultCurrency = string.IsNullOrWhiteSpace(currency)
                ? ApprovalWorkflowSettingsHelper.GetDefaultCurrencyCode(_unitOfWork.Repository<SystemSetting>().GetAll())
                : currency;

            return grouped
                .Select(pair =>
                {
                    var supplier = suppliers[pair.Key];
                    var avg = pair.Value.Average();
                    return new SupplierPriceComparisonRowVm
                    {
                        SupplierId = supplier.Id,
                        SupplierName = supplier.SupplierName,
                        ItemLabel = "Historical average",
                        UnitPrice = avg,
                        Currency = defaultCurrency,
                        LeadTimeDays = supplier.DefaultLeadTimeDays,
                        IsPreferred = supplier.IsPreferred,
                        IsHistorical = true
                    };
                })
                .OrderBy(x => x.UnitPrice)
                .ToList();
        }

        private static void ApplyPriceBadges(IList<SupplierPriceComparisonRowVm> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return;
            }

            if (rows.Count == 1)
            {
                return;
            }

            rows[0].IsCheapest = true;
            rows[rows.Count - 1].IsMostExpensive = true;
        }

        private static bool MatchesOffer(
            SupplierCatalogItem offer,
            IList<string> tokens,
            int? categoryId,
            int? assetTypeId,
            int? taggedAssetId)
        {
            if (taggedAssetId.HasValue
                && offer.TaggedAssetId.HasValue
                && offer.TaggedAssetId.Value == taggedAssetId.Value)
            {
                return true;
            }

            if (assetTypeId.HasValue
                && offer.AssetTypeId.HasValue
                && offer.AssetTypeId.Value == assetTypeId.Value)
            {
                return true;
            }

            if (categoryId.HasValue && offer.AssetCategoryId.HasValue && offer.AssetCategoryId.Value == categoryId.Value)
            {
                if (assetTypeId.HasValue
                    && offer.AssetTypeId.HasValue
                    && offer.AssetTypeId.Value != assetTypeId.Value)
                {
                    return false;
                }

                return true;
            }

            return MatchesText(offer.ItemName, tokens) || MatchesText(offer.ItemDescription, tokens);
        }

        private static bool MatchesText(string text, IList<string> tokens)
        {
            if (tokens.Count == 0)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var haystack = text.ToLowerInvariant();
            return tokens.Any(token => haystack.Contains(token));
        }

        private static IList<string> Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            return text.ToLowerInvariant()
                .Split(new[] { ' ', ',', ';', '.', '-', '/', '\\', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(x => x.Length >= 3)
                .Distinct()
                .ToList();
        }

        private static string BuildItemLabel(
            SupplierCatalogItem offer,
            IDictionary<int, string> categories,
            IDictionary<int, string> assetTypes)
        {
            var label = offer.ItemName;
            if (offer.AssetTypeId.HasValue && assetTypes.ContainsKey(offer.AssetTypeId.Value))
            {
                label += " · " + assetTypes[offer.AssetTypeId.Value];
            }

            if (!string.IsNullOrWhiteSpace(offer.Sku))
            {
                return label + " (" + offer.Sku + ")";
            }

            if (offer.AssetCategoryId.HasValue && categories.ContainsKey(offer.AssetCategoryId.Value))
            {
                return label + " · " + categories[offer.AssetCategoryId.Value];
            }

            return label;
        }

        private SupplierCatalogItemVm MapItem(SupplierCatalogItem entity)
        {
            var categoryName = entity.AssetCategoryId.HasValue
                ? _unitOfWork.Repository<AssetCategory>().GetById(entity.AssetCategoryId.Value)?.Name
                : null;
            var assetTypeName = entity.AssetTypeId.HasValue
                ? _unitOfWork.Repository<AssetType>().GetById(entity.AssetTypeId.Value)?.Name
                : null;
            var taggedAsset = entity.TaggedAssetId.HasValue
                ? _unitOfWork.Repository<Asset>().GetById(entity.TaggedAssetId.Value)
                : null;
            return new SupplierCatalogItemVm
            {
                Id = entity.Id,
                SupplierId = entity.SupplierId,
                ItemName = entity.ItemName,
                ItemDescription = entity.ItemDescription,
                Sku = entity.Sku,
                AssetCategoryId = entity.AssetCategoryId,
                AssetCategoryName = categoryName,
                AssetTypeId = entity.AssetTypeId,
                AssetTypeName = assetTypeName,
                TaggedAssetId = entity.TaggedAssetId,
                TaggedAssetTag = taggedAsset?.AssetTag,
                TaggedAssetName = taggedAsset?.AssetName,
                UnitPrice = entity.UnitPrice,
                Currency = entity.Currency,
                MinimumOrderQuantity = entity.MinimumOrderQuantity,
                LeadTimeDays = entity.LeadTimeDays,
                EffectiveFrom = entity.EffectiveFrom,
                EffectiveTo = entity.EffectiveTo,
                IsActive = entity.IsActive
            };
        }

        private void EnsureSupplierExists(int supplierId)
        {
            var supplier = _unitOfWork.Repository<Supplier>().GetById(supplierId);
            if (supplier == null)
            {
                throw new BusinessException("Supplier not found.");
            }
        }

        private static void ValidateModel(SupplierCatalogItemVm model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.ItemName))
            {
                throw new BusinessException("Catalog item name is required.");
            }

            if (model.UnitPrice <= 0)
            {
                throw new BusinessException("Unit price must be greater than zero.");
            }
        }

        private int? ResolveAssetTypeId(int? categoryId, int? assetTypeId)
        {
            if (!assetTypeId.HasValue || assetTypeId.Value <= 0)
            {
                return null;
            }

            var assetType = _unitOfWork.Repository<AssetType>().GetById(assetTypeId.Value);
            if (assetType == null || !assetType.IsActive)
            {
                throw new BusinessException("Selected asset type was not found or is inactive.");
            }

            if (categoryId.HasValue && assetType.AssetCategoryId != categoryId.Value)
            {
                throw new BusinessException("Selected asset type does not belong to the chosen category.");
            }

            return assetType.Id;
        }

        private int? ResolveTaggedAssetId(int? taggedAssetId, int? categoryId, int? assetTypeId)
        {
            if (!taggedAssetId.HasValue || taggedAssetId.Value <= 0)
            {
                return null;
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(taggedAssetId.Value);
            if (asset == null || !asset.IsActive)
            {
                throw new BusinessException("Selected asset tag was not found or is inactive.");
            }

            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (organizationId.HasValue
                && asset.OrganizationId.HasValue
                && asset.OrganizationId.Value != organizationId.Value)
            {
                throw new BusinessException("Selected asset does not belong to this organization.");
            }

            if (categoryId.HasValue && asset.CategoryId != categoryId.Value)
            {
                throw new BusinessException("Tagged asset must belong to the selected category.");
            }

            if (assetTypeId.HasValue && asset.AssetTypeId != assetTypeId.Value)
            {
                throw new BusinessException("Tagged asset must match the selected asset type.");
            }

            return asset.Id;
        }

        private static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private string NormalizeCurrency(string currency)
        {
            if (string.IsNullOrWhiteSpace(currency))
            {
                return ApprovalWorkflowSettingsHelper.GetDefaultCurrencyCode(_unitOfWork.Repository<SystemSetting>().GetAll());
            }

            return currency.Trim().ToUpperInvariant();
        }
    }
}

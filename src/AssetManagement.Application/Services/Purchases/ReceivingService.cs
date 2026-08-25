using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
namespace AssetManagement.Application.Services
{
    public class ReceivingService : IReceivingService
    {
        public const string PlacementRequisitionDepartment = "RequisitionDepartment";
        public const string PlacementCompanyCustody = "CompanyCustody";

        private readonly IUnitOfWork _unitOfWork;
        private readonly IAssetSubTypeService _assetSubTypeService;
        private readonly IAssetService _assetService;
        private readonly IOutboxWriter _outboxWriter;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IAuditWriter _auditWriter;

        public ReceivingService(
            IUnitOfWork unitOfWork,
            IAssetSubTypeService assetSubTypeService,
            IAssetService assetService,
            IOutboxWriter outboxWriter,
            IOrganizationScopeService organizationScope,
            IAuditWriter auditWriter = null)
        {
            _unitOfWork = unitOfWork;
            _assetSubTypeService = assetSubTypeService;
            _assetService = assetService;
            _outboxWriter = outboxWriter;
            _organizationScope = organizationScope;
            _auditWriter = auditWriter;
        }
        public PurchaseReceiveDetailVm GetReceiveDetail(int purchaseRecordId, bool applyCatalogMatch = false)
        {
            var purchase = _unitOfWork.Repository<PurchaseRecord>().GetById(purchaseRecordId);
            if (purchase == null)
            {
                return null;
            }
            var supplierName = _unitOfWork.Repository<Supplier>().GetById(purchase.SupplierId)?.SupplierName;
            var receivings = GetReceivingsForPurchase(purchaseRecordId).ToList();
            var quantityReceived = receivings.Sum(x => x.QuantityReceived);
            var purchaseRequest = purchase.PurchaseRequestId.HasValue
                ? _unitOfWork.Repository<PurchaseRequest>().GetById(purchase.PurchaseRequestId.Value)
                : null;
            var itemDescription = purchaseRequest?.ItemDescription;
            var context = ResolveReceiveContext(purchase, purchaseRequest, applyCatalogMatch);
            var requisitionDepartmentName = ResolveDepartmentName(context.RequisitionDepartmentId);
            int? categoryId = null;
            if (context.AssetTypeId.HasValue && context.AssetTypeId.Value > 0)
            {
                categoryId = _unitOfWork.Repository<AssetType>().GetById(context.AssetTypeId.Value)?.AssetCategoryId;
            }
            return new PurchaseReceiveDetailVm
            {
                PurchaseRecordId = purchase.Id,
                PurchaseOrderNumber = purchase.PurchaseOrderNumber,
                SupplierName = supplierName,
                ItemDescription = itemDescription,
                PurchaseQuantity = purchase.Quantity,
                QuantityReceived = quantityReceived,
                RemainingQuantity = Math.Max(0, purchase.Quantity - quantityReceived),
                AssetSubTypeId = context.AssetSubTypeId,
                AssetSubTypeName = context.AssetSubTypeName,
                RequiresSubTypeAssignment = context.RequiresSubTypeAssignment,
                RequiresCatalogMatchConfirmation = context.HasPendingCatalogMatch,
                CatalogMatchAssetId = context.CatalogMatchAssetId,
                CatalogMatchLabel = context.CatalogMatchLabel,
                CatalogMatchItemName = context.CatalogMatchItemName,
                ContextCategoryId = categoryId,
                ContextAssetTypeId = context.AssetTypeId,
                ContextBrand = context.Brand,
                ContextModel = context.Model,
                SuggestedAssetName = ResolveSuggestedAssetName(itemDescription, context),
                UnitCost = purchase.UnitCost,
                Currency = purchase.Currency,
                RequisitionDepartmentId = context.RequisitionDepartmentId,
                RequisitionDepartmentName = requisitionDepartmentName,
                Receivings = receivings
            };
        }
        public ReceiveAssetLookupVm GetReceiveAssetLookup(int purchaseRecordId, int? preferredAssetId, bool applyCatalogMatch = false)
        {
            return new ReceiveAssetLookupVm();
        }
        public IEnumerable<AssetReceivingListVm> GetReceivingsForPurchase(int purchaseRecordId)
        {
            var receivings = _unitOfWork.Repository<AssetReceiving>()
                .Find(x => x.PurchaseRecordId == purchaseRecordId && x.IsActive)
                .OrderByDescending(x => x.ReceivedDate)
                .ToList();
            if (!receivings.Any())
            {
                return Enumerable.Empty<AssetReceivingListVm>();
            }
            var assetIds = receivings.Select(x => x.AssetId).Distinct().ToList();
            var assets = _unitOfWork.Repository<Asset>().GetAll()
                .Where(x => assetIds.Contains(x.Id))
                .ToDictionary(x => x.Id, x => x);
            return receivings.Select(x =>
            {
                Asset asset;
                assets.TryGetValue(x.AssetId, out asset);
                return new AssetReceivingListVm
                {
                    Id = x.Id,
                    AssetId = x.AssetId,
                    AssetTag = asset?.AssetTag,
                    AssetName = asset?.AssetName,
                    ReceivedDate = x.ReceivedDate,
                    QuantityReceived = x.QuantityReceived,
                    ConditionOnReceipt = x.ConditionOnReceipt,
                    ReceivedById = x.ReceivedById,
                    Notes = x.Notes
                };
            }).ToList();
        }
        public ReceiveResultVm Receive(AssetReceiveVm model, string receivedById)
        {
            if (model == null)
            {
                throw new BusinessException("Receive details are required.");
            }
            var purchase = _unitOfWork.Repository<PurchaseRecord>().GetById(model.PurchaseRecordId);
            if (purchase == null)
            {
                throw new BusinessException("Purchase record not found.");
            }
            if (model.QuantityReceived < 1)
            {
                throw new BusinessException("Quantity received must be at least 1.");
            }
            var purchaseRequest = purchase.PurchaseRequestId.HasValue
                ? _unitOfWork.Repository<PurchaseRequest>().GetById(purchase.PurchaseRequestId.Value)
                : null;
            ValidateRequisitionDepartment(purchaseRequest);
            ApplyReceivePlacementChoice(model);
            var context = ResolveReceiveContext(purchase, purchaseRequest, model.CatalogMatchConfirmed);
            ValidateReceivePlacementChoice(model, context);
            var subType = ResolveReceiveSubType(model, context);
            var result = ReceiveUnitTrackedAssets(model, receivedById, purchase, purchaseRequest, context, subType);
            NotifyFacilitiesRequester(purchaseRequest, context, model);
            return result;
        }
        private ReceiveResultVm ReceiveUnitTrackedAssets(
            AssetReceiveVm model,
            string receivedById,
            PurchaseRecord purchase,
            PurchaseRequest purchaseRequest,
            ReceiveContext context,
            AssetSubTypeVm subType)
        {
            if (string.IsNullOrWhiteSpace(model.ConditionOnReceipt))
            {
                throw new BusinessException("Condition on receipt is required when creating new assets.");
            }
            var units = BuildReceiveUnits(model);
            if (units.Count != model.QuantityReceived)
            {
                throw new BusinessException("Unable to prepare " + model.QuantityReceived + " unit(s) for receiving.");
            }
            if (!context.AssetTypeId.HasValue || context.AssetTypeId.Value <= 0)
            {
                throw new BusinessException("Asset type could not be resolved for this receipt. Assign an asset sub-type first.");
            }
            var assetType = _unitOfWork.Repository<AssetType>().GetById(context.AssetTypeId.Value);
            if (assetType == null)
            {
                throw new BusinessException("Asset type was not found.");
            }
            if (string.IsNullOrWhiteSpace(context.Brand) || string.IsNullOrWhiteSpace(context.Model))
            {
                throw new BusinessException("Brand and model are required to create assets at receipt.");
            }
            var receiveDepartmentId = ResolveReceiveDepartmentId(model, context);
            var itemDescription = purchaseRequest?.ItemDescription;
            var assetName = ResolveSuggestedAssetName(itemDescription, context);
            var createdAssets = new List<ReceiveCreatedAssetVm>();
            var receivingId = 0;
            _unitOfWork.ExecuteInTransaction(() =>
            {
                var remaining = _unitOfWork.GetRemainingPurchaseQuantity(purchase.Id);
                if (remaining <= 0)
                {
                    throw new BusinessException("This purchase is already fully received.");
                }
                if (model.QuantityReceived > remaining)
                {
                    throw new BusinessException("Quantity received must be between 1 and the remaining purchase quantity (" + remaining + ").");
                }
                var now = DateTime.UtcNow;
                foreach (var unit in units)
                {
                    var createModel = new AssetCreateVm
                    {
                        AssetName = assetName,
                        AssetTag = null,
                        CategoryId = assetType.AssetCategoryId,
                        AssetTypeId = assetType.Id,
                        AssetSubTypeId = subType?.Id ?? context.AssetSubTypeId,
                        Brand = context.Brand.Trim(),
                        Model = context.Model.Trim(),
                        SerialNumber = unit.SerialNumber,
                        Description = itemDescription,
                        PurchaseDate = model.ReceivedDate,
                        AcquisitionCost = purchase.UnitCost > 0 ? purchase.UnitCost : 0.01m,
                        Currency = purchase.Currency,
                        SupplierId = purchase.SupplierId > 0 ? purchase.SupplierId : (int?)null,
                        DepartmentId = receiveDepartmentId,
                        ConditionOnReceipt = model.ConditionOnReceipt,
                        CurrentStatus = AssetStatus.InStore
                    };
                    var assetId = _assetService.Create(createModel);
                    var asset = _unitOfWork.Repository<Asset>().GetById(assetId);
                    var receiving = new AssetReceiving
                    {
                        PurchaseRecordId = purchase.Id,
                        AssetId = assetId,
                        ReceivedDate = model.ReceivedDate,
                        ConditionOnReceipt = model.ConditionOnReceipt,
                        QuantityReceived = 1,
                        ReceivedById = receivedById,
                        Notes = model.Notes,
                        CreatedAt = now,
                        IsActive = true
                    };
                    _unitOfWork.Repository<AssetReceiving>().Add(receiving);
                    _unitOfWork.SaveChanges();
                    receivingId = receiving.Id;
                    createdAssets.Add(new ReceiveCreatedAssetVm
                    {
                        AssetId = assetId,
                        AssetTag = asset?.AssetTag,
                        SerialNumber = unit.SerialNumber
                    });
                }
            });
            if (_auditWriter != null && createdAssets.Count > 0)
            {
                var assetIds = string.Join(",", createdAssets.Select(x => x.AssetId));
                _auditWriter.Write(
                    "Purchases.Receive",
                    nameof(AssetReceiving),
                    receivingId > 0 ? receivingId.ToString() : null,
                    purchase.Id.ToString(),
                    assetIds);
            }

            return new ReceiveResultVm
            {
                ReceivingId = receivingId,
                CreatedAssets = createdAssets
            };
        }
        private static IList<ReceiveAssetUnitVm> BuildReceiveUnits(AssetReceiveVm model)
        {
            var quantity = model.QuantityReceived;
            var provided = model.NewAssetUnits ?? new List<ReceiveAssetUnitVm>();
            var hasAnySerial = provided.Any(x => !string.IsNullOrWhiteSpace(x?.SerialNumber));
            if (!hasAnySerial)
            {
                return Enumerable.Range(0, quantity)
                    .Select(_ => new ReceiveAssetUnitVm { SerialNumber = null })
                    .ToList();
            }
            if (provided.Count != quantity)
            {
                throw new BusinessException("Enter a serial number for each unit being received (" + quantity + " required), or leave all serial numbers blank.");
            }
            var normalized = new List<ReceiveAssetUnitVm>();
            var seenSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var unit in provided)
            {
                var serial = unit?.SerialNumber?.Trim();
                if (string.IsNullOrWhiteSpace(serial))
                {
                    throw new BusinessException("Enter a serial number for each unit being received (" + quantity + " required), or leave all serial numbers blank.");
                }
                if (!seenSerials.Add(serial))
                {
                    throw new BusinessException("Duplicate serial number in this receipt: " + serial);
                }
                normalized.Add(new ReceiveAssetUnitVm { SerialNumber = serial });
            }
            return normalized;
        }
        private static string ResolveSuggestedAssetName(string itemDescription, ReceiveContext context)
        {
            if (!string.IsNullOrWhiteSpace(itemDescription))
            {
                return itemDescription.Trim();
            }
            if (!string.IsNullOrWhiteSpace(context.AssetSubTypeName))
            {
                return context.AssetSubTypeName.Trim();
            }
            var brandModel = string.Join(" ", new[] { context.Brand, context.Model }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
            return string.IsNullOrWhiteSpace(brandModel) ? "Received asset" : brandModel;
        }
        private AssetSubTypeVm ResolveReceiveSubType(AssetReceiveVm model, ReceiveContext context)
        {
            if (model.AssetSubTypeId.HasValue && model.AssetSubTypeId.Value > 0)
            {
                var assigned = _assetSubTypeService.GetById(model.AssetSubTypeId.Value);
                if (assigned == null)
                {
                    throw new BusinessException("Selected asset sub-type was not found.");
                }
                return assigned;
            }
            if (context.AssetSubTypeId.HasValue && context.AssetSubTypeId.Value > 0)
            {
                var inferred = _assetSubTypeService.GetById(context.AssetSubTypeId.Value);
                if (inferred != null)
                {
                    return inferred;
                }
            }
            if (context.RequiresSubTypeAssignment)
            {
                throw new BusinessException("Assign an asset sub-type before recording this receipt.");
            }
            return null;
        }
        private static void ApplyReceivePlacementChoice(AssetReceiveVm model)
        {
            if (model == null)
            {
                return;
            }

            if (string.Equals(model.ReceivePlacementChoice, PlacementRequisitionDepartment, StringComparison.OrdinalIgnoreCase))
            {
                model.AssignToRequisitionDepartment = true;
                return;
            }

            if (string.Equals(model.ReceivePlacementChoice, PlacementCompanyCustody, StringComparison.OrdinalIgnoreCase))
            {
                model.AssignToRequisitionDepartment = false;
            }
        }

        private void ValidateRequisitionDepartment(PurchaseRequest purchaseRequest)
        {
            if (purchaseRequest == null)
            {
                return;
            }

            if (purchaseRequest.DepartmentId <= 0)
            {
                throw new BusinessException("Linked requisition must have a target department before goods can be received.");
            }

            var department = _unitOfWork.Repository<Department>().GetById(purchaseRequest.DepartmentId);
            if (department == null || !department.IsActive)
            {
                throw new BusinessException("Requisition target department was not found.");
            }

            if (!department.IsRequisitionTarget)
            {
                throw new BusinessException("Requisition target must be a leaf department (class or admin unit).");
            }
        }

        private void ValidateReceivePlacementChoice(AssetReceiveVm model, ReceiveContext context)
        {
            if (context == null || !context.RequisitionDepartmentId.HasValue || context.RequisitionDepartmentId.Value <= 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(model.ReceivePlacementChoice))
            {
                throw new BusinessException("Choose whether received goods go to the requisition department or company custody.");
            }

            if (!string.Equals(model.ReceivePlacementChoice, PlacementRequisitionDepartment, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(model.ReceivePlacementChoice, PlacementCompanyCustody, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("Invalid receive placement choice.");
            }
        }

        private void NotifyFacilitiesRequester(PurchaseRequest purchaseRequest, ReceiveContext context, AssetReceiveVm model)
        {
            if (purchaseRequest == null || string.IsNullOrWhiteSpace(purchaseRequest.RequestedById))
            {
                return;
            }

            var departmentName = ResolveDepartmentName(context.RequisitionDepartmentId);
            string subject;
            string message;
            if (model.AssignToRequisitionDepartment && !string.IsNullOrWhiteSpace(departmentName))
            {
                subject = "Goods received and placed";
                message = "Goods were received and placed in " + departmentName + "; ready for handover.";
            }
            else
            {
                subject = "Goods received under company custody";
                message = "Goods were received under company custody; assign when ready.";
                if (!string.IsNullOrWhiteSpace(departmentName))
                {
                    message += " Intended destination: " + departmentName + ".";
                }
            }

            NotificationHelper.AddNotification(
                _unitOfWork,
                _outboxWriter,
                _organizationScope,
                purchaseRequest.RequestedById,
                NotificationType.General,
                subject,
                message,
                "/PurchaseRequests/Details/" + purchaseRequest.Id);
            _unitOfWork.SaveChanges();
        }

        private static int? ResolveReceiveDepartmentId(AssetReceiveVm model, ReceiveContext context)
        {
            if (model != null
                && model.AssignToRequisitionDepartment
                && context != null
                && context.RequisitionDepartmentId.HasValue
                && context.RequisitionDepartmentId.Value > 0)
            {
                return context.RequisitionDepartmentId;
            }

            return null;
        }

        private string ResolveDepartmentName(int? departmentId)
        {
            if (!departmentId.HasValue || departmentId.Value <= 0)
            {
                return null;
            }
            return _unitOfWork.Repository<Department>().GetById(departmentId.Value)?.Name;
        }
        private ReceiveContext ResolveReceiveContext(PurchaseRecord purchase, PurchaseRequest purchaseRequest, bool applyCatalogMatch)
        {
            var context = new ReceiveContext
            {
                RequisitionDepartmentId = purchaseRequest != null && purchaseRequest.DepartmentId > 0
                    ? (int?)purchaseRequest.DepartmentId
                    : null
            };
            if (purchaseRequest != null && purchaseRequest.TargetAssetId.HasValue && purchaseRequest.TargetAssetId.Value > 0)
            {
                ApplyAssetContext(context, _unitOfWork.Repository<Asset>().GetById(purchaseRequest.TargetAssetId.Value));
                return context;
            }
            var catalogMatch = ResolveCatalogReferenceAsset(purchase, purchaseRequest?.ItemDescription);
            if (catalogMatch != null)
            {
                if (applyCatalogMatch)
                {
                    ApplyAssetContext(context, catalogMatch.Asset);
                    return context;
                }
                context.HasPendingCatalogMatch = true;
                context.CatalogMatchAssetId = catalogMatch.Asset.Id;
                context.CatalogMatchLabel = catalogMatch.Asset.AssetTag + " - " + catalogMatch.Asset.AssetName;
                context.CatalogMatchItemName = catalogMatch.ItemName;
                return context;
            }
            if (context.AssetTypeId.HasValue
                && (!string.IsNullOrWhiteSpace(context.Brand) || !string.IsNullOrWhiteSpace(context.Model)))
            {
                var resolver = new AssetSubTypeResolver(_assetSubTypeService);
                var resolution = resolver.Resolve(
                    context.AssetTypeId.Value,
                    context.Brand,
                    context.Model,
                    context.AssetSubTypeId);
                if (resolution.IsMatched)
                {
                    ApplySubTypeContext(context, resolution.SubType);
                }
                else if (resolution.RequiresAssignment)
                {
                    context.RequiresSubTypeAssignment = true;
                    context.Brand = resolution.Brand;
                    context.Model = resolution.Model;
                }
            }
            else
            {
                context.RequiresSubTypeAssignment = true;
            }
            return context;
        }
        private CatalogMatchCandidate ResolveCatalogReferenceAsset(PurchaseRecord purchase, string itemDescription)
        {
            if (purchase == null || string.IsNullOrWhiteSpace(itemDescription))
            {
                return null;
            }
            var needle = itemDescription.Trim();
            var catalogItems = _unitOfWork.Repository<SupplierCatalogItem>().GetAll()
                .Where(x => x.IsActive && x.SupplierId == purchase.SupplierId)
                .ToList();
            SupplierCatalogItem match = catalogItems.FirstOrDefault(x =>
                    x.TaggedAssetId.HasValue
                    && ContainsIgnoreCase(x.ItemName, needle))
                ?? catalogItems.FirstOrDefault(x =>
                    x.TaggedAssetId.HasValue
                    && ContainsIgnoreCase(x.ItemDescription, needle));
            if (match == null || !match.TaggedAssetId.HasValue)
            {
                return null;
            }
            var asset = _unitOfWork.Repository<Asset>().GetById(match.TaggedAssetId.Value);
            if (asset == null || !asset.IsActive)
            {
                return null;
            }
            return new CatalogMatchCandidate
            {
                Asset = asset,
                ItemName = string.IsNullOrWhiteSpace(match.ItemName) ? match.ItemDescription : match.ItemName
            };
        }
        private void ApplyAssetContext(ReceiveContext context, Asset asset)
        {
            if (asset == null)
            {
                return;
            }
            context.AssetTypeId = asset.AssetTypeId;
            context.Brand = asset.Brand;
            context.Model = asset.Model;
            if (asset.AssetSubTypeId.HasValue && asset.AssetSubTypeId.Value > 0)
            {
                var subType = _assetSubTypeService.GetById(asset.AssetSubTypeId.Value);
                if (subType != null)
                {
                    ApplySubTypeContext(context, subType);
                }
            }
        }
        private static void ApplySubTypeContext(ReceiveContext context, AssetSubTypeVm subType)
        {
            if (subType == null)
            {
                return;
            }
            context.AssetSubTypeId = subType.Id;
            context.AssetSubTypeName = subType.Name;
            context.AssetTypeId = subType.AssetTypeId;
            context.Brand = subType.Brand;
            context.Model = subType.Model;
            context.RequiresSubTypeAssignment = false;
        }
        private static bool ContainsIgnoreCase(string haystack, string needle)
        {
            return !string.IsNullOrWhiteSpace(haystack)
                && !string.IsNullOrWhiteSpace(needle)
                && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        private sealed class ReceiveContext
        {
            public int? AssetSubTypeId { get; set; }
            public string AssetSubTypeName { get; set; }
            public bool RequiresSubTypeAssignment { get; set; }
            public bool HasPendingCatalogMatch { get; set; }
            public int? CatalogMatchAssetId { get; set; }
            public string CatalogMatchLabel { get; set; }
            public string CatalogMatchItemName { get; set; }
            public int? AssetTypeId { get; set; }
            public string Brand { get; set; }
            public string Model { get; set; }
            public int? RequisitionDepartmentId { get; set; }
        }
        private sealed class CatalogMatchCandidate
        {
            public Asset Asset { get; set; }
            public string ItemName { get; set; }
        }
    }
}

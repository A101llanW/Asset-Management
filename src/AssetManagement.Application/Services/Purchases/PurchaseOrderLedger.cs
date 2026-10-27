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
    /// <summary>Resolved classification of a PO line (what Receiving shows read-only).</summary>
    public sealed class PurchaseLineClassification
    {
        public int? AssetCategoryId { get; set; }

        public int? AssetTypeId { get; set; }

        public int? AssetSubTypeId { get; set; }

        public string Brand { get; set; }

        public string Model { get; set; }

        public string Source { get; set; }

        public bool HasType
        {
            get { return AssetTypeId.HasValue && AssetTypeId.Value > 0; }
        }
    }

    /// <summary>C3: a classification changed with Edit (old = the prefill / PO line value, new = the validated choice).</summary>
    public sealed class PurchaseLineClassificationEdit
    {
        public PurchaseOrderLine Line { get; set; }

        public PurchaseLineClassification Before { get; set; }

        public PurchaseLineClassification After { get; set; }
    }

    /// <summary>
    /// rel1009i: ordered / received / outstanding per requisition, PO and PO line (migration 098), and the
    /// classification a PO line resolves to. Shared by PurchaseService (ordering) and ReceivingService (receiving).
    /// Legacy POs (no PurchaseOrderLine rows) are one virtual line (Id 0) received against the PO quantity.
    /// </summary>
    public class PurchaseOrderLedger
    {
        public const string SourcePurchaseOrderLine = "PurchaseOrderLine";
        public const string SourceRequisitionAsset = "RequisitionAsset";
        /// <summary>Migration 108: the type / sub-type chosen on the requisition line (untagged lines must have one).</summary>
        public const string SourceRequisitionLine = "RequisitionLine";
        public const string SourceSelectedOnOrder = "SelectedOnOrder";
        /// <summary>Migration 108 (C3, Allan 2026-10-27): the buyer changed the requisition's prefilled classification on the PO.</summary>
        public const string SourceEditedOnOrder = "EditedOnOrder";
        /// <summary>C3: the receiver changed the PO line classification with Edit on the Receive page (asset only; the PO line keeps its value).</summary>
        public const string SourceEditedAtReceiving = "EditedAtReceiving";
        /// <summary>Audit action for a classification changed from the requisition prefill (PO) or from the PO line (receiving).</summary>
        public const string AuditActionClassificationEditedOnOrder = "Purchases.ClassificationEdited";
        public const string AuditActionClassificationEditedAtReceiving = "Purchases.Receive.ClassificationEdited";
        public const string SourceSupplierCatalog = "SupplierCatalog";

        private readonly IUnitOfWork _unitOfWork;

        public PurchaseOrderLedger(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public static string DescribeSource(string source)
        {
            switch (source)
            {
                case SourceRequisitionAsset:
                    return "Requisition's existing asset";
                case SourceRequisitionLine:
                    return "Requisition line";
                case SourceSelectedOnOrder:
                    return "Purchase order line";
                case SourceEditedOnOrder:
                    return "Purchase order line (changed from the requisition)";
                case SourceEditedAtReceiving:
                    return "Changed at receiving";
                case SourceSupplierCatalog:
                    return "Supplier catalog item";
                case SourcePurchaseOrderLine:
                    return "Purchase order line";
                default:
                    return null;
            }
        }

        public IList<PurchaseOrderLine> GetLines(int purchaseRecordId)
        {
            try
            {
                return _unitOfWork.Repository<PurchaseOrderLine>()
                    .Find(x => x.PurchaseRecordId == purchaseRecordId && x.IsActive)
                    .OrderBy(x => x.LineNumber)
                    .ToList();
            }
            catch (Exception ex)
            {
                if (PurchaseOrderQuantityRules.IsMissingTable(ex))
                {
                    return new List<PurchaseOrderLine>(); // 098 not applied: every PO is legacy.
                }

                throw;
            }
        }

        public IDictionary<int, int> ReceivedByLine(int purchaseRecordId)
        {
            try
            {
                return _unitOfWork.Repository<PurchaseOrderLineReceipt>()
                    .Find(x => x.PurchaseRecordId == purchaseRecordId && x.IsActive)
                    .GroupBy(x => x.PurchaseOrderLineId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.QuantityReceived));
            }
            catch (Exception ex)
            {
                if (PurchaseOrderQuantityRules.IsMissingTable(ex))
                {
                    return new Dictionary<int, int>();
                }

                throw;
            }
        }

        public int ReceivedForPurchase(int purchaseRecordId)
        {
            return _unitOfWork.Repository<AssetReceiving>()
                .Find(x => x.PurchaseRecordId == purchaseRecordId && x.IsActive)
                .Sum(x => x.QuantityReceived);
        }

        public IList<PurchaseRequestLine> GetRequisitionLines(int purchaseRequestId)
        {
            return _unitOfWork.Repository<PurchaseRequestLine>()
                .Find(x => x.PurchaseRequestId == purchaseRequestId && x.IsActive)
                .OrderBy(x => x.LineNumber)
                .ToList();
        }

        public int RequisitionTotal(PurchaseRequest request, IList<PurchaseRequestLine> lines)
        {
            if (lines != null && lines.Count > 0)
            {
                return lines.Sum(x => Math.Max(0, x.Quantity));
            }

            return request == null ? 0 : Math.Max(0, request.Quantity);
        }

        public IList<PurchaseRecord> GetOrdersForRequisition(int purchaseRequestId)
        {
            return _unitOfWork.Repository<PurchaseRecord>()
                .Find(x => x.PurchaseRequestId == purchaseRequestId && x.IsActive)
                .OrderBy(x => x.PurchaseDate)
                .ThenBy(x => x.Id)
                .ToList();
        }

        public int OrderedForRequisition(int purchaseRequestId)
        {
            return GetOrdersForRequisition(purchaseRequestId).Sum(x => x.Quantity);
        }

        /// <summary>Quantity already on PO lines for each requisition line (all POs of the requisition).</summary>
        public IDictionary<int, int> OrderedByRequisitionLine(int purchaseRequestId)
        {
            var orderIds = GetOrdersForRequisition(purchaseRequestId).Select(x => x.Id).ToList();
            var result = new Dictionary<int, int>();
            if (orderIds.Count == 0)
            {
                return result;
            }

            foreach (var orderId in orderIds)
            {
                foreach (var line in GetLines(orderId).Where(x => x.PurchaseRequestLineId.HasValue))
                {
                    int current;
                    result.TryGetValue(line.PurchaseRequestLineId.Value, out current);
                    result[line.PurchaseRequestLineId.Value] = current + line.Quantity;
                }
            }

            return result;
        }

        /// <summary>The requisition's lines with requisition / already ordered / outstanding quantities (PO Create prefill).</summary>
        public IList<PurchaseOrderLineInputVm> BuildRequisitionDraft(PurchaseRequest request)
        {
            var result = new List<PurchaseOrderLineInputVm>();
            if (request == null)
            {
                return result;
            }

            var lines = GetRequisitionLines(request.Id);
            var total = RequisitionTotal(request, lines);
            var orderedTotal = OrderedForRequisition(request.Id);
            var outstandingTotal = PurchaseOrderQuantityRules.Outstanding(total, orderedTotal);
            if (lines.Count == 0)
            {
                result.Add(new PurchaseOrderLineInputVm
                {
                    PurchaseRequestLineId = null,
                    LineNumber = 1,
                    Description = request.ItemDescription,
                    RequisitionQuantity = total,
                    AlreadyOrdered = orderedTotal,
                    Outstanding = outstandingTotal,
                    Quantity = outstandingTotal
                });
                return result;
            }

            var orderedByLine = OrderedByRequisitionLine(request.Id);
            foreach (var line in lines)
            {
                int ordered;
                orderedByLine.TryGetValue(line.Id, out ordered);
                var outstanding = Math.Min(PurchaseOrderQuantityRules.Outstanding(line.Quantity, ordered), outstandingTotal);
                result.Add(new PurchaseOrderLineInputVm
                {
                    PurchaseRequestLineId = line.Id,
                    LineNumber = line.LineNumber,
                    ItemName = line.ItemName,
                    Description = line.Description,
                    RequisitionQuantity = line.Quantity,
                    AlreadyOrdered = ordered,
                    Outstanding = outstanding,
                    Quantity = outstanding
                });
            }

            return result;
        }

        public PurchaseLineClassification ResolveClassification(PurchaseRecord purchase, PurchaseRequest request, PurchaseOrderLine line, string description)
        {
            if (line != null && line.AssetTypeId.HasValue && line.AssetTypeId.Value > 0)
            {
                return Complete(new PurchaseLineClassification
                {
                    AssetTypeId = line.AssetTypeId,
                    AssetSubTypeId = line.AssetSubTypeId,
                    Source = line.ClassificationSource ?? SourcePurchaseOrderLine
                });
            }

            // Migration 108: a PO line saved without a type (older PO, or the PO was raised before the requisition
            // line carried one) still gets the requisition line's own tag / type before the header-level fallbacks.
            var requisitionLine = GetRequisitionLine(line);
            var prefill = PrefillClassification(request, requisitionLine);
            if (prefill != null)
            {
                return prefill;
            }

            return FromSupplierCatalog(purchase == null ? 0 : purchase.SupplierId, string.IsNullOrWhiteSpace(description) ? request?.ItemDescription : description)
                ?? new PurchaseLineClassification();
        }

        /// <summary>The requisition line a PO line was raised from (null for legacy / unlinked lines).</summary>
        public PurchaseRequestLine GetRequisitionLine(PurchaseOrderLine line)
        {
            if (line == null || !line.PurchaseRequestLineId.HasValue || line.PurchaseRequestLineId.Value <= 0)
            {
                return null;
            }

            return _unitOfWork.Repository<PurchaseRequestLine>().GetById(line.PurchaseRequestLineId.Value);
        }

        /// <summary>
        /// Migration 108: the classification the requisition PRE-FILLS for a line - the line's stored (approved) type / sub-type,
        /// then the line's tagged asset, then (legacy requisitions) the header's tagged asset. Null when none of these is set.
        /// C3 (Allan 2026-10-27): this is a prefill, not a lock - the PO and receiving show it read-only with an Edit button,
        /// and a changed classification is validated (<see cref="ValidateEditedClassification"/>) and audit-logged.
        /// </summary>
        public PurchaseLineClassification PrefillClassification(PurchaseRequest request, PurchaseRequestLine requisitionLine)
        {
            return FromRequisitionLine(requisitionLine) ?? FromRequisitionAsset(request);
        }

        /// <summary>
        /// C3: validates a classification chosen with Edit (PO line or receiving line). The type must exist, be active and
        /// belong to the organisation (or be shared, OrganizationId NULL); the sub-type is REQUIRED, must be active, in the
        /// organisation and belong to the type. The category is always derived from the type (never posted).
        /// </summary>
        public PurchaseLineClassification ValidateEditedClassification(int? organizationId, int? assetTypeId, int? assetSubTypeId, string label, string source)
        {
            var suffix = string.IsNullOrWhiteSpace(label) ? "." : " (" + label + ").";
            if (!assetTypeId.HasValue || assetTypeId.Value <= 0)
            {
                throw new BusinessException("Choose the asset type" + suffix);
            }

            var type = _unitOfWork.Repository<AssetType>().GetById(assetTypeId.Value);
            if (type == null || !type.IsActive || !InOrganization(type.OrganizationId, organizationId))
            {
                throw new BusinessException("The selected asset type was not found" + suffix);
            }

            if (!assetSubTypeId.HasValue || assetSubTypeId.Value <= 0)
            {
                if (!TypeHasActiveSubTypes(type.Id))
                {
                    throw new BusinessException("Choose the sub-type" + suffix.TrimEnd('.') + ": this asset type has no active sub-types yet. Add one under Asset Categories first; every received asset needs a sub-type.");
                }

                throw new BusinessException("Choose the sub-type" + suffix.TrimEnd('.') + ": a sub-type is required.");
            }

            var subType = _unitOfWork.Repository<AssetSubType>().GetById(assetSubTypeId.Value);
            if (subType == null || !InOrganization(subType.OrganizationId, organizationId))
            {
                throw new BusinessException("The selected sub-type was not found" + suffix);
            }

            if (subType.AssetTypeId != type.Id)
            {
                throw new BusinessException("The selected sub-type does not belong to the selected asset type" + suffix);
            }

            if (!subType.IsActive)
            {
                throw new BusinessException("The selected sub-type is inactive" + suffix);
            }

            return Complete(new PurchaseLineClassification { AssetTypeId = type.Id, AssetSubTypeId = subType.Id, Source = source });
        }

        /// <summary>True when both have the same type and sub-type (the category follows the type).</summary>
        public static bool SameClassification(PurchaseLineClassification a, PurchaseLineClassification b)
        {
            var aType = a == null || !a.HasType ? (int?)null : a.AssetTypeId;
            var bType = b == null || !b.HasType ? (int?)null : b.AssetTypeId;
            var aSub = a == null || !a.AssetSubTypeId.HasValue || a.AssetSubTypeId.Value <= 0 ? (int?)null : a.AssetSubTypeId;
            var bSub = b == null || !b.AssetSubTypeId.HasValue || b.AssetSubTypeId.Value <= 0 ? (int?)null : b.AssetSubTypeId;
            return aType == bType && aSub == bSub;
        }

        /// <summary>Audit text: "Category (#id) > Type (#id) > Sub-type (#id)", or "none".</summary>
        /// <summary>C3 override trail (migration 108) on a PO line: who / when (UTC) / the classification it replaced.</summary>
        public static void StampOverride(PurchaseOrderLine line, PurchaseLineClassification before, string userId, DateTime atUtc)
        {
            before = before ?? new PurchaseLineClassification();
            line.ClassificationOverriddenById = userId;
            line.ClassificationOverriddenAt = atUtc;
            line.OriginalAssetCategoryId = before.AssetCategoryId;
            line.OriginalAssetTypeId = before.AssetTypeId;
            line.OriginalAssetSubTypeId = before.AssetSubTypeId;
        }

        /// <summary>C3 override trail (migration 108) on a receipt: "original" = the PO line's classification.</summary>
        public static void StampOverride(PurchaseOrderLineReceipt receipt, PurchaseLineClassification before, string userId, DateTime atUtc)
        {
            before = before ?? new PurchaseLineClassification();
            receipt.ClassificationOverriddenById = userId;
            receipt.ClassificationOverriddenAt = atUtc;
            receipt.OriginalAssetCategoryId = before.AssetCategoryId;
            receipt.OriginalAssetTypeId = before.AssetTypeId;
            receipt.OriginalAssetSubTypeId = before.AssetSubTypeId;
        }

        /// <summary>"Category > Type > Sub-type" by name (missing parts skipped); null when nothing is set.
        /// Without a category id the type's category is used.</summary>
        public string DescribeNames(int? categoryId, int? typeId, int? subTypeId)
        {
            if ((!categoryId.HasValue || categoryId.Value <= 0) && typeId.HasValue && typeId.Value > 0)
            {
                categoryId = _unitOfWork.Repository<AssetType>().GetById(typeId.Value)?.AssetCategoryId;
            }

            var parts = new List<string>();
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                parts.Add(_unitOfWork.Repository<AssetCategory>().GetById(categoryId.Value)?.Name ?? "#" + categoryId.Value);
            }

            if (typeId.HasValue && typeId.Value > 0)
            {
                parts.Add(_unitOfWork.Repository<AssetType>().GetById(typeId.Value)?.Name ?? "#" + typeId.Value);
            }

            if (subTypeId.HasValue && subTypeId.Value > 0)
            {
                parts.Add(_unitOfWork.Repository<AssetSubType>().GetById(subTypeId.Value)?.Name ?? "#" + subTypeId.Value);
            }

            return parts.Count == 0 ? null : string.Join(" > ", parts);
        }

        public string DescribeForAudit(PurchaseLineClassification classification)
        {
            if (classification == null || !classification.HasType)
            {
                return "none";
            }

            var parts = new List<string>();
            if (classification.AssetCategoryId.HasValue)
            {
                parts.Add(NameOf(_unitOfWork.Repository<AssetCategory>().GetById(classification.AssetCategoryId.Value)?.Name, classification.AssetCategoryId.Value));
            }

            parts.Add(NameOf(_unitOfWork.Repository<AssetType>().GetById(classification.AssetTypeId.Value)?.Name, classification.AssetTypeId.Value));
            parts.Add(classification.AssetSubTypeId.HasValue
                ? NameOf(_unitOfWork.Repository<AssetSubType>().GetById(classification.AssetSubTypeId.Value)?.Name, classification.AssetSubTypeId.Value)
                : "no sub-type");
            return string.Join(" > ", parts);
        }

        private static string NameOf(string name, int id)
        {
            return (string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()) + " (#" + id + ")";
        }

        /// <summary>A shared row (OrganizationId NULL) is in every organisation. Callers pass a resolved organisation
        /// (the record's, else the current tenant's); only when neither is known is the row accepted.</summary>
        internal static bool InOrganization(int? rowOrganizationId, int? organizationId)
        {
            return !rowOrganizationId.HasValue || !organizationId.HasValue || rowOrganizationId.Value == organizationId.Value;
        }

        /// <summary>
        /// Migration 108: the STORED (approved) line type / sub-type first - for a tagged line that is the tagged asset's
        /// classification at submit time, so reclassifying the asset after approval does not change the PO
        /// (Source RequisitionAsset when tagged, RequisitionLine otherwise). Only a line with no stored type falls back
        /// to the tagged asset's current classification.
        /// </summary>
        public PurchaseLineClassification FromRequisitionLine(PurchaseRequestLine requisitionLine)
        {
            if (requisitionLine == null)
            {
                return null;
            }

            var tagged = requisitionLine.TargetAssetId.HasValue && requisitionLine.TargetAssetId.Value > 0;
            var asset = tagged ? _unitOfWork.Repository<Asset>().GetById(requisitionLine.TargetAssetId.Value) : null;

            if (requisitionLine.AssetTypeId.HasValue && requisitionLine.AssetTypeId.Value > 0)
            {
                // Brand / model only describe the stored classification when the asset still has it.
                var assetStillMatches = asset != null
                    && asset.AssetTypeId == requisitionLine.AssetTypeId.Value
                    && asset.AssetSubTypeId == requisitionLine.AssetSubTypeId;
                var stored = Complete(new PurchaseLineClassification
                {
                    AssetTypeId = requisitionLine.AssetTypeId,
                    AssetSubTypeId = requisitionLine.AssetSubTypeId,
                    Brand = assetStillMatches ? asset.Brand : null,
                    Model = assetStillMatches ? asset.Model : null,
                    Source = tagged ? SourceRequisitionAsset : SourceRequisitionLine
                });
                if (stored != null && stored.HasType)
                {
                    return stored;
                }
            }

            if (asset != null && asset.AssetTypeId > 0)
            {
                return Complete(new PurchaseLineClassification
                {
                    AssetTypeId = asset.AssetTypeId,
                    AssetSubTypeId = asset.AssetSubTypeId,
                    Brand = asset.Brand,
                    Model = asset.Model,
                    Source = SourceRequisitionAsset
                });
            }

            return null;
        }

        /// <summary>
        /// Migration 108: the name the received asset should get by default - the PO line's ItemName, then its
        /// requisition line's ItemName, then (legacy PO without lines) the single requisition line's ItemName.
        /// Null when none is set (older rows): callers fall back to description / sub-type / brand-model.
        /// </summary>
        public string ResolveItemName(PurchaseRequest request, PurchaseOrderLine line)
        {
            var name = AssetNameRules.Normalize(line == null ? null : line.ItemName);
            if (name != null)
            {
                return name;
            }

            var requisitionLine = GetRequisitionLine(line);
            if (requisitionLine == null && line == null && request != null)
            {
                var lines = GetRequisitionLines(request.Id);
                requisitionLine = lines.Count == 1 ? lines[0] : null;
            }

            return AssetNameRules.Normalize(requisitionLine == null ? null : requisitionLine.ItemName);
        }

        public PurchaseLineClassification FromRequisitionAsset(PurchaseRequest request)
        {
            if (request == null || !request.TargetAssetId.HasValue || request.TargetAssetId.Value <= 0)
            {
                return null;
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(request.TargetAssetId.Value);
            if (asset == null || asset.AssetTypeId <= 0)
            {
                return null;
            }

            return Complete(new PurchaseLineClassification
            {
                AssetTypeId = asset.AssetTypeId,
                AssetSubTypeId = asset.AssetSubTypeId,
                Brand = asset.Brand,
                Model = asset.Model,
                Source = SourceRequisitionAsset
            });
        }

        public PurchaseLineClassification FromSupplierCatalog(int supplierId, string description)
        {
            if (supplierId <= 0 || string.IsNullOrWhiteSpace(description))
            {
                return null;
            }

            var needle = description.Trim();
            var items = _unitOfWork.Repository<SupplierCatalogItem>()
                .Find(x => x.IsActive && x.SupplierId == supplierId)
                .ToList();
            var match = items.FirstOrDefault(x => Matches(x.ItemName, needle))
                ?? items.FirstOrDefault(x => Matches(x.ItemDescription, needle));
            if (match == null)
            {
                return null;
            }

            if (match.TaggedAssetId.HasValue)
            {
                var asset = _unitOfWork.Repository<Asset>().GetById(match.TaggedAssetId.Value);
                if (asset != null && asset.IsActive && asset.AssetTypeId > 0)
                {
                    return Complete(new PurchaseLineClassification
                    {
                        AssetTypeId = asset.AssetTypeId,
                        AssetSubTypeId = asset.AssetSubTypeId,
                        Brand = asset.Brand,
                        Model = asset.Model,
                        Source = SourceSupplierCatalog
                    });
                }
            }

            if (match.AssetTypeId.HasValue && match.AssetTypeId.Value > 0)
            {
                return Complete(new PurchaseLineClassification { AssetTypeId = match.AssetTypeId, Source = SourceSupplierCatalog });
            }

            return null;
        }

        /// <summary>True when the type has at least one active sub-type (so receiving must choose one).</summary>
        public bool TypeHasActiveSubTypes(int assetTypeId)
        {
            return assetTypeId > 0
                && _unitOfWork.Repository<AssetSubType>().Find(x => x.AssetTypeId == assetTypeId && x.IsActive).Any();
        }

        /// <summary>Fills the category from the type and drops a sub-type that does not belong to the type.</summary>
        public PurchaseLineClassification Complete(PurchaseLineClassification classification)
        {
            if (classification == null || !classification.HasType)
            {
                return classification;
            }

            var type = _unitOfWork.Repository<AssetType>().GetById(classification.AssetTypeId.Value);
            if (type == null)
            {
                return new PurchaseLineClassification();
            }

            classification.AssetCategoryId = type.AssetCategoryId;
            if (classification.AssetSubTypeId.HasValue && classification.AssetSubTypeId.Value > 0)
            {
                var subType = _unitOfWork.Repository<AssetSubType>().GetById(classification.AssetSubTypeId.Value);
                if (subType == null || subType.AssetTypeId != type.Id)
                {
                    classification.AssetSubTypeId = null;
                }
                else
                {
                    classification.Brand = string.IsNullOrWhiteSpace(subType.Brand) ? classification.Brand : subType.Brand;
                    classification.Model = string.IsNullOrWhiteSpace(subType.Model) ? classification.Model : subType.Model;
                }
            }
            else
            {
                classification.AssetSubTypeId = null;
            }

            return classification;
        }

        public PurchaseOrderStatusVm BuildStatus(PurchaseRecord purchase)
        {
            if (purchase == null)
            {
                return null;
            }

            var request = purchase.PurchaseRequestId.HasValue
                ? _unitOfWork.Repository<PurchaseRequest>().GetById(purchase.PurchaseRequestId.Value)
                : null;
            var lines = GetLines(purchase.Id);
            var status = new PurchaseOrderStatusVm
            {
                PurchaseRecordId = purchase.Id,
                PurchaseRequestId = purchase.PurchaseRequestId,
                PurchaseRequestNumber = request?.RequestNumber,
                IsLegacy = lines.Count == 0
            };

            if (lines.Count == 0)
            {
                var received = ReceivedForPurchase(purchase.Id);
                status.Lines.Add(BuildLineStatus(purchase, request, null, 0, request?.ItemDescription, purchase.Quantity, received, purchase.UnitCost, 1));
            }
            else
            {
                var receivedByLine = ReceivedByLine(purchase.Id);
                foreach (var line in lines)
                {
                    int received;
                    receivedByLine.TryGetValue(line.Id, out received);
                    var vm = BuildLineStatus(purchase, request, line, line.Id, line.Description, line.Quantity, received, line.UnitCost, line.LineNumber);
                    vm.PurchaseRequestLineId = line.PurchaseRequestLineId;
                    status.Lines.Add(vm);
                }
            }

            status.Ordered = status.Lines.Sum(x => x.Ordered);
            status.Received = status.Lines.Sum(x => x.Received);
            status.Outstanding = status.Lines.Sum(x => x.Outstanding);
            status.Status = PurchaseOrderQuantityRules.ReceiptStatus(status.Ordered, status.Received);
            return status;
        }

        private PurchaseOrderLineStatusVm BuildLineStatus(
            PurchaseRecord purchase,
            PurchaseRequest request,
            PurchaseOrderLine line,
            int id,
            string description,
            int ordered,
            int received,
            decimal unitCost,
            int lineNumber)
        {
            var classification = ResolveClassification(purchase, request, line, description) ?? new PurchaseLineClassification();
            return new PurchaseOrderLineStatusVm
            {
                Id = id,
                LineNumber = lineNumber,
                ItemName = ResolveItemName(request, line),
                Description = string.IsNullOrWhiteSpace(description) ? "Purchased item" : description,
                Ordered = ordered,
                Received = received,
                Outstanding = PurchaseOrderQuantityRules.Outstanding(ordered, received),
                UnitCost = unitCost,
                AssetCategoryId = classification.AssetCategoryId,
                AssetCategoryName = classification.AssetCategoryId.HasValue ? _unitOfWork.Repository<AssetCategory>().GetById(classification.AssetCategoryId.Value)?.Name : null,
                AssetTypeId = classification.AssetTypeId,
                AssetTypeName = classification.AssetTypeId.HasValue ? _unitOfWork.Repository<AssetType>().GetById(classification.AssetTypeId.Value)?.Name : null,
                AssetSubTypeId = classification.AssetSubTypeId,
                AssetSubTypeName = classification.AssetSubTypeId.HasValue ? _unitOfWork.Repository<AssetSubType>().GetById(classification.AssetSubTypeId.Value)?.Name : null,
                Brand = classification.Brand,
                Model = classification.Model,
                ClassificationSource = classification.HasType ? classification.Source : null,
                ClassificationOverride = line != null && line.ClassificationOverriddenAt.HasValue
                    ? new ClassificationOverrideVm
                    {
                        ChangedById = line.ClassificationOverriddenById,
                        ChangedAtUtc = line.ClassificationOverriddenAt,
                        OldClassification = DescribeNames(line.OriginalAssetCategoryId, line.OriginalAssetTypeId, line.OriginalAssetSubTypeId),
                        NewClassification = DescribeNames(line.AssetCategoryId, line.AssetTypeId, line.AssetSubTypeId)
                    }
                    : null
            };
        }

        public RequisitionOrderSummaryVm BuildRequisitionSummary(PurchaseRequest request)
        {
            if (request == null)
            {
                return null;
            }

            var lines = GetRequisitionLines(request.Id);
            var total = RequisitionTotal(request, lines);
            var summary = new RequisitionOrderSummaryVm
            {
                PurchaseRequestId = request.Id,
                RequisitionQuantity = total
            };

            foreach (var order in GetOrdersForRequisition(request.Id))
            {
                var status = BuildStatus(order);
                summary.Orders.Add(new RequisitionLinkedOrderVm
                {
                    PurchaseRecordId = order.Id,
                    PurchaseOrderNumber = order.PurchaseOrderNumber,
                    SupplierName = _unitOfWork.Repository<Supplier>().GetById(order.SupplierId)?.SupplierName,
                    PurchaseDate = order.PurchaseDate,
                    Quantity = order.Quantity,
                    Received = status.Received,
                    Status = status.Status
                });
            }

            summary.OrderedQuantity = summary.Orders.Sum(x => x.Quantity);
            summary.ReceivedQuantity = summary.Orders.Sum(x => x.Received);
            summary.OutstandingToOrder = PurchaseOrderQuantityRules.Outstanding(total, summary.OrderedQuantity);
            summary.FulfilmentStatus = PurchaseOrderQuantityRules.FulfilmentStatus(total, summary.OrderedQuantity, summary.ReceivedQuantity);
            return summary;
        }

        private static bool Matches(string haystack, string needle)
        {
            return !string.IsNullOrWhiteSpace(haystack)
                && !string.IsNullOrWhiteSpace(needle)
                && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}

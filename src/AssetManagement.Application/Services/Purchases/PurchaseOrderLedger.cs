using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
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

    /// <summary>
    /// rel1009i: ordered / received / outstanding per requisition, PO and PO line (migration 098), and the
    /// classification a PO line resolves to. Shared by PurchaseService (ordering) and ReceivingService (receiving).
    /// Legacy POs (no PurchaseOrderLine rows) are one virtual line (Id 0) received against the PO quantity.
    /// </summary>
    public class PurchaseOrderLedger
    {
        public const string SourcePurchaseOrderLine = "PurchaseOrderLine";
        public const string SourceRequisitionAsset = "RequisitionAsset";
        public const string SourceSelectedOnOrder = "SelectedOnOrder";
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
                case SourceSelectedOnOrder:
                    return "Purchase order line";
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

            var fromAsset = FromRequisitionAsset(request);
            if (fromAsset != null)
            {
                return fromAsset;
            }

            return FromSupplierCatalog(purchase == null ? 0 : purchase.SupplierId, string.IsNullOrWhiteSpace(description) ? request?.ItemDescription : description)
                ?? new PurchaseLineClassification();
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
                ClassificationSource = classification.HasType ? classification.Source : null
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

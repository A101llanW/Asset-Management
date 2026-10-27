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
                RequiresTypeSelection = !context.AssetTypeId.HasValue || context.AssetTypeId.Value <= 0,
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
            if (new PurchaseOrderLedger(_unitOfWork).GetLines(purchase.Id).Count > 0)
            {
                // rel1009i: a PO with lines is received per line (ReceiveOrder) so line receipts stay exact.
                throw new BusinessException("This purchase order has lines. Receive it on the receiving screen, line by line.");
            }

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
            var resolvedSubType = subType;
            if (resolvedSubType == null && context.AssetSubTypeId.HasValue && context.AssetSubTypeId.Value > 0)
            {
                resolvedSubType = _assetSubTypeService.GetById(context.AssetSubTypeId.Value);
            }
            var resolvedAssetTypeId = context.AssetTypeId;
            if ((!resolvedAssetTypeId.HasValue || resolvedAssetTypeId.Value <= 0) && model.AssetTypeId.HasValue && model.AssetTypeId.Value > 0)
            {
                resolvedAssetTypeId = model.AssetTypeId;
            }
            if ((!resolvedAssetTypeId.HasValue || resolvedAssetTypeId.Value <= 0) && resolvedSubType != null)
            {
                resolvedAssetTypeId = resolvedSubType.AssetTypeId;
            }
            if (!resolvedAssetTypeId.HasValue || resolvedAssetTypeId.Value <= 0)
            {
                throw new BusinessException("Select an asset type (and optional sub-type) before receiving. This purchase has no linked requisition/catalog classification context.");
            }
            var assetType = _unitOfWork.Repository<AssetType>().GetById(resolvedAssetTypeId.Value);
            if (assetType == null)
            {
                throw new BusinessException("Asset type was not found.");
            }

            // Sub-type is mandatory at receiving (catalog requisition flows, stage d): every received asset is fully classified.
            EnsureReceiveSubType(resolvedSubType == null ? (int?)null : resolvedSubType.Id, resolvedSubType == null ? (int?)null : resolvedSubType.AssetTypeId, assetType.Id, null);
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
                        AssetSubTypeId = resolvedSubType != null ? resolvedSubType.Id : (int?)null,
                        Brand = resolvedSubType != null ? resolvedSubType.Brand : context.Brand,
                        Model = resolvedSubType != null ? resolvedSubType.Model : context.Model,
                        SerialNumber = unit.SerialNumber,
                        Description = itemDescription,
                        PurchaseDate = model.ReceivedDate,
                        // The PO unit cost is this asset's own price; no PO price = "Not set" (no 0.01 placeholder).
                        AcquisitionCost = purchase.UnitCost > 0 ? purchase.UnitCost : (decimal?)null,
                        Currency = purchase.Currency,
                        SupplierId = purchase.SupplierId > 0 ? purchase.SupplierId : (int?)null,
                        DepartmentId = receiveDepartmentId,
                        ConditionOnReceipt = model.ConditionOnReceipt,
                        CurrentStatus = AssetStatus.InStore,
                        AllowDeferredSubTypeClassification = false
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
        // ===================== rel1009i: receiving screen (per PO line) =====================

        public ReceiveOrderVm GetReceiveOrder(int purchaseRecordId)
        {
            var purchase = _unitOfWork.Repository<PurchaseRecord>().GetById(purchaseRecordId);
            if (purchase == null)
            {
                return null;
            }

            var request = purchase.PurchaseRequestId.HasValue
                ? _unitOfWork.Repository<PurchaseRequest>().GetById(purchase.PurchaseRequestId.Value)
                : null;
            var status = new PurchaseOrderLedger(_unitOfWork).BuildStatus(purchase);
            var model = new ReceiveOrderVm
            {
                PurchaseRecordId = purchase.Id,
                PurchaseOrderNumber = purchase.PurchaseOrderNumber,
                SupplierName = _unitOfWork.Repository<Supplier>().GetById(purchase.SupplierId)?.SupplierName,
                PurchaseRequestId = purchase.PurchaseRequestId,
                PurchaseRequestNumber = request?.RequestNumber,
                RequisitionDepartmentId = request != null && request.DepartmentId > 0 ? (int?)request.DepartmentId : null,
                RequisitionDepartmentName = request != null ? ResolveDepartmentName(request.DepartmentId) : null,
                IsLegacy = status.IsLegacy,
                Status = status.Status,
                Ordered = status.Ordered,
                Received = status.Received,
                Outstanding = status.Outstanding,
                ReceivedDate = DateTime.UtcNow
            };

            foreach (var line in status.Lines)
            {
                model.Lines.Add(new ReceiveOrderLineVm
                {
                    PurchaseOrderLineId = line.Id,
                    LineNumber = line.LineNumber,
                    Description = line.Description,
                    Ordered = line.Ordered,
                    Received = line.Received,
                    Outstanding = line.Outstanding,
                    UnitCost = line.UnitCost,
                    AssetCategoryName = line.AssetCategoryName,
                    AssetTypeId = line.AssetTypeId,
                    AssetTypeName = line.AssetTypeName,
                    AssetSubTypeId = line.AssetSubTypeId,
                    AssetSubTypeName = line.AssetSubTypeName,
                    ClassificationSourceLabel = PurchaseOrderLedger.DescribeSource(line.ClassificationSource),
                    NeedsClassification = !line.HasClassification,
                    QuantityReceived = line.Outstanding,
                    Condition = AssetCondition.New.ToString(),
                    TagMode = ReceiveOrderLineVm.TagModeGenerate,
                    DestinationKind = model.RequisitionDepartmentId.HasValue ? ReceiveOrderLineVm.DestinationDepartment : ReceiveOrderLineVm.DestinationStore,
                    DestinationDepartmentId = model.RequisitionDepartmentId
                });
            }

            return model;
        }

        /// <summary>
        /// Receives any number of units on any lines of one PO. Classification comes from the line / requisition asset /
        /// supplier catalog (posted values are ignored when a source has it). Partial receipts are allowed; over-receipt
        /// is refused per line and per PO, cumulatively, inside one transaction that locks the PO's receipts.
        /// </summary>
        public ReceiveResultVm ReceiveOrder(ReceiveOrderVm model, string receivedById)
        {
            if (model == null)
            {
                throw new BusinessException("Receipt details are required.");
            }

            var purchase = _unitOfWork.Repository<PurchaseRecord>().GetById(model.PurchaseRecordId);
            if (purchase == null || !purchase.IsActive)
            {
                throw new BusinessException("Purchase order not found.");
            }

            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (organizationId.HasValue && purchase.OrganizationId.HasValue && purchase.OrganizationId.Value != organizationId.Value)
            {
                throw new BusinessException("Purchase order not found.");
            }

            if (model.ReceivedDate == default(DateTime))
            {
                throw new BusinessException("Received date is required.");
            }

            var request = purchase.PurchaseRequestId.HasValue
                ? _unitOfWork.Repository<PurchaseRequest>().GetById(purchase.PurchaseRequestId.Value)
                : null;
            var ledger = new PurchaseOrderLedger(_unitOfWork);
            var postedLines = (model.Lines ?? new List<ReceiveOrderLineVm>())
                .Where(x => x != null && x.QuantityReceived != 0)
                .ToList();
            if (postedLines.Count == 0)
            {
                throw new BusinessException("Enter a quantity received on at least one line.");
            }

            if (postedLines.Select(x => x.PurchaseOrderLineId).Distinct().Count() != postedLines.Count)
            {
                throw new BusinessException("Each purchase order line can appear only once in a receipt.");
            }

            // Validate the inputs that do not depend on current quantities before opening the transaction.
            var plans = new List<ReceiveLinePlan>();
            var seenSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var input in postedLines)
            {
                var label = "line " + (input.LineNumber > 0 ? input.LineNumber.ToString() : input.PurchaseOrderLineId.ToString());
                if (input.QuantityReceived < 0)
                {
                    throw new BusinessException("Quantity received cannot be negative (" + label + ").");
                }

                AssetCondition condition;
                if (string.IsNullOrWhiteSpace(input.Condition)
                    || !Enum.TryParse(input.Condition.Trim(), true, out condition)
                    || !Enum.IsDefined(typeof(AssetCondition), condition))
                {
                    throw new BusinessException("Choose the condition on receipt (" + label + ").");
                }

                var serials = PurchaseOrderQuantityRules.ParseList(input.SerialNumbersText);
                if (serials.Count > 0 && serials.Count != input.QuantityReceived)
                {
                    throw new BusinessException("Enter exactly " + input.QuantityReceived + " serial number(s), one per line, or leave serials blank (" + label + ": " + serials.Count + " entered).");
                }

                foreach (var serial in serials)
                {
                    if (serial.Length > 120)
                    {
                        throw new BusinessException("Serial number is too long (max 120): " + serial);
                    }

                    if (!seenSerials.Add(serial))
                    {
                        throw new BusinessException("Duplicate serial number in this receipt: " + serial);
                    }
                }

                IList<string> tags = null;
                if (string.Equals(input.TagMode, ReceiveOrderLineVm.TagModeEnter, StringComparison.OrdinalIgnoreCase))
                {
                    tags = PurchaseOrderQuantityRules.ParseList(input.AssetTagsText);
                    if (tags.Count != input.QuantityReceived)
                    {
                        throw new BusinessException("Enter exactly " + input.QuantityReceived + " asset tag(s), one per line, or choose 'Generate tags' (" + label + ": " + tags.Count + " entered).");
                    }

                    foreach (var tag in tags)
                    {
                        if (!seenTags.Add(tag))
                        {
                            throw new BusinessException("Duplicate asset tag in this receipt: " + tag);
                        }
                    }
                }

                plans.Add(new ReceiveLinePlan
                {
                    Input = input,
                    Label = label,
                    Condition = condition.ToString(),
                    Serials = serials,
                    Tags = tags,
                    Destination = ResolveDestination(input, purchase, label)
                });
            }

            var createdAssets = new List<ReceiveCreatedAssetVm>();
            var summary = new List<string>();
            var receivingId = 0;
            var fullyReceived = false;
            try
            {
                _unitOfWork.ExecuteInTransaction(() =>
                {
                    // Locks the PO row and its receipts (UPDLOCK, HOLDLOCK) until commit: concurrent receipts serialise.
                    var remainingOnOrder = _unitOfWork.GetRemainingPurchaseQuantity(purchase.Id);
                    var lines = ledger.GetLines(purchase.Id);
                    var receivedByLine = lines.Count > 0 ? ledger.ReceivedByLine(purchase.Id) : new Dictionary<int, int>();
                    var totalRequested = 0;
                    foreach (var plan in plans)
                    {
                        if (lines.Count == 0)
                        {
                            if (plan.Input.PurchaseOrderLineId != 0)
                            {
                                throw new BusinessException("That line does not belong to this purchase order.");
                            }

                            plan.Description = request?.ItemDescription;
                            plan.UnitCost = purchase.UnitCost;
                            PurchaseOrderQuantityRules.EnsureCanReceive(plan.Label, plan.Input.QuantityReceived, purchase.Quantity, ledger.ReceivedForPurchase(purchase.Id));
                        }
                        else
                        {
                            var line = lines.FirstOrDefault(x => x.Id == plan.Input.PurchaseOrderLineId);
                            if (line == null)
                            {
                                throw new BusinessException("That line does not belong to this purchase order.");
                            }

                            int alreadyReceived;
                            receivedByLine.TryGetValue(line.Id, out alreadyReceived);
                            PurchaseOrderQuantityRules.EnsureCanReceive(plan.Label, plan.Input.QuantityReceived, line.Quantity, alreadyReceived);
                            plan.Line = line;
                            plan.Description = line.Description;
                            plan.UnitCost = line.UnitCost;
                        }

                        plan.Classification = ResolvePlanClassification(ledger, purchase, request, plan);
                        totalRequested += plan.Input.QuantityReceived;
                    }

                    PurchaseOrderQuantityRules.EnsureCanReceive("this purchase order", totalRequested, remainingOnOrder, 0);

                    var now = DateTime.UtcNow;
                    foreach (var plan in plans)
                    {
                        for (var i = 0; i < plan.Input.QuantityReceived; i++)
                        {
                            var serial = plan.Serials.Count > 0 ? plan.Serials[i] : null;
                            var assetId = _assetService.Create(new AssetCreateVm
                            {
                                AssetName = ResolveLineAssetName(plan),
                                AssetTag = plan.Tags == null ? null : plan.Tags[i], // null = the existing tag-number scheme
                                CategoryId = plan.Classification.AssetCategoryId.Value,
                                AssetTypeId = plan.Classification.AssetTypeId.Value,
                                AssetSubTypeId = plan.Classification.AssetSubTypeId,
                                Brand = plan.Classification.Brand,
                                Model = plan.Classification.Model,
                                SerialNumber = serial,
                                Description = plan.Description,
                                PurchaseDate = model.ReceivedDate,
                                AcquisitionCost = plan.UnitCost > 0 ? plan.UnitCost : (purchase.UnitCost > 0 ? purchase.UnitCost : (decimal?)null),
                                Currency = purchase.Currency,
                                SupplierId = purchase.SupplierId > 0 ? purchase.SupplierId : (int?)null,
                                DepartmentId = plan.Destination == null ? (int?)null : plan.Destination.Id,
                                ConditionOnReceipt = plan.Condition,
                                CurrentStatus = AssetStatus.InStore,
                                AllowDeferredSubTypeClassification = false
                            });
                            var asset = _unitOfWork.Repository<Asset>().GetById(assetId);
                            // AssetService.Create always starts at Condition=New; the receiver's per-line condition wins.
                            AssetCondition receivedCondition;
                            if (asset != null
                                && Enum.TryParse(plan.Condition, true, out receivedCondition)
                                && asset.Condition != receivedCondition)
                            {
                                asset.Condition = receivedCondition;
                                _unitOfWork.Repository<Asset>().Update(asset);
                            }

                            var receiving = new AssetReceiving
                            {
                                OrganizationId = purchase.OrganizationId,
                                PurchaseRecordId = purchase.Id,
                                AssetId = assetId,
                                ReceivedDate = model.ReceivedDate,
                                ConditionOnReceipt = plan.Condition,
                                QuantityReceived = 1,
                                ReceivedById = receivedById,
                                Notes = model.Notes,
                                CreatedAt = now,
                                IsActive = true
                            };
                            _unitOfWork.Repository<AssetReceiving>().Add(receiving);
                            _unitOfWork.SaveChanges();
                            receivingId = receiving.Id;
                            if (plan.Line != null)
                            {
                                _unitOfWork.Repository<PurchaseOrderLineReceipt>().Add(new PurchaseOrderLineReceipt
                                {
                                    OrganizationId = purchase.OrganizationId,
                                    PurchaseRecordId = purchase.Id,
                                    PurchaseOrderLineId = plan.Line.Id,
                                    AssetReceivingId = receiving.Id,
                                    AssetId = assetId,
                                    QuantityReceived = 1,
                                    CreatedAt = now,
                                    IsActive = true
                                });
                                _unitOfWork.SaveChanges();
                            }

                            createdAssets.Add(new ReceiveCreatedAssetVm { AssetId = assetId, AssetTag = asset?.AssetTag, SerialNumber = serial });
                        }

                        summary.Add(plan.Label + " x" + plan.Input.QuantityReceived + " -> "
                            + (plan.Destination == null ? "store (company custody)" : plan.Destination.Name)
                            + " [" + plan.Condition + "]");
                    }

                    // PO status: Received once nothing is outstanding (ReceivedDate stamped); otherwise Partially Received.
                    if (remainingOnOrder - totalRequested <= 0)
                    {
                        fullyReceived = true;
                        purchase.ReceivedDate = model.ReceivedDate;
                        purchase.UpdatedAt = now;
                        _unitOfWork.Repository<PurchaseRecord>().Update(purchase);
                        _unitOfWork.SaveChanges();
                    }
                });
            }
            catch (Exception ex)
            {
                if (PurchaseOrderQuantityRules.IsMissingTable(ex))
                {
                    throw new BusinessException("Receiving per line is not available yet: database migration 098 has not been applied.");
                }

                throw;
            }

            var statusText = fullyReceived ? PurchaseOrderQuantityRules.StatusReceived : PurchaseOrderQuantityRules.StatusPartiallyReceived;
            _auditWriter?.Write(
                "Purchases.Receive",
                nameof(AssetReceiving),
                receivingId > 0 ? receivingId.ToString() : null,
                purchase.Id.ToString(),
                "PO " + (purchase.PurchaseOrderNumber ?? purchase.Id.ToString())
                    + (request == null ? string.Empty : " | requisition " + (request.RequestNumber ?? request.Id.ToString()))
                    + " | " + string.Join("; ", summary)
                    + " | status " + statusText
                    + " | assets " + string.Join(",", createdAssets.Select(x => x.AssetId)));

            NotifyRequisitionReceipt(request, purchase, createdAssets.Count, statusText);
            return new ReceiveResultVm { ReceivingId = receivingId, CreatedAssets = createdAssets };
        }

        private Department ResolveDestination(ReceiveOrderLineVm input, PurchaseRecord purchase, string label)
        {
            if (string.IsNullOrWhiteSpace(input.DestinationKind)
                || string.Equals(input.DestinationKind, ReceiveOrderLineVm.DestinationStore, StringComparison.OrdinalIgnoreCase))
            {
                return null; // Store = company custody (no department), In Store.
            }

            if (!string.Equals(input.DestinationKind, ReceiveOrderLineVm.DestinationDepartment, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("Invalid destination (" + label + ").");
            }

            if (!input.DestinationDepartmentId.HasValue || input.DestinationDepartmentId.Value <= 0)
            {
                throw new BusinessException("Choose the department or room the goods go to (" + label + ").");
            }

            var department = _unitOfWork.Repository<Department>().GetById(input.DestinationDepartmentId.Value);
            if (department == null || !department.IsActive
                || (purchase.OrganizationId.HasValue && department.OrganizationId.HasValue && department.OrganizationId.Value != purchase.OrganizationId.Value))
            {
                throw new BusinessException("The selected department or room was not found (" + label + ").");
            }

            // A received unit comes from company custody (no department): this is a first placement, which the move
            // rules always allow; the rel1009h assignment-target rule is applied for consistency with Assign.
            AssetMoveRules.EnsureAssignmentTarget(null, false, department.DepartmentKind, !department.ParentDepartmentId.HasValue);
            return department;
        }

        private PurchaseLineClassification ResolvePlanClassification(PurchaseOrderLedger ledger, PurchaseRecord purchase, PurchaseRequest request, ReceiveLinePlan plan)
        {
            var classification = ledger.ResolveClassification(purchase, request, plan.Line, plan.Description);
            var input = plan.Input;
            if (classification != null && classification.HasType)
            {
                // Source wins for the type. A missing sub-type must be chosen by the receiver (stage d: mandatory).
                if (!classification.AssetSubTypeId.HasValue || classification.AssetSubTypeId.Value <= 0)
                {
                    var pickedSubTypeId = input.OverrideAssetSubTypeId.HasValue && input.OverrideAssetSubTypeId.Value > 0
                        ? input.OverrideAssetSubTypeId
                        : null;
                    var picked = pickedSubTypeId.HasValue ? _unitOfWork.Repository<AssetSubType>().GetById(pickedSubTypeId.Value) : null;
                    EnsureReceiveSubType(pickedSubTypeId, picked == null ? (int?)null : picked.AssetTypeId, classification.AssetTypeId.Value, plan.Label, picked == null || picked.IsActive);
                    classification.AssetSubTypeId = picked.Id;
                    classification.Source = string.IsNullOrWhiteSpace(classification.Source) ? "ReceiverSubType" : classification.Source + "+ReceiverSubType";
                    return ledger.Complete(classification);
                }

                return classification; // Source wins: posted type / sub-type are ignored.
            }

            if (!input.OverrideAssetTypeId.HasValue || input.OverrideAssetTypeId.Value <= 0)
            {
                throw new BusinessException("Choose the asset type (" + plan.Label + "): neither the purchase order line, the requisition nor the supplier catalog has one.");
            }

            var type = _unitOfWork.Repository<AssetType>().GetById(input.OverrideAssetTypeId.Value);
            if (type == null || !type.IsActive)
            {
                throw new BusinessException("The selected asset type was not found (" + plan.Label + ").");
            }

            int? subTypeId = null;
            if (input.OverrideAssetSubTypeId.HasValue && input.OverrideAssetSubTypeId.Value > 0)
            {
                var subType = _unitOfWork.Repository<AssetSubType>().GetById(input.OverrideAssetSubTypeId.Value);
                if (subType == null || subType.AssetTypeId != type.Id)
                {
                    throw new BusinessException("The selected sub-type does not belong to the selected asset type (" + plan.Label + ").");
                }

                EnsureReceiveSubType(subType.Id, subType.AssetTypeId, type.Id, plan.Label, subType.IsActive);
                subTypeId = subType.Id;
            }
            else
            {
                EnsureReceiveSubType(null, null, type.Id, plan.Label);
            }

            return ledger.Complete(new PurchaseLineClassification { AssetTypeId = type.Id, AssetSubTypeId = subTypeId, Source = "ReceiverOverride" });
        }

        /// <summary>
        /// Stage (d): a sub-type is mandatory for goods received from now on. Legacy assets/receipts without one are
        /// untouched (DB trigger TR_AssetReceiving_RequireSubType checks only newly inserted receipts).
        /// </summary>
        internal static void EnsureReceiveSubType(int? subTypeId, int? subTypeAssetTypeId, int assetTypeId, string label, bool subTypeActive = true)
        {
            var suffix = string.IsNullOrWhiteSpace(label) ? "." : " (" + label + ").";
            if (!subTypeId.HasValue || subTypeId.Value <= 0)
            {
                throw new BusinessException("Choose the asset sub-type before receiving" + suffix + " Every received asset needs a sub-type.");
            }

            if (!subTypeAssetTypeId.HasValue || subTypeAssetTypeId.Value != assetTypeId)
            {
                throw new BusinessException("The selected sub-type does not belong to the asset type" + suffix);
            }

            if (!subTypeActive)
            {
                throw new BusinessException("The selected sub-type is inactive" + suffix);
            }
        }

        private string ResolveLineAssetName(ReceiveLinePlan plan)
        {
            if (!string.IsNullOrWhiteSpace(plan.Description))
            {
                return plan.Description.Trim().Length > 200 ? plan.Description.Trim().Substring(0, 200) : plan.Description.Trim();
            }

            if (plan.Classification.AssetSubTypeId.HasValue)
            {
                var name = _unitOfWork.Repository<AssetSubType>().GetById(plan.Classification.AssetSubTypeId.Value)?.Name;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }

            var brandModel = string.Join(" ", new[] { plan.Classification.Brand, plan.Classification.Model }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
            return string.IsNullOrWhiteSpace(brandModel) ? "Received asset" : brandModel;
        }

        private void NotifyRequisitionReceipt(PurchaseRequest request, PurchaseRecord purchase, int unitCount, string statusText)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RequestedById) || unitCount <= 0)
            {
                return;
            }

            try
            {
                NotificationHelper.AddNotification(
                    _unitOfWork,
                    _outboxWriter,
                    _organizationScope,
                    request.RequestedById,
                    NotificationType.General,
                    "Goods received",
                    unitCount + " unit(s) received on PO " + (purchase.PurchaseOrderNumber ?? purchase.Id.ToString())
                        + " for requisition " + (request.RequestNumber ?? request.Id.ToString()) + " (" + statusText + ").",
                    "/PurchaseRequests/Details/" + request.Id);
                _unitOfWork.SaveChanges();
            }
            catch (BusinessException)
            {
                // A notification problem never undoes a committed receipt.
            }
        }

        private sealed class ReceiveLinePlan
        {
            public ReceiveOrderLineVm Input { get; set; }

            public string Label { get; set; }

            public string Condition { get; set; }

            public IList<string> Serials { get; set; }

            public IList<string> Tags { get; set; }

            public Department Destination { get; set; }

            public PurchaseOrderLine Line { get; set; }

            public string Description { get; set; }

            public decimal UnitCost { get; set; }

            public PurchaseLineClassification Classification { get; set; }
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
            // Classification Assign during receive removed — allow blank subtype; classify after create when type context exists.
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
                throw new BusinessException("Requisition target must be a leaf department (classroom or admin unit).");
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

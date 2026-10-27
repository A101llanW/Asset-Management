using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public class PurchaseService : IPurchaseService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOperationsQueryRepository _operationsQueryRepository;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IAuditWriter _auditWriter;
        private readonly IUserService _userService;

        public PurchaseService(
            IUnitOfWork unitOfWork,
            IOperationsQueryRepository operationsQueryRepository,
            IOrganizationScopeService organizationScope,
            IAuditWriter auditWriter = null,
            IUserService userService = null)
        {
            _unitOfWork = unitOfWork;
            _operationsQueryRepository = operationsQueryRepository;
            _organizationScope = organizationScope;
            _auditWriter = auditWriter;
            _userService = userService;
        }

        public IEnumerable<PurchaseRecordVm> GetAll()
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return new List<PurchaseRecordVm>();
            }

            return _operationsQueryRepository.GetPurchaseRecordList(organizationId.Value);
        }

        public PagedListVm<PurchaseRecordVm> GetListPage(
            string search,
            int? supplierId,
            string sort,
            string direction,
            int page,
            int pageSize)
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return new PagedListVm<PurchaseRecordVm>();
            }

            return _operationsQueryRepository.GetPurchaseRecordListPage(
                organizationId.Value,
                search,
                supplierId,
                sort,
                direction,
                page,
                pageSize);
        }

        public PurchaseRecordVm GetById(int id)
        {
            var suppliers = _unitOfWork.Repository<Supplier>().GetAll()
                .ToDictionary(x => x.Id, x => x.SupplierName);
            var entity = _unitOfWork.Repository<PurchaseRecord>().GetById(id);
            if (entity == null)
            {
                return null;
            }

            var requestNumber = entity.PurchaseRequestId.HasValue
                ? _unitOfWork.Repository<PurchaseRequest>().GetById(entity.PurchaseRequestId.Value)?.RequestNumber
                : null;

            return new PurchaseRecordVm
            {
                Id = entity.Id,
                PurchaseRequestId = entity.PurchaseRequestId,
                PurchaseRequestNumber = requestNumber,
                PurchaseOrderNumber = entity.PurchaseOrderNumber,
                SupplierId = entity.SupplierId,
                SupplierName = suppliers.ContainsKey(entity.SupplierId) ? suppliers[entity.SupplierId] : null,
                InvoiceNumber = entity.InvoiceNumber,
                PurchaseDate = entity.PurchaseDate,
                Quantity = entity.Quantity,
                UnitCost = entity.UnitCost,
                TotalCost = entity.TotalCost,
                Currency = entity.Currency
            };
        }

        public int Create(PurchaseRecordVm model)
        {
            // No actor supplied: fail closed for requisitions with a configured PO raiser.
            return Create(model, null, null);
        }

        public const string RequisitionRequiredMessage =
            "A purchase order must be raised from an approved requisition. Choose the requisition first.";

        public int Create(PurchaseRecordVm model, string actingUserId, int? actingRoleId)
        {
            if (model == null)
            {
                throw new BusinessException("Purchase record details are required.");
            }

            // rel1009i: every new PO is linked to an approved requisition (standalone POs are legacy only).
            if (!model.PurchaseRequestId.HasValue || model.PurchaseRequestId.Value <= 0)
            {
                throw new BusinessException(RequisitionRequiredMessage);
            }

            var organizationId = _organizationScope.GetCurrentOrganizationId();
            var request = _unitOfWork.Repository<PurchaseRequest>().GetById(model.PurchaseRequestId.Value);
            if (request == null || !request.IsActive)
            {
                throw new BusinessException("Purchase request not found.");
            }

            if (request.ApprovalStatus != ApprovalStatus.Approved)
            {
                throw new BusinessException("Only approved purchase requests can be linked to a purchase record.");
            }

            if (organizationId.HasValue && request.OrganizationId.HasValue && request.OrganizationId.Value != organizationId.Value)
            {
                throw new BusinessException("Purchase request not found.");
            }

            if (!organizationId.HasValue && request.OrganizationId.HasValue)
            {
                organizationId = request.OrganizationId;
            }

            EnsurePurchaseOrderRaiser(request, actingUserId, actingRoleId);

            if (!organizationId.HasValue)
            {
                throw new BusinessException("Organization context is required to create a purchase record.");
            }

            var ledger = new PurchaseOrderLedger(_unitOfWork);
            var currency = string.IsNullOrWhiteSpace(model.Currency)
                ? ApprovalWorkflowSettingsHelper.GetDefaultCurrencyCode(_unitOfWork.Repository<SystemSetting>().GetAll())
                : model.Currency.Trim().ToUpperInvariant();
            PurchaseRecord entity = null;
            var createdLines = new List<PurchaseOrderLine>();
            var classificationEdits = new List<PurchaseLineClassificationEdit>();

            try
            {
                _unitOfWork.ExecuteInTransaction(() =>
                {
                    // Touch the requisition first: concurrent POs for the same requisition serialise on this row
                    // (or fail on its row version), so the cumulative over-ordering check below cannot be raced.
                    request.UpdatedAt = DateTime.UtcNow;
                    _unitOfWork.Repository<PurchaseRequest>().Update(request);
                    _unitOfWork.SaveChanges();

                    var lines = BuildOrderLines(ledger, request, model, organizationId, classificationEdits);
                    if (lines.Count == 0 || lines.Sum(x => x.Quantity) <= 0)
                    {
                        throw new BusinessException("Enter a quantity of at least 1 on one or more requisition lines.");
                    }

                    var requisitionLines = ledger.GetRequisitionLines(request.Id);
                    var requisitionTotal = ledger.RequisitionTotal(request, requisitionLines);
                    var orderedTotal = ledger.OrderedForRequisition(request.Id);
                    var orderedByLine = ledger.OrderedByRequisitionLine(request.Id);
                    foreach (var line in lines.Where(x => x.PurchaseRequestLineId.HasValue))
                    {
                        var requisitionLine = requisitionLines.First(x => x.Id == line.PurchaseRequestLineId.Value);
                        int alreadyOrdered;
                        orderedByLine.TryGetValue(requisitionLine.Id, out alreadyOrdered);
                        PurchaseOrderQuantityRules.EnsureCanOrder(
                            "line " + requisitionLine.LineNumber + " (" + Shorten(requisitionLine.Description) + ")",
                            line.Quantity,
                            requisitionLine.Quantity,
                            alreadyOrdered);
                    }

                    PurchaseOrderQuantityRules.EnsureCanOrder(
                        "requisition " + (request.RequestNumber ?? request.Id.ToString()),
                        lines.Sum(x => x.Quantity),
                        requisitionTotal,
                        orderedTotal);

                    var quantity = lines.Sum(x => x.Quantity);
                    var total = lines.Sum(x => x.Quantity * x.UnitCost);
                    entity = new PurchaseRecord
                    {
                        OrganizationId = organizationId,
                        PurchaseRequestId = request.Id,
                        PurchaseOrderNumber = model.PurchaseOrderNumber,
                        SupplierId = model.SupplierId,
                        InvoiceNumber = model.InvoiceNumber,
                        PurchaseDate = model.PurchaseDate,
                        Quantity = quantity,
                        UnitCost = quantity > 0 ? Math.Round(total / quantity, 2) : 0m,
                        TotalCost = total,
                        Currency = currency,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };

                    _unitOfWork.Repository<PurchaseRecord>().Add(entity);
                    _unitOfWork.SaveChanges();

                    var lineNumber = 0;
                    foreach (var line in lines.Where(x => x.Quantity > 0))
                    {
                        line.PurchaseRecordId = entity.Id;
                        line.OrganizationId = organizationId;
                        line.LineNumber = ++lineNumber;
                        line.CreatedAt = DateTime.UtcNow;
                        line.IsActive = true;
                        _unitOfWork.Repository<PurchaseOrderLine>().Add(line);
                        createdLines.Add(line);
                    }

                    _unitOfWork.SaveChanges();
                });
            }
            catch (Exception ex)
            {
                if (PurchaseOrderQuantityRules.IsMissingTable(ex))
                {
                    throw new BusinessException("Purchase order lines are not available yet: database migration 098 has not been applied.");
                }

                throw;
            }

            _auditWriter?.Write(
                "Purchases.Create",
                nameof(PurchaseRecord),
                entity.Id.ToString(),
                null,
                (entity.PurchaseOrderNumber ?? entity.Id.ToString())
                    + " | requisition " + (request.RequestNumber ?? request.Id.ToString())
                    + " | " + string.Join("; ", createdLines.Select(x => "L" + x.LineNumber + " x" + x.Quantity)));

            // C3: every classification changed from the requisition prefill gets its own audit row (old vs new).
            foreach (var edit in classificationEdits)
            {
                _auditWriter?.Write(
                    PurchaseOrderLedger.AuditActionClassificationEditedOnOrder,
                    nameof(PurchaseOrderLine),
                    edit.Line.Id.ToString(),
                    ledger.DescribeForAudit(edit.Before),
                    ledger.DescribeForAudit(edit.After)
                        + " | PO " + (entity.PurchaseOrderNumber ?? entity.Id.ToString()) + " line " + edit.Line.LineNumber
                        + " | requisition " + (request.RequestNumber ?? request.Id.ToString())
                        + (edit.Line.PurchaseRequestLineId.HasValue ? " line #" + edit.Line.PurchaseRequestLineId.Value : string.Empty)
                        + " | prefilled from " + (PurchaseOrderLedger.DescribeSource(edit.Before.Source) ?? "requisition"));
            }

            return entity.Id;
        }

        /// <summary>
        /// The PO lines to create: posted lines are matched to the requisition's lines by id (description and limits
        /// always come from the requisition, never from the post). Without posted lines (older callers) the whole
        /// quantity goes on the single requisition line, or on the requisition itself when it has no / several lines.
        /// </summary>
        private IList<PurchaseOrderLine> BuildOrderLines(
            PurchaseOrderLedger ledger,
            PurchaseRequest request,
            PurchaseRecordVm model,
            int? organizationId,
            IList<PurchaseLineClassificationEdit> edits)
        {
            edits.Clear();
            var requisitionLines = ledger.GetRequisitionLines(request.Id);
            var posted = (model.Lines ?? new List<PurchaseOrderLineInputVm>()).Where(x => x != null).ToList();
            var result = new List<PurchaseOrderLine>();
            if (posted.Count == 0)
            {
                var single = requisitionLines.Count == 1 ? requisitionLines[0] : null;
                result.Add(BuildLine(ledger, request, model.SupplierId, single, single == null ? request.ItemDescription : single.Description, model.Quantity, model.UnitCost, null, null, organizationId, null, edits));
                return result;
            }

            var seen = new HashSet<int>();
            foreach (var input in posted)
            {
                if (input.Quantity < 0)
                {
                    throw new BusinessException("Quantity cannot be negative.");
                }

                if (input.UnitCost < 0)
                {
                    throw new BusinessException("Unit cost cannot be negative.");
                }

                PurchaseRequestLine requisitionLine = null;
                if (requisitionLines.Count > 0)
                {
                    if (!input.PurchaseRequestLineId.HasValue)
                    {
                        throw new BusinessException("Each purchase order line must come from a line of the requisition.");
                    }

                    requisitionLine = requisitionLines.FirstOrDefault(x => x.Id == input.PurchaseRequestLineId.Value);
                    if (requisitionLine == null)
                    {
                        throw new BusinessException("A purchase order line does not belong to this requisition.");
                    }

                    if (!seen.Add(requisitionLine.Id))
                    {
                        throw new BusinessException("A requisition line can appear only once on a purchase order.");
                    }
                }
                else if (input.PurchaseRequestLineId.HasValue || result.Count > 0)
                {
                    throw new BusinessException("A purchase order line does not belong to this requisition.");
                }

                if (input.Quantity == 0)
                {
                    continue;
                }

                result.Add(BuildLine(
                    ledger,
                    request,
                    model.SupplierId,
                    requisitionLine,
                    requisitionLine == null ? request.ItemDescription : requisitionLine.Description,
                    input.Quantity,
                    input.UnitCost,
                    input.AssetTypeId,
                    input.AssetSubTypeId,
                    organizationId,
                    "line " + (requisitionLine != null ? requisitionLine.LineNumber : (input.LineNumber > 0 ? input.LineNumber : 1)),
                    edits));
            }

            return result;
        }

        private PurchaseOrderLine BuildLine(
            PurchaseOrderLedger ledger,
            PurchaseRequest request,
            int supplierId,
            PurchaseRequestLine requisitionLine,
            string description,
            int quantity,
            decimal unitCost,
            int? selectedTypeId,
            int? selectedSubTypeId,
            int? organizationId,
            string label,
            IList<PurchaseLineClassificationEdit> edits)
        {
            // Classification precedence (migration 108): what the requisition PRE-FILLS for this line - the line's stored
            // type / sub-type, then the line's tagged asset, then a legacy header tag - then a type chosen on the PO, then
            // the supplier catalog item; otherwise none and Receiving asks for it. Before 108 only the header asset was
            // read here, so a type picked on a requisition line was dropped at the PO (the "type at PO" gap).
            // C3 (Allan 2026-10-27): the prefill is NOT a lock. A type / sub-type posted with Edit replaces it after
            // validation (active, same organisation, sub-type required and of that type; category from the type) and the
            // change is audit-logged. Nothing posted (the edit selects stay disabled until Edit) = the prefill.
            var prefill = ledger.PrefillClassification(request, requisitionLine);
            if (prefill != null && !prefill.HasType)
            {
                prefill = null; // e.g. a tagged asset whose type no longer exists: nothing to prefill.
            }

            var classification = prefill;
            if (prefill != null && selectedTypeId.HasValue && selectedTypeId.Value > 0)
            {
                var posted = new PurchaseLineClassification { AssetTypeId = selectedTypeId, AssetSubTypeId = selectedSubTypeId };
                if (!PurchaseOrderLedger.SameClassification(prefill, posted))
                {
                    classification = ledger.ValidateEditedClassification(organizationId, selectedTypeId, selectedSubTypeId, label, PurchaseOrderLedger.SourceEditedOnOrder);
                }
            }
            else if (prefill == null && selectedTypeId.HasValue && selectedTypeId.Value > 0)
            {
                var type = _unitOfWork.Repository<AssetType>().GetById(selectedTypeId.Value);
                if (type == null || !type.IsActive)
                {
                    throw new BusinessException("The selected asset type was not found.");
                }

                if (selectedSubTypeId.HasValue && selectedSubTypeId.Value > 0)
                {
                    var subType = _unitOfWork.Repository<AssetSubType>().GetById(selectedSubTypeId.Value);
                    if (subType == null || subType.AssetTypeId != type.Id)
                    {
                        throw new BusinessException("The selected sub-type does not belong to the selected asset type.");
                    }
                }

                classification = ledger.Complete(new PurchaseLineClassification
                {
                    AssetTypeId = type.Id,
                    AssetSubTypeId = selectedSubTypeId.HasValue && selectedSubTypeId.Value > 0 ? selectedSubTypeId : null,
                    Source = PurchaseOrderLedger.SourceSelectedOnOrder
                });
            }

            if (classification == null)
            {
                classification = ledger.FromSupplierCatalog(supplierId, description);
            }

            var orderLine = new PurchaseOrderLine
            {
                PurchaseRequestLineId = requisitionLine == null ? (int?)null : requisitionLine.Id,
                ItemName = AssetNameRules.Normalize(requisitionLine == null ? null : requisitionLine.ItemName),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Quantity = quantity,
                UnitCost = unitCost,
                AssetCategoryId = classification == null ? null : classification.AssetCategoryId,
                AssetTypeId = classification == null ? null : classification.AssetTypeId,
                AssetSubTypeId = classification == null ? null : classification.AssetSubTypeId,
                ClassificationSource = classification == null || !classification.HasType ? null : classification.Source
            };
            if (prefill != null && !ReferenceEquals(classification, prefill))
            {
                edits.Add(new PurchaseLineClassificationEdit { Line = orderLine, Before = prefill, After = classification });
            }

            return orderLine;
        }

        private static string Shorten(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "item";
            }

            var trimmed = text.Trim();
            return trimmed.Length <= 60 ? trimmed : trimmed.Substring(0, 57) + "...";
        }

        /// <summary>rel1009i: a PO draft prefilled from the requisition's lines (outstanding quantities).</summary>
        public PurchaseRecordVm BuildFromRequisition(int purchaseRequestId)
        {
            var request = _unitOfWork.Repository<PurchaseRequest>().GetById(purchaseRequestId);
            if (request == null || !request.IsActive || request.ApprovalStatus != ApprovalStatus.Approved)
            {
                return null;
            }

            var ledger = new PurchaseOrderLedger(_unitOfWork);
            var lines = ledger.BuildRequisitionDraft(request);
            var requisitionLines = ledger.GetRequisitionLines(request.Id).ToDictionary(x => x.Id);
            foreach (var line in lines)
            {
                PurchaseRequestLine requisitionLine = null;
                if (line.PurchaseRequestLineId.HasValue)
                {
                    requisitionLines.TryGetValue(line.PurchaseRequestLineId.Value, out requisitionLine);
                }

                // C3: prefilled from the requisition, shown read-only with an Edit button (not locked).
                var prefill = ledger.PrefillClassification(request, requisitionLine);
                if (prefill != null && prefill.HasType)
                {
                    line.AssetTypeId = prefill.AssetTypeId;
                    line.AssetSubTypeId = prefill.AssetSubTypeId;
                    line.PrefilledAssetTypeId = prefill.AssetTypeId;
                    line.PrefilledAssetSubTypeId = prefill.AssetSubTypeId;
                    line.ClassificationPrefilled = true;
                    line.ClassificationLabel = DescribeClassification(prefill)
                        + (prefill.Source == PurchaseOrderLedger.SourceRequisitionLine
                            ? " (from the requisition line)"
                            : " (from the requisition's existing asset)");
                }
            }

            return new PurchaseRecordVm
            {
                PurchaseRequestId = request.Id,
                PurchaseRequestNumber = request.RequestNumber,
                PurchaseDate = DateTime.UtcNow,
                Currency = request.Currency,
                Quantity = lines.Sum(x => x.Quantity),
                Lines = lines
            };
        }

        /// <summary>rel1009i: approved requisitions (of the given visible ids) with quantity still to order.</summary>
        public IList<PurchaseRequisitionOptionVm> GetRequisitionsReadyForPurchaseOrder(IEnumerable<int> visibleRequestIds)
        {
            var ids = new HashSet<int>(visibleRequestIds ?? Enumerable.Empty<int>());
            var ledger = new PurchaseOrderLedger(_unitOfWork);
            var departments = _unitOfWork.Repository<Department>().GetAll().GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First().Name);
            var result = new List<PurchaseRequisitionOptionVm>();
            foreach (var request in _unitOfWork.Repository<PurchaseRequest>()
                .Find(x => x.IsActive && x.ApprovalStatus == ApprovalStatus.Approved)
                .Where(x => ids.Contains(x.Id))
                .OrderByDescending(x => x.ApprovedAt ?? x.CreatedAt))
            {
                var total = ledger.RequisitionTotal(request, ledger.GetRequisitionLines(request.Id));
                var outstanding = PurchaseOrderQuantityRules.Outstanding(total, ledger.OrderedForRequisition(request.Id));
                if (outstanding <= 0)
                {
                    continue;
                }

                string departmentName;
                departments.TryGetValue(request.DepartmentId, out departmentName);
                result.Add(new PurchaseRequisitionOptionVm
                {
                    Id = request.Id,
                    RequestNumber = request.RequestNumber,
                    DepartmentName = departmentName,
                    ItemDescription = request.ItemDescription,
                    RequisitionQuantity = total,
                    Outstanding = outstanding,
                    Label = (request.RequestNumber ?? ("#" + request.Id))
                        + (string.IsNullOrWhiteSpace(departmentName) ? string.Empty : " - " + departmentName)
                        + " - " + Shorten(request.ItemDescription)
                        + " (" + outstanding + " of " + total + " to order)"
                });
            }

            return result;
        }

        public PurchaseOrderStatusVm GetOrderStatus(int purchaseRecordId)
        {
            var purchase = _unitOfWork.Repository<PurchaseRecord>().GetById(purchaseRecordId);
            return purchase == null ? null : new PurchaseOrderLedger(_unitOfWork).BuildStatus(purchase);
        }

        public RequisitionOrderSummaryVm GetRequisitionOrderSummary(int purchaseRequestId)
        {
            var request = _unitOfWork.Repository<PurchaseRequest>().GetById(purchaseRequestId);
            return request == null ? null : new PurchaseOrderLedger(_unitOfWork).BuildRequisitionSummary(request);
        }

        private string DescribeClassification(PurchaseLineClassification classification)
        {
            if (classification == null || !classification.HasType)
            {
                return null;
            }

            var parts = new List<string>();
            if (classification.AssetCategoryId.HasValue)
            {
                parts.Add(_unitOfWork.Repository<AssetCategory>().GetById(classification.AssetCategoryId.Value)?.Name);
            }

            parts.Add(_unitOfWork.Repository<AssetType>().GetById(classification.AssetTypeId.Value)?.Name);
            if (classification.AssetSubTypeId.HasValue)
            {
                parts.Add(_unitOfWork.Repository<AssetSubType>().GetById(classification.AssetSubTypeId.Value)?.Name);
            }

            return string.Join(" > ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        /// <summary>Server-side "Purchase order raised by" check for a requisition-linked PO (all create paths).</summary>
        private void EnsurePurchaseOrderRaiser(PurchaseRequest request, string actingUserId, int? actingRoleId)
        {
            var config = PurchaseOrderRaiserRules.ResolveForRequest(_unitOfWork, request);
            if (!PurchaseOrderRaiserRules.IsConfigured(config))
            {
                return; // Unconfigured anywhere in the chain: legacy behaviour.
            }

            // Configured: only the raiser (person, else any active holder of the role). No Company Admin exemption.
            // rel1009e: role-only raiser = holders of that role scoped to the requisition's department (shared resolver).
            PurchaseOrderRaiserRules.EnsureCanRaise(
                config,
                actingUserId,
                actingRoleId,
                null,
                PurchaseOrderRaiserRules.ResolveRaiserScope(_unitOfWork, _userService, config, request == null ? (int?)null : request.DepartmentId, request != null && Helpers.CatalogRequisitionFlowResolver.IsCatalogSource(request.ApprovalFlowSource)));
        }
    }
}

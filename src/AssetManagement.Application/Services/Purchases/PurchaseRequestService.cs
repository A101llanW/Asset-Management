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
    public class PurchaseRequestService : IPurchaseRequestService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;
        private readonly IUserService _userService;
        private readonly IDepartmentScopeService _departmentScope;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IOutboxWriter _outboxWriter;
        private readonly IWebhookService _webhookService;
        private readonly IApprovalWorkflowEngine _approvalEngine;
        private readonly IOperationsQueryRepository _operationsQueryRepository;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICurrentUserContext _currentUser;

        public PurchaseRequestService(
            IUnitOfWork unitOfWork,
            IAuditWriter auditWriter,
            IUserService userService,
            IDepartmentScopeService departmentScope,
            IOrganizationScopeService organizationScope,
            IOutboxWriter outboxWriter,
            IWebhookService webhookService,
            IApprovalWorkflowEngine approvalEngine,
            IOperationsQueryRepository operationsQueryRepository,
            IAuthorizationService authorizationService = null,
            ICurrentUserContext currentUser = null)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
            _userService = userService;
            _departmentScope = departmentScope;
            _organizationScope = organizationScope;
            _outboxWriter = outboxWriter;
            _webhookService = webhookService;
            _approvalEngine = approvalEngine;
            _operationsQueryRepository = operationsQueryRepository;
            _authorizationService = authorizationService;
            _currentUser = currentUser;
        }

        public IEnumerable<PurchaseRequestListItemVm> GetAll()
        {
            if (IsDepartmentScopeDenied())
            {
                return new List<PurchaseRequestListItemVm>();
            }

            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return new List<PurchaseRequestListItemVm>();
            }

            var bypassDepartmentScope = _departmentScope.BypassesDepartmentScope || CanApprovePurchases();
            int? departmentId = null;
            var denyDepartmentScope = false;
            if (!bypassDepartmentScope)
            {
                departmentId = _departmentScope.ScopedDepartmentId;
                denyDepartmentScope = !departmentId.HasValue;
            }

            return _operationsQueryRepository.GetPurchaseRequestList(
                organizationId.Value,
                departmentId,
                bypassDepartmentScope,
                denyDepartmentScope);
        }

        public PagedListVm<PurchaseRequestListItemVm> GetListPage(
            string search,
            string sort,
            string direction,
            int page,
            int pageSize)
        {
            if (IsDepartmentScopeDenied())
            {
                return new PagedListVm<PurchaseRequestListItemVm>();
            }

            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return new PagedListVm<PurchaseRequestListItemVm>();
            }

            var bypassDepartmentScope = _departmentScope.BypassesDepartmentScope || CanApprovePurchases();
            int? departmentId = null;
            var denyDepartmentScope = false;
            if (!bypassDepartmentScope)
            {
                departmentId = _departmentScope.ScopedDepartmentId;
                denyDepartmentScope = !departmentId.HasValue;
            }

            return _operationsQueryRepository.GetPurchaseRequestListPage(
                organizationId.Value,
                departmentId,
                bypassDepartmentScope,
                denyDepartmentScope,
                search,
                sort,
                direction,
                page,
                pageSize);
        }

        public PurchaseRequestDetailVm GetById(int id)
        {
            var entity = _unitOfWork.Repository<PurchaseRequest>().GetById(id);
            if (entity == null)
            {
                return null;
            }

            EnsureCanAccessPurchaseRequest(entity);

            var organizationId = entity.OrganizationId ?? _organizationScope.GetCurrentOrganizationId();
            var roleLookup = _unitOfWork.Repository<Role>().GetAll()
                .Where(x => !organizationId.HasValue || x.OrganizationId == organizationId.Value)
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First().Name);
            var departmentLookup = _unitOfWork.Repository<Department>().GetAll()
                .Where(x => !organizationId.HasValue || x.OrganizationId == organizationId.Value)
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First().Name);
            var actions = _unitOfWork.Repository<PurchaseApprovalAction>().Find(x => x.PurchaseRequestId == id)
                .OrderBy(x => x.StageNumber).ThenBy(x => x.DecisionDate).ToList();
            var stageRoleIds = ResolveStageRoleIds(entity, persistBackfill: false);
            var stageUserIds = ResolveStageUserIds(entity, persistBackfill: false);
            var stageNumber = entity.CurrentApprovalStage <= 0 ? 1 : entity.CurrentApprovalStage;
            var stageRoleId = stageNumber > 0 && stageNumber <= stageRoleIds.Count
                ? (int?)stageRoleIds[stageNumber - 1]
                : null;
            var stageUserId = ApprovalWorkflowSettingsHelper.TryGetCurrentStageUserId(
                ApprovalWorkflowSettingsHelper.SerializeStageUserIds(stageUserIds),
                stageNumber);
            var linkedRecord = _unitOfWork.Repository<PurchaseRecord>().Find(x => x.PurchaseRequestId == id).FirstOrDefault();
            var targetAsset = entity.TargetAssetId.HasValue
                ? _unitOfWork.Repository<Asset>().GetById(entity.TargetAssetId.Value)
                : null;
            var lineItems = _unitOfWork.Repository<PurchaseRequestLine>()
                .Find(x => x.PurchaseRequestId == id && x.IsActive)
                .OrderBy(x => x.LineNumber)
                .Select(x => new PurchaseRequestLineVm
                {
                    LineNumber = x.LineNumber,
                    ItemName = x.ItemName,
                    Description = x.Description,
                    Quantity = x.Quantity,
                    AssetCategoryId = x.AssetCategoryId,
                    AssetTypeId = x.AssetTypeId,
                    AssetSubTypeId = x.AssetSubTypeId,
                    TargetAssetId = x.TargetAssetId
                })
                .ToList();
            foreach (var lineItem in lineItems)
            {
                lineItem.ClassificationLabel = BuildClassificationLabel(lineItem.AssetCategoryId, lineItem.AssetTypeId, lineItem.AssetSubTypeId);
                if (lineItem.TargetAssetId.HasValue)
                {
                    var lineAsset = targetAsset != null && targetAsset.Id == lineItem.TargetAssetId.Value
                        ? targetAsset
                        : _unitOfWork.Repository<Asset>().GetById(lineItem.TargetAssetId.Value);
                    lineItem.TargetAssetTag = lineAsset == null ? null : lineAsset.AssetTag;
                    lineItem.TargetAssetName = lineAsset == null ? null : lineAsset.AssetName;
                }
            }

            string batchLabel = null;
            var siblings = new List<PurchaseRequestSiblingVm>();
            if (entity.PurchaseRequestGroupId.HasValue)
            {
                var group = _unitOfWork.Repository<PurchaseRequestGroup>().GetById(entity.PurchaseRequestGroupId.Value);
                batchLabel = group == null ? null : group.BatchLabel;
                var groupId = entity.PurchaseRequestGroupId.Value;
                siblings = _unitOfWork.Repository<PurchaseRequest>()
                    .Find(x => x.PurchaseRequestGroupId == groupId && x.Id != entity.Id)
                    .Where(CanAccessPurchaseRequest)
                    .OrderBy(x => x.Id)
                    .Select(x => new PurchaseRequestSiblingVm { Id = x.Id, RequestNumber = x.RequestNumber, ApprovalFlowSource = x.ApprovalFlowSource })
                    .ToList();
            }

            return new PurchaseRequestDetailVm
            {
                Id = entity.Id,
                RequestNumber = entity.RequestNumber,
                RequestedById = entity.RequestedById,
                ApprovedById = entity.ApprovedById,
                ApprovalStatus = entity.ApprovalStatus.ToString(),
                DepartmentId = entity.DepartmentId,
                DepartmentName = departmentLookup.ContainsKey(entity.DepartmentId) ? departmentLookup[entity.DepartmentId] : null,
                Justification = entity.Justification,
                ItemDescription = entity.ItemDescription,
                QuantityInStock = entity.QuantityInStock,
                RequiredDate = entity.RequiredDate,
                OrderByUserId = entity.OrderByUserId,
                Quantity = entity.Quantity,
                Currency = entity.Currency,
                Notes = entity.Notes,
                AttachmentFileName = entity.AttachmentFileName,
                AttachmentContentType = entity.AttachmentContentType,
                HasAttachment = !string.IsNullOrWhiteSpace(entity.AttachmentFilePath),
                ApprovedAt = entity.ApprovedAt,
                CreatedAt = entity.CreatedAt,
                CurrentApprovalStage = entity.CurrentApprovalStage,
                CurrentStageRoleId = stageRoleId,
                CurrentStageRoleName = ApprovalWorkflowSettingsHelper.ResolveRoleName(roleLookup, stageRoleId),
                CurrentStageUserId = stageUserId,
                CurrentStageUserName = ResolveApproverDisplayName(stageUserId),
                IsPending = entity.ApprovalStatus == ApprovalStatus.Pending,
                IsApproved = entity.ApprovalStatus == ApprovalStatus.Approved,
                HasPurchaseRecord = linkedRecord != null,
                LinkedPurchaseRecordId = linkedRecord?.Id,
                TargetAssetId = entity.TargetAssetId,
                TargetAssetTag = targetAsset?.AssetTag,
                TargetAssetName = targetAsset?.AssetName,
                PurchaseRequestGroupId = entity.PurchaseRequestGroupId,
                ApprovalFlowSource = entity.ApprovalFlowSource,
                LineItems = lineItems,
                BatchLabel = batchLabel,
                Siblings = siblings,
                ApprovalHistory = ApprovalWorkflowHelper.MapDecisionHistory(
                    actions.Select(x => ApprovalWorkflowHelper.ToSnapshot(
                        x.StageNumber, x.RoleId, x.ApproverUserId, x.Decision, x.Notes, x.DecisionDate)),
                    roleLookup)
            };
        }

        public int Submit(PurchaseRequestCreateVm model, string requestedByUserId)
        {
            return SubmitBatch(model, requestedByUserId).PrimaryId;
        }

        /// <summary>
        /// Catalog requisition flows (stage b): resolves the approval path per line (sub-type > type > category >
        /// department chain > org matrix) and groups lines by path fingerprint. One path = one requisition exactly as
        /// before. Several paths: MixedFlowPolicy AutoSplit (default) creates one requisition per path, linked by a
        /// PurchaseRequestGroup and sharing department, justification, notes and (via the controller) the attachment;
        /// Block rejects the form. Everything is created in one transaction, so a failure leaves no partial batch.
        /// </summary>
        public PurchaseRequestSubmitResult SubmitBatch(PurchaseRequestCreateVm model, string requestedByUserId)
        {
            if (model == null)
            {
                throw new BusinessException("Purchase request is required.");
            }

            if (string.IsNullOrWhiteSpace(requestedByUserId))
            {
                throw new BusinessException("Current user is required.");
            }

            var department = _unitOfWork.Repository<Department>().GetById(model.DepartmentId);
            if (department == null)
            {
                throw new BusinessException("Department not found.");
            }

            _departmentScope.EnsureCanCreateForRequisitionTarget(department);

            if (string.IsNullOrWhiteSpace(model.Justification))
            {
                throw new BusinessException("Justification is required.");
            }

            // Decision A (Allan 2026-10-28 08:39 EAT): never drop a filled-in row that has no description.
            var missingDescriptions = model.FindLinesMissingDescription();
            if (missingDescriptions.Count > 0)
            {
                throw new BusinessException(string.Join(" ", missingDescriptions.Select(PurchaseRequestCreateVm.MissingDescriptionMessage).ToArray()));
            }

            var effectiveLines = model.ResolveEffectiveLines();
            if (effectiveLines.Count == 0)
            {
                throw new BusinessException("Describe at least one item to order.");
            }

            ApplyHeaderSummaryFromLines(model, effectiveLines);

            if (!string.IsNullOrWhiteSpace(model.OrderByUserId))
            {
                var orderByUser = _userService.GetById(model.OrderByUserId);
                if (orderByUser == null || !orderByUser.IsActive)
                {
                    throw new BusinessException("Selected order-by employee was not found or is inactive.");
                }

                if (orderByUser.DepartmentId != model.DepartmentId)
                {
                    throw new BusinessException("Order-by employee must belong to the request department.");
                }
            }

            // Per-line tags (migration 108): every tag is validated (active, same organisation). Back-compat: an older
            // form / link posting only the header TargetAssetId tags line 1.
            ResolveLineTargets(model, effectiveLines, strict: true);

            // Classification policy + normalization (server-side; never trust the client).
            var classified = ClassifyLines(model, effectiveLines);

            // New-line rules (only on a new submit; existing requisitions are never re-validated): name, type, sub-type.
            EnsureNewLineRules(classified);

            // Resolve each line's path and partition by fingerprint (first-appearance order keeps line 1 first).
            var partitions = new List<RequisitionPartition>();
            foreach (var line in classified)
            {
                var resolved = RequisitionFlowResolver.ResolveForLine(_unitOfWork, department, line.Classification);
                var partition = partitions.FirstOrDefault(x => x.Resolved.Fingerprint == resolved.Fingerprint);
                if (partition == null)
                {
                    partition = new RequisitionPartition { Resolved = resolved };
                    partitions.Add(partition);
                }

                partition.Lines.Add(line);
            }

            if (partitions.Count > 1
                && RequisitionPolicySettings.GetMixedFlowPolicy(_unitOfWork) == RequisitionMixedFlowPolicy.Block)
            {
                throw new BusinessException(
                    "These items use different approval paths ("
                    + string.Join(", ", partitions.Select(x => x.Resolved.SourceLabel).Distinct().ToArray())
                    + "). Remove lines or submit them separately.");
            }

            // Validate every path before creating anything (no partial batch on misconfiguration).
            foreach (var partition in partitions)
            {
                EnsureUsableConfiguration(partition.Resolved.Configuration);
            }

            var result = new PurchaseRequestSubmitResult();
            Action create = () =>
            {
                int? groupId = null;
                if (partitions.Count > 1)
                {
                    var group = new PurchaseRequestGroup
                    {
                        OrganizationId = department.OrganizationId ?? _organizationScope.GetCurrentOrganizationId(),
                        BatchLabel = "PENDING",
                        CreatedByUserId = requestedByUserId.Trim(),
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    _unitOfWork.Repository<PurchaseRequestGroup>().Add(group);
                    _unitOfWork.SaveChanges();
                    group.BatchLabel = "BATCH-" + group.Id.ToString("D6");
                    group.UpdatedAt = DateTime.UtcNow;
                    _unitOfWork.Repository<PurchaseRequestGroup>().Update(group);
                    _unitOfWork.SaveChanges();
                    groupId = group.Id;
                    result.GroupId = group.Id;
                    result.BatchLabel = group.BatchLabel;
                }

                for (var i = 0; i < partitions.Count; i++)
                {
                    var partition = partitions[i];
                    var entity = CreateRequisition(
                        model,
                        partition.Lines,
                        partition.Resolved,
                        requestedByUserId.Trim(),
                        // Header tag (back-compat for lists, Details and older PO/receiving code): the first tagged line
                        // of THIS requisition, so each split requisition keeps the tags of its own lines.
                        partition.Lines.Select(x => x.Line.TargetAssetId).FirstOrDefault(x => x.HasValue),
                        groupId);
                    result.RequestIds.Add(entity.Id);
                    result.RequestNumbers.Add(entity.RequestNumber);
                    result.FlowSources.Add(partition.Resolved.SourceLabel);
                }
            };

            if (partitions.Count > 1)
            {
                _unitOfWork.ExecuteInTransaction(create);
                _auditWriter.Write(
                    "Purchases.RequestGroup",
                    nameof(PurchaseRequestGroup),
                    result.GroupId.ToString(),
                    null,
                    "AutoSplit:" + string.Join(",", result.RequestNumbers.ToArray()));
            }
            else
            {
                create();
            }

            return result;
        }

        public PurchaseRequestPathPreviewVm PreviewPaths(PurchaseRequestCreateVm model)
        {
            var result = new PurchaseRequestPathPreviewVm
            {
                ClassificationMode = RequisitionPolicySettings.GetClassificationMode(_unitOfWork),
                MixedFlowPolicy = RequisitionPolicySettings.GetMixedFlowPolicy(_unitOfWork)
            };
            if (model == null)
            {
                return result;
            }

            // Keep every posted row (blank description = placeholder) so line numbers match the form rows.
            var lines = (model.Lines ?? new List<PurchaseRequestLineCreateVm>())
                .Select(x => new PurchaseRequestLineCreateVm
                {
                    ItemName = x == null ? null : x.ItemName,
                    Description = x == null || string.IsNullOrWhiteSpace(x.Description) ? "-" : x.Description,
                    Quantity = x == null || x.Quantity <= 0 ? 1 : x.Quantity,
                    AssetCategoryId = x == null ? null : x.AssetCategoryId,
                    AssetTypeId = x == null ? null : x.AssetTypeId,
                    AssetSubTypeId = x == null ? null : x.AssetSubTypeId,
                    TargetAssetId = x == null || !x.TargetAssetId.HasValue || x.TargetAssetId.Value <= 0 ? null : x.TargetAssetId
                })
                .ToList();
            if (lines.Count == 0)
            {
                lines.Add(new PurchaseRequestLineCreateVm { Description = "-", Quantity = 1 });
            }

            try
            {
                Department department = null;
                if (model.DepartmentId > 0)
                {
                    department = _unitOfWork.Repository<Department>().GetById(model.DepartmentId);
                    if (department != null)
                    {
                        _departmentScope.EnsureCanCreateForRequisitionTarget(department);
                    }
                }

                // Submit reports a bad tag; the preview just ignores it. Name / sub-type rules are not checked here.
                ResolveLineTargets(model, lines, strict: false);
                var classified = ClassifyLines(model, lines);
                for (var i = 0; i < classified.Count; i++)
                {
                    var resolved = RequisitionFlowResolver.ResolveForLine(_unitOfWork, department, classified[i].Classification);
                    var group = result.Groups.FirstOrDefault(x => x.SourceKind == resolved.Fingerprint);
                    if (group == null)
                    {
                        group = new PurchaseRequestPathPreviewGroupVm
                        {
                            GroupNumber = result.Groups.Count + 1,
                            SourceLabel = resolved.SourceLabel,
                            // SourceKind carries the fingerprint while grouping; replaced below.
                            SourceKind = resolved.Fingerprint,
                            Configuration = resolved.Configuration
                        };
                        result.Groups.Add(group);
                    }

                    group.LineNumbers.Add(i + 1);
                    result.Lines.Add(new PurchaseRequestPathPreviewLineVm
                    {
                        LineNumber = i + 1,
                        GroupNumber = group.GroupNumber,
                        SourceLabel = resolved.SourceLabel
                    });
                }
            }
            catch (BusinessException ex)
            {
                result.Error = ex.Message;
                result.Lines.Clear();
                result.Groups.Clear();
            }

            foreach (var group in result.Groups)
            {
                group.SourceKind = null;
            }

            return result;
        }

        public PurchaseRequestBatchVm GetBatch(int groupId)
        {
            var group = _unitOfWork.Repository<PurchaseRequestGroup>().GetById(groupId);
            if (group == null)
            {
                return null;
            }

            var all = _unitOfWork.Repository<PurchaseRequest>()
                .Find(x => x.PurchaseRequestGroupId == groupId)
                .OrderBy(x => x.Id)
                .ToList();
            var visible = all.Where(CanAccessPurchaseRequest).ToList();
            if (visible.Count == 0)
            {
                return null;
            }

            var organizationId = group.OrganizationId ?? _organizationScope.GetCurrentOrganizationId();
            var departmentNames = _unitOfWork.Repository<Department>().GetAll()
                .Where(x => !organizationId.HasValue || x.OrganizationId == organizationId.Value)
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First().Name);
            var result = new PurchaseRequestBatchVm
            {
                GroupId = group.Id,
                BatchLabel = group.BatchLabel,
                CreatedAt = group.CreatedAt,
                CreatedByUserId = group.CreatedByUserId,
                CreatedByName = ResolveApproverDisplayName(group.CreatedByUserId),
                HiddenCount = all.Count - visible.Count
            };

            foreach (var request in visible)
            {
                var requestId = request.Id;
                var lines = _unitOfWork.Repository<PurchaseRequestLine>()
                    .Find(x => x.PurchaseRequestId == requestId && x.IsActive)
                    .ToList();
                result.Requests.Add(new PurchaseRequestBatchItemVm
                {
                    Id = request.Id,
                    RequestNumber = request.RequestNumber,
                    ApprovalStatus = request.ApprovalStatus.ToString(),
                    CurrentApprovalStage = request.CurrentApprovalStage,
                    StageCount = ResolveStageRoleIds(request, persistBackfill: false).Count,
                    ApprovalFlowSource = request.ApprovalFlowSource,
                    ItemSummary = request.ItemDescription,
                    LineCount = lines.Count == 0 ? 1 : lines.Count,
                    TotalQuantity = lines.Count == 0 ? request.Quantity : lines.Sum(x => x.Quantity),
                    DepartmentName = departmentNames.ContainsKey(request.DepartmentId) ? departmentNames[request.DepartmentId] : null,
                    HasPurchaseRecord = _unitOfWork.Repository<PurchaseRecord>().Find(x => x.PurchaseRequestId == requestId).Any()
                });
            }

            return result;
        }

        private string BuildClassificationLabel(int? categoryId, int? typeId, int? subTypeId)
        {
            var parts = new List<string>();
            if (categoryId.HasValue)
            {
                parts.Add(RequisitionFlowResolver.GetCatalogName(_unitOfWork, CatalogFlowScope.Category, categoryId.Value));
            }

            if (typeId.HasValue)
            {
                parts.Add(RequisitionFlowResolver.GetCatalogName(_unitOfWork, CatalogFlowScope.Type, typeId.Value));
            }

            if (subTypeId.HasValue)
            {
                parts.Add(RequisitionFlowResolver.GetCatalogName(_unitOfWork, CatalogFlowScope.SubType, subTypeId.Value));
            }

            var names = parts.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            return names.Length == 0 ? null : string.Join(" > ", names);
        }

        private sealed class ClassifiedLine
        {
            public PurchaseRequestLineCreateVm Line { get; set; }

            public RequisitionLineClassification Classification { get; set; }
        }

        private sealed class RequisitionPartition
        {
            public RequisitionFlowResolveResult Resolved { get; set; }

            public IList<ClassifiedLine> Lines { get; } = new List<ClassifiedLine>();
        }

        private IList<ClassifiedLine> ClassifyLines(
            PurchaseRequestCreateVm model,
            IList<PurchaseRequestLineCreateVm> effectiveLines)
        {
            var mode = RequisitionPolicySettings.GetClassificationMode(_unitOfWork);
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            var header = RequisitionLineClassification.Create(
                model.HeaderAssetCategoryId,
                model.HeaderAssetTypeId,
                model.HeaderAssetSubTypeId);

            if (mode == RequisitionClassificationMode.RequiredHeader && !header.HasTypeOrSubType)
            {
                // A tagged asset may stand in for the header pick (its type/sub-type): the first tagged line's asset.
                header = ClassificationFromAsset(effectiveLines.Select(x => x.TargetAssetId).FirstOrDefault(x => x.HasValue)) ?? header;
                if (!header.HasTypeOrSubType)
                {
                    throw new BusinessException("Choose an asset type or sub-type for this requisition.");
                }
            }

            var result = new List<ClassifiedLine>();
            for (var i = 0; i < effectiveLines.Count; i++)
            {
                var line = effectiveLines[i];
                // A tagged line takes its classification from the tagged asset (posted picks are ignored). Untagged:
                // the category is derived from the type / sub-type, never taken from the client when one is picked.
                var classification = ClassificationFromAsset(line.TargetAssetId);
                if (classification == null)
                {
                    var hasTypeOrSubType = (line.AssetTypeId.HasValue && line.AssetTypeId.Value > 0)
                        || (line.AssetSubTypeId.HasValue && line.AssetSubTypeId.Value > 0);
                    classification = RequisitionLineClassification.Create(
                        hasTypeOrSubType ? null : line.AssetCategoryId,
                        line.AssetTypeId,
                        line.AssetSubTypeId);
                }

                if (!classification.HasAny && mode == RequisitionClassificationMode.RequiredHeader)
                {
                    classification = header.Clone();
                }

                if (mode == RequisitionClassificationMode.RequiredPerLine && !classification.HasTypeOrSubType)
                {
                    throw new BusinessException("Line " + (i + 1) + ": choose an asset type or sub-type.");
                }

                classification = CatalogRequisitionFlowResolver.Normalize(
                    classification,
                    id => _unitOfWork.Repository<AssetSubType>().GetById(id),
                    id => _unitOfWork.Repository<AssetType>().GetById(id),
                    id => _unitOfWork.Repository<AssetCategory>().GetById(id),
                    organizationId,
                    "Line " + (i + 1));

                result.Add(new ClassifiedLine { Line = line, Classification = classification });
            }

            return result;
        }

        /// <summary>
        /// Validates each line's TargetAssetId (strict: throws like the old header check; preview: drops a bad tag) and
        /// applies the back-compat header TargetAssetId to line 1 when no line carries its own tag.
        /// </summary>
        private void ResolveLineTargets(PurchaseRequestCreateVm model, IList<PurchaseRequestLineCreateVm> lines, bool strict)
        {
            if (lines == null || lines.Count == 0)
            {
                return;
            }

            if (!lines.Any(x => x.TargetAssetId.HasValue && x.TargetAssetId.Value > 0)
                && model.TargetAssetId.HasValue
                && model.TargetAssetId.Value > 0)
            {
                lines[0].TargetAssetId = model.TargetAssetId;
            }

            for (var i = 0; i < lines.Count; i++)
            {
                try
                {
                    lines[i].TargetAssetId = ResolveTargetAssetId(lines[i].TargetAssetId);
                }
                catch (BusinessException ex)
                {
                    if (strict)
                    {
                        throw new BusinessException("Line " + (i + 1) + ": " + ex.Message);
                    }

                    lines[i].TargetAssetId = null;
                }
            }
        }

        /// <summary>
        /// Rules for NEW requisition lines (submit only; stored requisitions, POs and receipts are never re-checked):
        /// untagged line = item / asset name + asset type + sub-type required (category derived from the type);
        /// tagged line = name defaults to the tagged asset's name (editable), classification from the asset.
        /// The line Description stays required: untouched blank rows are ignored, and a filled-in row without one was
        /// already rejected by SubmitBatch (decision A, PurchaseRequestCreateVm.FindLinesMissingDescription).
        /// </summary>
        private void EnsureNewLineRules(IList<ClassifiedLine> classified)
        {
            for (var i = 0; i < classified.Count; i++)
            {
                var line = classified[i].Line;
                var label = "Line " + (i + 1);
                var name = AssetNameRules.Normalize(line.ItemName);
                if (line.TargetAssetId.HasValue)
                {
                    if (name == null)
                    {
                        var asset = _unitOfWork.Repository<Asset>().GetById(line.TargetAssetId.Value);
                        name = AssetNameRules.Fit(asset == null ? null : asset.AssetName);
                    }
                }
                else
                {
                    var classification = classified[i].Classification;
                    if (classification == null
                        || !classification.AssetTypeId.HasValue
                        || !classification.AssetSubTypeId.HasValue)
                    {
                        if (classification != null
                            && classification.AssetTypeId.HasValue
                            && !new PurchaseOrderLedger(_unitOfWork).TypeHasActiveSubTypes(classification.AssetTypeId.Value))
                        {
                            // Same wording as receiving (ReceivingService.RequireSubType).
                            throw new BusinessException(label + ": this asset type has no active sub-types yet. Add one under Asset Categories first; every requisitioned asset needs a sub-type.");
                        }

                        throw new BusinessException(label + ": choose the asset type and sub-type.");
                    }
                }

                if (name == null)
                {
                    throw new BusinessException(label + ": enter the item / asset name.");
                }

                if (name.Length > AssetNameRules.MaxLength)
                {
                    throw new BusinessException(label + ": the item / asset name cannot be longer than " + AssetNameRules.MaxLength + " characters (" + name.Length + " entered).");
                }

                line.ItemName = name;
            }
        }

        private RequisitionLineClassification ClassificationFromAsset(int? targetAssetId)
        {
            if (!targetAssetId.HasValue)
            {
                return null;
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(targetAssetId.Value);
            if (asset == null || asset.AssetTypeId <= 0)
            {
                return null;
            }

            // Best effort: a tagged asset whose type/sub-type is inactive or otherwise unusable simply does not
            // pre-fill (the requisition then resolves as before) instead of blocking the submit.
            try
            {
                var classification = CatalogRequisitionFlowResolver.Normalize(
                    RequisitionLineClassification.Create(null, asset.AssetTypeId, asset.AssetSubTypeId),
                    id => _unitOfWork.Repository<AssetSubType>().GetById(id),
                    id => _unitOfWork.Repository<AssetType>().GetById(id),
                    id => _unitOfWork.Repository<AssetCategory>().GetById(id),
                    _organizationScope.GetCurrentOrganizationId(),
                    null);
                return classification.HasAny ? classification : null;
            }
            catch (BusinessException)
            {
                return null;
            }
        }

        private static void EnsureUsableConfiguration(ApprovalProcessConfiguration approvalConfig)
        {
            if (!approvalConfig.UsesApproval && approvalConfig.RequiresApproval)
            {
                throw new BusinessException(
                    "Requisition approval is enabled but Stage 1 approver role is not configured. "
                    + "Go to Settings → Approval Matrix, assign a role for Requisition (e.g. Procurement Officer), and save.");
            }
        }

        private PurchaseRequest CreateRequisition(
            PurchaseRequestCreateVm model,
            IList<ClassifiedLine> lines,
            RequisitionFlowResolveResult resolved,
            string requestedByUserId,
            int? targetAssetId,
            int? groupId)
        {
            var approvalConfig = resolved.Configuration;
            var summary = SummarizeLines(lines.Select(x => x.Line).ToList());
            var entity = new PurchaseRequest
            {
                RequestNumber = "PENDING",
                RequestedById = requestedByUserId,
                DepartmentId = model.DepartmentId,
                ItemDescription = summary.Key,
                Justification = (model.Justification ?? string.Empty).Trim(),
                QuantityInStock = model.QuantityInStock,
                RequiredDate = model.RequiredDate,
                OrderByUserId = string.IsNullOrWhiteSpace(model.OrderByUserId) ? null : model.OrderByUserId.Trim(),
                Quantity = summary.Value,
                Currency = string.IsNullOrWhiteSpace(model.Currency)
                    ? ApprovalWorkflowSettingsHelper.GetDefaultCurrencyCode(_unitOfWork.Repository<SystemSetting>().GetAll())
                    : model.Currency.Trim().ToUpperInvariant(),
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
                TargetAssetId = targetAssetId,
                PurchaseRequestGroupId = groupId,
                ApprovalFlowSource = Truncate(resolved.SourceLabel, 200),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            if (!approvalConfig.UsesApproval)
            {
                entity.ApprovalStatus = ApprovalStatus.Approved;
                entity.CurrentApprovalStage = 0;
                entity.ApprovalStageRoleIds = null;
                entity.ApprovalStageUserIds = null;
                entity.ApprovedById = requestedByUserId;
                entity.ApprovedAt = DateTime.UtcNow;
                _unitOfWork.Repository<PurchaseRequest>().Add(entity);
                _unitOfWork.SaveChanges();
                entity.RequestNumber = "PR-" + entity.Id.ToString("D6");
                entity.UpdatedAt = DateTime.UtcNow;
                PersistLineItems(entity, lines);
                _unitOfWork.Repository<PurchaseRequest>().Update(entity);
                _unitOfWork.SaveChanges();
                NotificationHelper.AddNotification(
                    _unitOfWork,
                    _outboxWriter,
                    _organizationScope,
                    requestedByUserId,
                    NotificationType.General,
                    "Purchase request approved",
                    "Purchase request " + entity.RequestNumber + " was approved automatically (approval workflow disabled).",
                    "/PurchaseRequests/Details/" + entity.Id);
                _unitOfWork.SaveChanges();
                _auditWriter.Write("Purchases.Request", nameof(PurchaseRequest), entity.Id.ToString(), null, "AutoApproved");
                return entity;
            }

            // Snapshot guard: never copy another organisation's role/person ids onto the requisition.
            NewReferenceGuard().EnsureConfigurationInOrganization(
                _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId(),
                approvalConfig,
                "Purchases.Request",
                nameof(PurchaseRequest),
                null,
                checkUsers: false); // persons are sanitized just below (foreign/inactive -> any user in role)

            entity.ApprovalStatus = ApprovalStatus.Pending;
            entity.CurrentApprovalStage = 1;
            entity.ApprovalStageRoleIds = ApprovalWorkflowSettingsHelper.SerializeStageRoleIds(approvalConfig.StageRoleIds.Select(x => (int?)x));
            // Snapshot: a configured person is kept only while active and still in the stage role (else any role holder).
            var snapshotUserIds = StageApproverRules.SanitizeForSnapshot(
                approvalConfig.StageRoleIds,
                approvalConfig.StageUserIds,
                FindApproverUser);
            entity.ApprovalStageUserIds = ApprovalWorkflowSettingsHelper.SerializeStageUserIds(snapshotUserIds);
            entity.ApprovedAt = null;
            entity.ApprovedById = null;
            _unitOfWork.Repository<PurchaseRequest>().Add(entity);
            _unitOfWork.SaveChanges();
            entity.RequestNumber = "PR-" + entity.Id.ToString("D6");
            entity.UpdatedAt = DateTime.UtcNow;
            PersistLineItems(entity, lines);
            _unitOfWork.Repository<PurchaseRequest>().Update(entity);
            _unitOfWork.SaveChanges();
            NotificationHelper.AddNotification(
                _unitOfWork,
                _outboxWriter,
                _organizationScope,
                requestedByUserId,
                NotificationType.PendingApproval,
                "Purchase request submitted",
                "Purchase request " + entity.RequestNumber + " is pending approval.",
                "/PurchaseRequests/Details/" + entity.Id);
            NotificationHelper.AddStageApproverNotification(
                _unitOfWork,
                _outboxWriter,
                _organizationScope,
                _userService,
                approvalConfig.StageRoleIds.FirstOrDefault(),
                ApprovalWorkflowHelper.GetStageUserId(snapshotUserIds, 1),
                entity.DepartmentId,
                "Purchase approval required",
                "Purchase request " + entity.RequestNumber + " is awaiting Stage 1 approval.",
                "/PurchaseRequests/Details/" + entity.Id,
                Helpers.CatalogRequisitionFlowResolver.IsCatalogSource(entity.ApprovalFlowSource));
            _unitOfWork.SaveChanges();
            _auditWriter.Write("Purchases.Request", nameof(PurchaseRequest), entity.Id.ToString(), null, "PendingApproval");
            return entity;
        }

        private static KeyValuePair<string, int> SummarizeLines(IList<PurchaseRequestLineCreateVm> lines)
        {
            var quantity = lines.Sum(x => x.Quantity);
            var description = lines.Count == 1
                ? lines[0].Description
                : string.Join("; ", lines.Select(x => x.Description).ToArray());
            return new KeyValuePair<string, int>(Truncate((description ?? string.Empty).Trim(), 2000), quantity);
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength - 3) + "...";
        }

        public void SaveAttachment(int purchaseRequestId, PurchaseRequestAttachmentInfo attachment)
        {
            if (attachment == null || string.IsNullOrWhiteSpace(attachment.FilePath))
            {
                return;
            }

            var entity = _unitOfWork.Repository<PurchaseRequest>().GetById(purchaseRequestId);
            if (entity == null)
            {
                throw new BusinessException("Purchase request not found.");
            }

            EnsureCanAccessPurchaseRequest(entity);
            entity.AttachmentFileName = attachment.FileName;
            entity.AttachmentFilePath = attachment.FilePath;
            entity.AttachmentContentType = attachment.ContentType;
            entity.AttachmentFileSizeBytes = attachment.FileSizeBytes;
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<PurchaseRequest>().Update(entity);
            _unitOfWork.SaveChanges();
        }

        public string GetAttachmentRelativePath(int purchaseRequestId)
        {
            var entity = _unitOfWork.Repository<PurchaseRequest>().GetById(purchaseRequestId);
            if (entity == null || string.IsNullOrWhiteSpace(entity.AttachmentFilePath))
            {
                return null;
            }

            EnsureCanAccessPurchaseRequest(entity);
            return entity.AttachmentFilePath;
        }

        public void Approve(PurchaseRequestApprovalVm model, string approvedByUserId, int? approverRoleId, bool isSuperAdmin)
        {
            if (model == null)
            {
                throw new BusinessException("Approval payload is required.");
            }

            var request = GetPendingRequest(model.PurchaseRequestId);
            EnsureCanAccessPurchaseRequest(request);
            var stageRoleIds = ResolveStageRoleIds(request, persistBackfill: true);
            var stageUserIds = ResolveStageUserIds(request, persistBackfill: true);
            var stageNumber = request.CurrentApprovalStage <= 0 ? 1 : request.CurrentApprovalStage;
            var expectedRoleId = ApprovalWorkflowHelper.GetStageRoleId(stageRoleIds, stageNumber, ApprovalProcessCodes.Purchase);
            var expectedUserId = ApprovalWorkflowHelper.GetStageUserId(stageUserIds, stageNumber);
            int? requestDepartmentId = request.DepartmentId;
            var isFinalStage = stageNumber >= stageRoleIds.Count;
            var poRaiserConfig = isFinalStage ? PurchaseOrderRaiserRules.ResolveForRequest(_unitOfWork, request) : null;

            _approvalEngine.ExecuteStageDecision(new ApprovalStageDecisionRequest
            {
                ProcessCode = ApprovalProcessCodes.Purchase,
                ActingUserId = approvedByUserId,
                ApproverRoleId = approverRoleId,
                IsSuperAdmin = isSuperAdmin,
                RequesterUserId = request.RequestedById,
                Notes = NormalizeText(model.Notes),
                Decision = ApprovalStatus.Approved,
                RequestEntity = request,
                RequestEntityType = typeof(PurchaseRequest),
                StageNumber = stageNumber,
                StageRoleIds = stageRoleIds,
                ExpectedRoleId = expectedRoleId,
                ExpectedUserId = expectedUserId,
                ApproverScope = StageApproverScopeResolver.ResolveFor(_unitOfWork, _userService, expectedRoleId, expectedUserId, requestDepartmentId, Helpers.CatalogRequisitionFlowResolver.IsCatalogSource(request.ApprovalFlowSource)),
                ApprovalActionEntity = new PurchaseApprovalAction
                {
                    PurchaseRequestId = request.Id,
                    StageNumber = stageNumber,
                    RoleId = expectedRoleId,
                    ApproverUserId = approvedByUserId,
                    Decision = ApprovalStatus.Approved,
                    Notes = NormalizeText(model.Notes),
                    DecisionDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                },
                WriteNotifications = uow =>
                {
                    if (isFinalStage && PurchaseOrderRaiserRules.IsConfigured(poRaiserConfig))
                    {
                        WriteReadyForPurchaseOrderNotifications(uow, request, poRaiserConfig);
                    }
                    else if (isFinalStage)
                    {
                        NotificationHelper.AddNotification(
                            uow,
                            _outboxWriter,
                            _organizationScope,
                            request.RequestedById,
                            NotificationType.General,
                            "Purchase request approved",
                            "Purchase request " + request.RequestNumber + " has been fully approved. You can record the purchase order when ready.",
                            "/PurchaseRequests/Details/" + request.Id);
                    }
                    else
                    {
                        NotificationHelper.AddNotification(
                            uow,
                            _outboxWriter,
                            _organizationScope,
                            request.RequestedById,
                            NotificationType.PendingApproval,
                            "Purchase stage approved",
                            "Purchase request " + request.RequestNumber + " moved to Stage " + request.CurrentApprovalStage + " approval.",
                            "/PurchaseRequests/Details/" + request.Id);
                        NotificationHelper.AddStageApproverNotification(
                            uow,
                            _outboxWriter,
                            _organizationScope,
                            _userService,
                            ApprovalWorkflowHelper.GetStageRoleId(stageRoleIds, request.CurrentApprovalStage, ApprovalProcessCodes.Purchase),
                            ApprovalWorkflowHelper.GetStageUserId(stageUserIds, request.CurrentApprovalStage),
                            requestDepartmentId,
                            "Purchase approval stage advanced",
                            "Purchase request " + request.RequestNumber + " is now awaiting Stage " + request.CurrentApprovalStage + " approval.",
                            "/PurchaseRequests/Details/" + request.Id,
                            Helpers.CatalogRequisitionFlowResolver.IsCatalogSource(request.ApprovalFlowSource));
                    }
                }
            });

            _auditWriter.Write(
                "Purchases.Approve",
                nameof(PurchaseRequest),
                request.Id.ToString(),
                isFinalStage ? "PendingApproval" : "Stage" + stageNumber,
                isFinalStage ? "Approved" : "Stage" + request.CurrentApprovalStage);

            if (isFinalStage)
            {
                _webhookService.QueueDelivery(
                    "purchase.approved",
                    "{\"purchaseRequestId\":" + request.Id + ",\"requestNumber\":\"" + (request.RequestNumber ?? string.Empty) + "\"}");
            }
        }

        public void Reject(PurchaseRequestApprovalVm model, string rejectedByUserId, int? approverRoleId, bool isSuperAdmin)
        {
            if (model == null)
            {
                throw new BusinessException("Rejection payload is required.");
            }

            var request = GetPendingRequest(model.PurchaseRequestId);
            EnsureCanAccessPurchaseRequest(request);
            var stageRoleIds = ResolveStageRoleIds(request, persistBackfill: true);
            var stageUserIds = ResolveStageUserIds(request, persistBackfill: true);
            var stageNumber = request.CurrentApprovalStage <= 0 ? 1 : request.CurrentApprovalStage;
            var expectedRoleId = ApprovalWorkflowHelper.GetStageRoleId(stageRoleIds, stageNumber, ApprovalProcessCodes.Purchase);
            var expectedUserId = ApprovalWorkflowHelper.GetStageUserId(stageUserIds, stageNumber);
            int? requestDepartmentId = request.DepartmentId;

            _approvalEngine.ExecuteStageDecision(new ApprovalStageDecisionRequest
            {
                ProcessCode = ApprovalProcessCodes.Purchase,
                ActingUserId = rejectedByUserId,
                ApproverRoleId = approverRoleId,
                IsSuperAdmin = isSuperAdmin,
                RequesterUserId = request.RequestedById,
                Notes = NormalizeText(model.Notes),
                Decision = ApprovalStatus.Rejected,
                RequestEntity = request,
                RequestEntityType = typeof(PurchaseRequest),
                StageNumber = stageNumber,
                StageRoleIds = stageRoleIds,
                ExpectedRoleId = expectedRoleId,
                ExpectedUserId = expectedUserId,
                ApproverScope = StageApproverScopeResolver.ResolveFor(_unitOfWork, _userService, expectedRoleId, expectedUserId, requestDepartmentId, Helpers.CatalogRequisitionFlowResolver.IsCatalogSource(request.ApprovalFlowSource)),
                ApprovalActionEntity = new PurchaseApprovalAction
                {
                    PurchaseRequestId = request.Id,
                    StageNumber = stageNumber,
                    RoleId = expectedRoleId,
                    ApproverUserId = rejectedByUserId,
                    Decision = ApprovalStatus.Rejected,
                    Notes = NormalizeText(model.Notes),
                    DecisionDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                },
                WriteNotifications = uow =>
                {
                    NotificationHelper.AddNotification(
                        uow,
                        _outboxWriter,
                        _organizationScope,
                        request.RequestedById,
                        NotificationType.General,
                        "Purchase request rejected",
                        "Purchase request " + request.RequestNumber + " was rejected at Stage " + stageNumber + ".",
                        "/PurchaseRequests/Details/" + request.Id);
                }
            });

            _auditWriter.Write("Purchases.Approve", nameof(PurchaseRequest), request.Id.ToString(), "PendingApproval", "Rejected");
        }

        private PurchaseRequest GetPendingRequest(int id)
        {
            var request = _unitOfWork.Repository<PurchaseRequest>().GetById(id);
            if (request == null)
            {
                throw new BusinessException("Purchase request not found.");
            }

            if (request.ApprovalStatus != ApprovalStatus.Pending)
            {
                throw new BusinessException("This purchase request is no longer pending approval.");
            }

            return request;
        }

        private IList<int> ResolveStageRoleIds(PurchaseRequest request, bool persistBackfill)
        {
            var stageRoleIds = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(request.ApprovalStageRoleIds);
            if (stageRoleIds.Count > 0)
            {
                return stageRoleIds;
            }

            var department = _unitOfWork.Repository<Department>().GetById(request.DepartmentId);
            var config = ApprovalWorkflowHelper.GetDepartmentRequisitionConfiguration(_unitOfWork, department);
            if (!config.UsesApproval)
            {
                throw new BusinessException(
                    "This requisition has no approval stages recorded and requisition approval is not configured. "
                    + "Go to Settings → Approval Matrix, enable Requisition, assign Stage 1 (e.g. Procurement Officer), save, then resubmit.");
            }

            if (persistBackfill)
            {
                NewReferenceGuard().EnsureConfigurationInOrganization(
                    request.OrganizationId ?? (_organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId()),
                    config,
                    "Purchases.BackfillStages",
                    nameof(PurchaseRequest),
                    request.Id.ToString());
                request.ApprovalStageRoleIds = ApprovalWorkflowSettingsHelper.SerializeStageRoleIds(
                    config.StageRoleIds.Select(x => (int?)x));
                if (config.StageUserIds != null && config.StageUserIds.Count > 0)
                {
                    request.ApprovalStageUserIds = ApprovalWorkflowSettingsHelper.SerializeStageUserIds(config.StageUserIds);
                }
                if (request.CurrentApprovalStage <= 0)
                {
                    request.CurrentApprovalStage = 1;
                }

                request.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Repository<PurchaseRequest>().Update(request);
                _unitOfWork.SaveChanges();
            }

            return config.StageRoleIds;
        }

        private IList<string> ResolveStageUserIds(PurchaseRequest request, bool persistBackfill)
        {
            var stageUserIds = ApprovalWorkflowSettingsHelper.ParseStageUserIds(request.ApprovalStageUserIds);
            if (stageUserIds.Count > 0 || !string.IsNullOrWhiteSpace(request.ApprovalStageRoleIds))
            {
                // A submitted requisition keeps the approval path it was submitted with: when the stage roles were
                // snapshotted, the person snapshot (possibly empty = any role holder) is authoritative. Only legacy
                // rows with no stage snapshot at all fall back to the live department configuration below.
                return stageUserIds;
            }

            var department = _unitOfWork.Repository<Department>().GetById(request.DepartmentId);
            var config = ApprovalWorkflowHelper.GetDepartmentRequisitionConfiguration(_unitOfWork, department);
            if (!config.UsesApproval)
            {
                return stageUserIds;
            }

            if (persistBackfill && config.StageUserIds != null && config.StageUserIds.Count > 0)
            {
                NewReferenceGuard().EnsureConfigurationInOrganization(
                    request.OrganizationId ?? (_organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId()),
                    config,
                    "Purchases.BackfillStages",
                    nameof(PurchaseRequest),
                    request.Id.ToString());
                request.ApprovalStageUserIds = ApprovalWorkflowSettingsHelper.SerializeStageUserIds(config.StageUserIds);
                request.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Repository<PurchaseRequest>().Update(request);
                _unitOfWork.SaveChanges();
            }

            return config.StageUserIds ?? new List<string>();
        }

        private OrgReferenceValidator NewReferenceGuard()
        {
            return new OrgReferenceValidator(_unitOfWork, _auditWriter, null, _userService);
        }

        private ApproverUserRecord FindApproverUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId) || _userService == null)
            {
                return null;
            }

            return PurchaseOrderRaiserRules.ToRecord(_userService.GetById(userId.Trim()), _organizationScope.GetCurrentOrganizationId());
        }

        /// <summary>
        /// Final approval with a configured PO raiser: only the configured person (or active holders of the
        /// configured role) get "ready for purchase order"; the requester is told who will raise it.
        /// </summary>
        private void WriteReadyForPurchaseOrderNotifications(IUnitOfWork uow, PurchaseRequest request, ApprovalProcessConfiguration poRaiserConfig)
        {
            // rel1009e: same scoped resolver as the PO-raise permission check (PurchaseService / Details button).
            var raiserScope = PurchaseOrderRaiserRules.ResolveRaiserScope(_unitOfWork, _userService, poRaiserConfig, request.DepartmentId, Helpers.CatalogRequisitionFlowResolver.IsCatalogSource(request.ApprovalFlowSource));
            var raiserIds = raiserScope == null
                ? PurchaseOrderRaiserRules.ResolveNotifyUserIds(poRaiserConfig, new List<ApproverUserRecord>())
                : raiserScope.UserIds;
            var requesterId = string.IsNullOrWhiteSpace(request.RequestedById) ? null : request.RequestedById.Trim();
            var requesterIsRaiser = requesterId != null
                && raiserIds.Any(x => string.Equals(x, requesterId, StringComparison.OrdinalIgnoreCase));
            var raiserLabel = PurchaseOrderRaiserRules.Describe(_unitOfWork, _userService, poRaiserConfig);
            var link = "/PurchaseRequests/Details/" + request.Id;

            NotificationHelper.AddNotification(
                uow,
                _outboxWriter,
                _organizationScope,
                request.RequestedById,
                NotificationType.General,
                "Purchase request approved",
                requesterIsRaiser
                    ? "Purchase request " + request.RequestNumber + " has been fully approved. You can record the purchase order when ready."
                    : "Purchase request " + request.RequestNumber + " has been fully approved. The purchase order will be raised by " + raiserLabel + ".",
                link);

            foreach (var raiserId in raiserIds)
            {
                if (requesterIsRaiser && string.Equals(raiserId, requesterId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                NotificationHelper.AddNotification(
                    uow,
                    _outboxWriter,
                    _organizationScope,
                    raiserId,
                    NotificationType.General,
                    "Ready for purchase order",
                    "Purchase request " + request.RequestNumber + " is fully approved and ready for a purchase order. You are configured to raise it (" + raiserLabel + ").",
                    link);
            }
        }

        private string ResolveApproverDisplayName(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId) || _userService == null)
            {
                return userId;
            }

            var user = _userService.GetById(userId);
            if (user == null)
            {
                return userId;
            }

            var name = ((user.FirstName ?? string.Empty) + " " + (user.LastName ?? string.Empty)).Trim();
            return string.IsNullOrWhiteSpace(name) ? (user.Email ?? userId) : name;
        }

        private bool CanApprovePurchases()
        {
            var userId = _currentUser == null ? null : _currentUser.UserId;
            return !string.IsNullOrWhiteSpace(userId)
                && _authorizationService != null
                && _authorizationService.HasPermission(userId, "Purchases.Approve");
        }

        private bool CanAccessPurchaseRequest(PurchaseRequest request)
        {
            if (request == null || _departmentScope.BypassesDepartmentScope || CanApprovePurchases())
            {
                return true;
            }

            var departmentId = _departmentScope.ScopedDepartmentId;
            if (!departmentId.HasValue)
            {
                return true;
            }

            return request.DepartmentId == departmentId.Value || IsCurrentStageApprover(request);
        }

        /// <summary>
        /// rel1009e: an approver resolved for the current stage (e.g. a parent unit's head via the nearest-ancestor
        /// fallback) can open the requisition even though it belongs to another department. Same resolver as the
        /// notification and the approve/reject check.
        /// </summary>
        private bool IsCurrentStageApprover(PurchaseRequest request)
        {
            var userId = _currentUser == null ? null : _currentUser.UserId;
            if (request == null || request.ApprovalStatus != ApprovalStatus.Pending || string.IsNullOrWhiteSpace(userId) || _userService == null)
            {
                return false;
            }

            var stageNumber = request.CurrentApprovalStage <= 0 ? 1 : request.CurrentApprovalStage;
            var stageRoleId = ApprovalWorkflowSettingsHelper.TryGetCurrentStageRoleId(request.ApprovalStageRoleIds, stageNumber);
            var stageUserId = ApprovalWorkflowSettingsHelper.TryGetCurrentStageUserId(request.ApprovalStageUserIds, stageNumber);
            if (!stageRoleId.HasValue && string.IsNullOrWhiteSpace(stageUserId))
            {
                return false;
            }

            var user = _userService.GetById(userId);
            if (user == null || !user.IsActive)
            {
                return false;
            }

            return ApprovalWorkflowHelper.CanUserActOnStage(
                null,
                request.RequestedById,
                userId,
                false,
                user.RoleId,
                stageRoleId,
                stageUserId,
                StageApproverScopeResolver.ResolveFor(_unitOfWork, _userService, stageRoleId, stageUserId, request.DepartmentId, Helpers.CatalogRequisitionFlowResolver.IsCatalogSource(request.ApprovalFlowSource)));
        }

        private void EnsureCanAccessPurchaseRequest(PurchaseRequest request)
        {
            if (!CanAccessPurchaseRequest(request))
            {
                throw new BusinessException("You do not have access to this department's purchase requests.");
            }
        }

        private bool IsDepartmentScopeDenied()
        {
            return !_departmentScope.BypassesDepartmentScope
                && !CanApprovePurchases()
                && !_departmentScope.ScopedDepartmentId.HasValue;
        }

        private static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private int? ResolveTargetAssetId(int? targetAssetId)
        {
            if (!targetAssetId.HasValue || targetAssetId.Value <= 0)
            {
                return null;
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(targetAssetId.Value);
            if (asset == null || !asset.IsActive)
            {
                throw new BusinessException("Selected asset was not found or is inactive.");
            }

            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (organizationId.HasValue
                && asset.OrganizationId.HasValue
                && asset.OrganizationId.Value != organizationId.Value)
            {
                throw new BusinessException("Selected asset does not belong to this organization.");
            }

            return asset.Id;
        }

        private static void ApplyHeaderSummaryFromLines(PurchaseRequestCreateVm model, IList<PurchaseRequestLineCreateVm> lines)
        {
            if (model == null || lines == null || lines.Count == 0)
            {
                return;
            }

            model.Quantity = lines.Sum(x => x.Quantity);
            if (lines.Count == 1)
            {
                model.ItemDescription = lines[0].Description;
                return;
            }

            var summary = string.Join("; ", lines.Select(x => x.Description));
            if (summary.Length > 2000)
            {
                summary = summary.Substring(0, 1997) + "...";
            }

            model.ItemDescription = summary;
        }

        private void PersistLineItems(PurchaseRequest entity, IList<ClassifiedLine> lines)
        {
            if (entity == null || lines == null || lines.Count == 0)
            {
                return;
            }

            var organizationId = entity.OrganizationId ?? _organizationScope.GetCurrentOrganizationId();
            var lineNumber = 1;
            foreach (var line in lines)
            {
                _unitOfWork.Repository<PurchaseRequestLine>().Add(new PurchaseRequestLine
                {
                    OrganizationId = organizationId,
                    PurchaseRequestId = entity.Id,
                    LineNumber = lineNumber,
                    ItemName = AssetNameRules.Normalize(line.Line.ItemName),
                    Description = line.Line.Description,
                    Quantity = line.Line.Quantity,
                    AssetCategoryId = line.Classification == null ? null : line.Classification.AssetCategoryId,
                    AssetTypeId = line.Classification == null ? null : line.Classification.AssetTypeId,
                    AssetSubTypeId = line.Classification == null ? null : line.Classification.AssetSubTypeId,
                    TargetAssetId = line.Line.TargetAssetId,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                });
                lineNumber++;
            }
        }
    }
}

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
    public class AssetService : IAssetService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;
        private readonly IDepartmentScopeService _departmentScope;
        private readonly IAssetScanLookupRepository _assetScanLookupRepository;
        private readonly IUserService _userService;
        private readonly ICurrentUserContext _currentUser;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IAssetQueryService _assetQueryService;
        private readonly IAssetWorkflowGuard _workflowGuard;
        private readonly IApprovalWorkflowEngine _approvalEngine;
        private readonly IOutboxWriter _outboxWriter;
        private readonly IWebhookService _webhookService;
        private readonly IReferenceDataCache _referenceDataCache;

        public AssetService(
            IUnitOfWork unitOfWork,
            IAuditWriter auditWriter,
            IDepartmentScopeService departmentScope,
            IAssetScanLookupRepository assetScanLookupRepository,
            IUserService userService,
            ICurrentUserContext currentUser,
            IOrganizationScopeService organizationScope,
            IAssetQueryService assetQueryService,
            IAssetWorkflowGuard workflowGuard,
            IApprovalWorkflowEngine approvalEngine,
            IOutboxWriter outboxWriter,
            IWebhookService webhookService,
            IReferenceDataCache referenceDataCache)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
            _departmentScope = departmentScope;
            _assetScanLookupRepository = assetScanLookupRepository;
            _userService = userService;
            _currentUser = currentUser;
            _organizationScope = organizationScope;
            _assetQueryService = assetQueryService;
            _workflowGuard = workflowGuard;
            _approvalEngine = approvalEngine;
            _outboxWriter = outboxWriter;
            _webhookService = webhookService;
            _referenceDataCache = referenceDataCache;
        }

        public IEnumerable<AssetListVm> GetAssets(AssetFilterVm filter)
        {
            var items = new List<AssetListVm>();
            _assetQueryService.StreamExport(filter, "tag", "asc", row =>
            {
                items.Add(new AssetListVm
                {
                    AssetTag = row.AssetTag,
                    AssetName = row.AssetName,
                    CategoryName = row.CategoryName,
                    DepartmentName = row.DepartmentName,
                    CurrentCustodianId = row.CurrentCustodianId,
                    CurrentStatus = row.CurrentStatus,
                    AcquisitionCost = row.AcquisitionCost
                });
            });
            return items;
        }

        public AssetListPageVm GetAssetListPage(AssetFilterVm filter, string sort, string direction, int page, int pageSize)
        {
            return _assetQueryService.GetListPage(filter, sort, direction, page, pageSize);
        }

        public int CountAssets(AssetFilterVm filter)
        {
            return _assetQueryService.Count(filter);
        }

        public AssetScanLookupVm LookupByScanCode(string code, bool applyDepartmentScope = true, bool includeDetails = true)
        {
            var lookupKey = ScanCodeHelper.ToLookupKey(code);
            if (string.IsNullOrWhiteSpace(lookupKey))
            {
                return new AssetScanLookupVm { Found = false, Message = "Scan code is required." };
            }

            var organizationId = _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return new AssetScanLookupVm { Found = false, Message = "Organization context is required for scan lookup." };
            }

            if (!includeDetails)
            {
                var exists = _assetScanLookupRepository.ExistsByScanCode(lookupKey, organizationId.Value);
                return new AssetScanLookupVm
                {
                    Found = exists,
                    Message = exists ? "Asset found." : "No active asset matched that code."
                };
            }

            var departmentId = applyDepartmentScope ? _departmentScope.ScopedDepartmentId : null;
            var match = _assetScanLookupRepository.FindByScanCode(lookupKey, organizationId.Value, departmentId);
            if (match == null)
            {
                return new AssetScanLookupVm { Found = false, Message = "No active asset matched that code." };
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(match.Id);
            if (asset != null && applyDepartmentScope)
            {
                _departmentScope.EnsureCanAccessAsset(asset);
            }

            return new AssetScanLookupVm
            {
                Found = true,
                AssetId = match.Id,
                AssetTag = match.AssetTag,
                AssetName = match.AssetName,
                DepartmentName = match.DepartmentName,
                CurrentStatus = match.CurrentStatus,
                BarcodeOrQRCode = match.BarcodeOrQRCode,
                SerialNumber = match.SerialNumber,
                Brand = match.Brand,
                Model = match.Model,
                CategoryName = match.CategoryName,
                CustodianName = match.CustodianName
            };
        }

        public AssetTcoVm GetTotalCostOfOwnership(int assetId)
        {
            return GetTotalCostOfOwnership(assetId, true);
        }

        private AssetTcoVm GetTotalCostOfOwnership(int assetId, bool enforceDepartmentScope)
        {
            var asset = _unitOfWork.Repository<Asset>().GetById(assetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            if (enforceDepartmentScope)
            {
                _departmentScope.EnsureCanAccessAsset(asset);
            }

            var maintenanceTotal = _unitOfWork.Repository<AssetMaintenanceRecord>().Find(x => x.AssetId == assetId)
                .Sum(x => x.Cost);
            var insuranceExposure = _unitOfWork.Repository<InsurancePolicy>().Find(x => x.AssetId == assetId)
                .Sum(x => x.InsuredValue);
            var total = asset.AcquisitionCost + asset.TaxAmount + maintenanceTotal;

            return new AssetTcoVm
            {
                AssetId = assetId,
                AcquisitionCost = asset.AcquisitionCost,
                TaxAmount = asset.TaxAmount,
                MaintenanceTotal = maintenanceTotal,
                InsuranceExposure = insuranceExposure,
                TotalCostOfOwnership = total
            };
        }

        public AssetDetailsVm GetById(int id)
        {
            var asset = _unitOfWork.Repository<Asset>().GetById(id);
            if (asset == null)
            {
                return null;
            }

            var enforceDepartmentScope = true;
            try
            {
                _departmentScope.EnsureCanAccessAsset(asset);
            }
            catch (BusinessException)
            {
                if (!CanViewAssetForPendingTransferApproval(asset))
                {
                    return null;
                }

                enforceDepartmentScope = false;
            }

            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return null;
            }

            var departmentLookup = _referenceDataCache.GetDepartments(organizationId.Value, false)
                .ToDictionary(x => x.Id, x => x.Name);
            var categoryLookup = _referenceDataCache.GetCategories(organizationId.Value, false)
                .ToDictionary(x => x.Id, x => x.Name);
            var supplierLookup = _referenceDataCache.GetSuppliers(organizationId.Value, false)
                .ToDictionary(x => x.Id, x => x.SupplierName);
            var roleLookup = _referenceDataCache.GetRoles(organizationId.Value)
                .ToDictionary(x => x.Id, x => x.Name);

            var timeline = _unitOfWork.Repository<AssetCustodyEvent>().Find(x => x.AssetId == id)
                .OrderByDescending(x => x.ActionDate).ToList()
                .Select(x => new AssetCustodyTimelineVm
                {
                    ActionDate = x.ActionDate,
                    ActionType = x.ActionType.ToString(),
                    FromEntity = BuildTransferEntityLabel(x.FromUserId, x.FromDepartmentId, departmentLookup),
                    ToEntity = BuildTransferEntityLabel(x.ToUserId, x.ToDepartmentId, departmentLookup),
                    ConditionBefore = x.ConditionBefore,
                    ConditionAfter = x.ConditionAfter,
                    Reason = x.Reason,
                    ApprovedById = x.ApprovedById,
                    Notes = x.Notes
                }).ToList();

            var pendingTransfers = _unitOfWork.Repository<AssetTransfer>()
                .Find(x => x.AssetId == id && x.ApprovalStatus == ApprovalStatus.Pending && x.IsActive)
                .OrderByDescending(x => x.TransferDate).ToList();
            var transferIds = pendingTransfers.Select(x => x.Id).ToList();
            var transferActions = transferIds.Count == 0
                ? new Dictionary<int, List<TransferApprovalAction>>()
                : _unitOfWork.Repository<TransferApprovalAction>().GetAll()
                    .Where(x => transferIds.Contains(x.AssetTransferId))
                    .ToList()
                    .GroupBy(x => x.AssetTransferId)
                    .ToDictionary(x => x.Key, x => x.OrderBy(y => y.StageNumber).ThenBy(y => y.DecisionDate).ToList());

            var pendingDisposal = _unitOfWork.Repository<DisposalRecord>()
                .Find(x => x.AssetId == id && x.ApprovalStatus == ApprovalStatus.Pending && x.IsActive)
                .OrderByDescending(x => x.DisposalRequestDate).FirstOrDefault();
            var disposalActions = pendingDisposal == null
                ? new List<DisposalApprovalAction>()
                : _unitOfWork.Repository<DisposalApprovalAction>().Find(x => x.DisposalRecordId == pendingDisposal.Id)
                    .OrderBy(x => x.StageNumber).ThenBy(x => x.DecisionDate).ToList();

            return new AssetDetailsVm
            {
                Id = asset.Id,
                AssetTag = asset.AssetTag,
                AssetName = asset.AssetName,
                StatusGuidance = BuildStatusGuidance(asset.CurrentStatus),
                SerialNumber = asset.SerialNumber,
                Brand = asset.Brand,
                Model = asset.Model,
                DepartmentName = asset.DepartmentId.HasValue && departmentLookup.ContainsKey(asset.DepartmentId.Value)
                    ? departmentLookup[asset.DepartmentId.Value]
                    : null,
                CategoryName = categoryLookup.ContainsKey(asset.CategoryId) ? categoryLookup[asset.CategoryId] : null,
                SupplierName = asset.SupplierId.HasValue && supplierLookup.ContainsKey(asset.SupplierId.Value)
                    ? supplierLookup[asset.SupplierId.Value]
                    : null,
                CurrentCustodianId = asset.CurrentCustodianId,
                CurrentStatus = asset.CurrentStatus,
                AcquisitionCost = asset.AcquisitionCost,
                TaxAmount = asset.TaxAmount,
                UsefulLifeMonths = asset.UsefulLifeMonths,
                PurchaseDate = asset.PurchaseDate,
                PolicyReference = asset.PolicyReference,
                WarrantyEndDate = asset.WarrantyEndDate,
                CustodyHistory = timeline,
                PendingTransfers = pendingTransfers.Select(x => BuildPendingTransferVm(
                    x, roleLookup, departmentLookup, transferActions.ContainsKey(x.Id) ? transferActions[x.Id] : new List<TransferApprovalAction>())).ToList(),
                PendingDisposal = pendingDisposal == null ? null : BuildPendingDisposalVm(pendingDisposal, roleLookup, disposalActions),
                MaintenanceRecords = BuildMaintenanceList(id),
                Incidents = BuildIncidentList(id),
                InsuranceClaims = BuildClaimList(id),
                InsurancePolicies = _unitOfWork.Repository<InsurancePolicy>().Find(x => x.AssetId == id)
                    .OrderByDescending(x => x.PolicyEndDate)
                    .Select(x => new InsurancePolicyListVm
                    {
                        Id = x.Id,
                        AssetId = x.AssetId,
                        InsurerName = x.InsurerName,
                        PolicyNumber = x.PolicyNumber,
                        PolicyStartDate = x.PolicyStartDate,
                        PolicyEndDate = x.PolicyEndDate,
                        InsuredValue = x.InsuredValue,
                        ClaimEligibility = x.ClaimEligibility
                    })
                    .ToList(),
                TotalCostOfOwnership = GetTotalCostOfOwnership(id, enforceDepartmentScope)
            };
        }

        private List<MaintenanceRecordListVm> BuildMaintenanceList(int assetId)
        {
            return _unitOfWork.Repository<AssetMaintenanceRecord>()
                .Find(x => x.AssetId == assetId)
                .OrderByDescending(x => x.ServiceDate)
                .Select(x => new MaintenanceRecordListVm
                {
                    Id = x.Id,
                    AssetId = x.AssetId,
                    MaintenanceTicketNumber = x.MaintenanceTicketNumber,
                    MaintenanceType = x.MaintenanceType.ToString(),
                    ReportedIssue = x.ReportedIssue,
                    Status = x.Status,
                    ServiceDate = x.ServiceDate,
                    CompletionDate = x.CompletionDate,
                    Outcome = x.Outcome
                })
                .ToList();
        }

        private List<IncidentListVm> BuildIncidentList(int assetId)
        {
            return _unitOfWork.Repository<AssetIncident>()
                .Find(x => x.AssetId == assetId)
                .OrderByDescending(x => x.IncidentDate)
                .Select(x => new IncidentListVm
                {
                    Id = x.Id,
                    IncidentNumber = x.IncidentNumber,
                    AssetId = x.AssetId,
                    IncidentType = x.IncidentType.ToString(),
                    Severity = x.Severity,
                    IncidentDate = x.IncidentDate,
                    ResolutionStatus = x.ResolutionStatus
                })
                .ToList();
        }

        private List<ClaimListVm> BuildClaimList(int assetId)
        {
            return _unitOfWork.Repository<InsuranceClaim>()
                .Find(x => x.AssetId == assetId)
                .OrderByDescending(x => x.ClaimDate)
                .Select(x => new ClaimListVm
                {
                    Id = x.Id,
                    ClaimNumber = x.ClaimNumber,
                    AssetId = x.AssetId,
                    ClaimType = x.ClaimType,
                    Insurer = x.Insurer,
                    ClaimStatus = x.ClaimStatus,
                    ClaimDate = x.ClaimDate
                })
                .ToList();
        }

        public int Create(AssetCreateVm model)
        {
            model.AssetTag = NormalizeAssetTag(ResolveAssetTagForCreate(model));
            ValidateUniqueness(model.AssetTag, model.SerialNumber, null);
            var entity = new Asset
            {
                AssetName = model.AssetName,
                AssetTag = model.AssetTag,
                CategoryId = model.CategoryId,
                AssetTypeId = model.AssetTypeId,
                Brand = model.Brand,
                Model = model.Model,
                SerialNumber = model.SerialNumber,
                Description = model.Description,
                PurchaseDate = model.PurchaseDate,
                AcquisitionCost = model.AcquisitionCost,
                TaxAmount = model.TaxAmount,
                Currency = string.IsNullOrWhiteSpace(model.Currency)
                    ? ApprovalWorkflowSettingsHelper.GetDefaultCurrencyCode(_unitOfWork.Repository<SystemSetting>().GetAll())
                    : model.Currency.Trim().ToUpperInvariant(),
                SupplierId = NormalizeOptionalId(model.SupplierId),
                DepartmentId = NormalizeOptionalId(model.DepartmentId),
                CurrentCustodianId = null,
                ConditionOnReceipt = model.ConditionOnReceipt,
                UsefulLifeMonths = UsefulLifeResolver.Resolve(_unitOfWork, model.AssetTypeId, model.CategoryId),
                SalvageValue = 0,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = model.PurchaseDate,
                CurrentBookValue = model.AcquisitionCost,
                AccumulatedDepreciation = 0,
                IsInsured = model.IsInsured,
                InsuredValue = model.InsuredValue,
                WarrantyStartDate = model.WarrantyStartDate,
                WarrantyEndDate = model.WarrantyEndDate,
                CurrentStatus = model.CurrentStatus == 0 ? AssetStatus.InStore : model.CurrentStatus,
                Condition = AssetCondition.New,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            AssetApprovalSettingsHelper.ApplyToAsset(entity, model.ApprovalProcesses);

            _unitOfWork.Repository<Asset>().Add(entity);
            _unitOfWork.SaveChanges();
            _auditWriter.Write("Assets.Create", nameof(Asset), entity.Id.ToString(), null, entity.AssetTag);
            return entity.Id;
        }

        public void UpdateStatus(int id, AssetStatus status)
        {
            var entity = _unitOfWork.Repository<Asset>().GetById(id);
            if (entity == null)
            {
                throw new BusinessException("Asset not found.");
            }

            _departmentScope.EnsureCanAccessAsset(entity);

            if (entity.CurrentStatus == AssetStatus.Disposed)
            {
                throw new BusinessException("Disposed assets cannot be modified.");
            }

            _workflowGuard.EnsureNoBlockingWorkflow(id);

            var oldStatus = entity.CurrentStatus;
            entity.CurrentStatus = status;
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Asset>().Update(entity);
            _unitOfWork.SaveChanges();
            _auditWriter.Write("Assets.UpdateStatus", nameof(Asset), entity.Id.ToString(), oldStatus.ToString(), status.ToString());
            _webhookService.QueueDelivery(
                "asset.status_changed",
                "{\"assetId\":" + entity.Id + ",\"status\":\"" + status + "\"}");
        }

        public void Update(AssetEditVm model)
        {
            model.AssetTag = NormalizeAssetTag(model.AssetTag);
            ValidateUniqueness(model.AssetTag, model.SerialNumber, model.Id);
            var entity = _unitOfWork.Repository<Asset>().GetById(model.Id);
            if (entity == null)
            {
                throw new BusinessException("Asset not found.");
            }

            _departmentScope.EnsureCanAccessAsset(entity);

            if (entity.CurrentStatus == AssetStatus.Disposed)
            {
                throw new BusinessException("Disposed assets cannot be modified.");
            }

            var oldSnapshot = entity.AssetName + "|" + entity.AssetTag + "|" + entity.CurrentStatus;
            entity.AssetName = model.AssetName;
            entity.AssetTag = model.AssetTag;
            entity.CategoryId = model.CategoryId;
            entity.AssetTypeId = model.AssetTypeId;
            entity.Brand = model.Brand;
            entity.Model = model.Model;
            entity.SerialNumber = model.SerialNumber;
            entity.Description = model.Description;
            entity.PurchaseDate = model.PurchaseDate;
            entity.AcquisitionCost = model.AcquisitionCost;
            entity.TaxAmount = model.TaxAmount;
            entity.Currency = string.IsNullOrWhiteSpace(model.Currency)
                ? ApprovalWorkflowSettingsHelper.GetDefaultCurrencyCode(_unitOfWork.Repository<SystemSetting>().GetAll())
                : model.Currency.Trim().ToUpperInvariant();
            entity.SupplierId = NormalizeOptionalId(model.SupplierId);
            entity.DepartmentId = NormalizeOptionalId(model.DepartmentId);
            entity.ConditionOnReceipt = model.ConditionOnReceipt;
            entity.UsefulLifeMonths = UsefulLifeResolver.Resolve(_unitOfWork, model.AssetTypeId, model.CategoryId);
            entity.IsInsured = model.IsInsured;
            entity.InsuredValue = model.InsuredValue;
            entity.WarrantyStartDate = model.WarrantyStartDate;
            entity.WarrantyEndDate = model.WarrantyEndDate;
            if (entity.CurrentStatus != model.CurrentStatus && model.CurrentStatus != 0)
            {
                _workflowGuard.EnsureNoBlockingWorkflow(entity.Id);
            }

            if (model.CurrentStatus != 0)
            {
                entity.CurrentStatus = model.CurrentStatus;
            }
            entity.UpdatedAt = DateTime.UtcNow;

            AssetApprovalSettingsHelper.ApplyToAsset(entity, model.ApprovalProcesses);

            _unitOfWork.Repository<Asset>().Update(entity);
            _unitOfWork.SaveChanges();
            _auditWriter.Write("Assets.Edit", nameof(Asset), entity.Id.ToString(), oldSnapshot, entity.AssetName + "|" + entity.AssetTag + "|" + entity.CurrentStatus);
        }

        public void Delete(int id)
        {
            var entity = _unitOfWork.Repository<Asset>().GetById(id);
            if (entity == null)
            {
                return;
            }

            entity.IsActive = false;
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Asset>().Update(entity);
            _unitOfWork.SaveChanges();
            _auditWriter.Write("Assets.Delete", nameof(Asset), entity.Id.ToString(), entity.AssetTag, "SoftDeleted");
        }

        public void RequestDisposal(AssetDisposalRequestVm model, string requestedByUserId)
        {
            EnsureDisposalRequestInput(model, requestedByUserId);
            var asset = _unitOfWork.Repository<Asset>().GetById(model.AssetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            _workflowGuard.EnsureNoBlockingWorkflow(model.AssetId);
            EnsureCanRequestDisposal(asset, model.AssetId);
            var approvalConfig = ApprovalWorkflowHelper.GetAssetProcessConfiguration(_unitOfWork, asset, ApprovalProcessCodes.Disposal);

            if (!approvalConfig.UsesApproval)
            {
                CompleteDisposalWithoutApproval(asset, model, requestedByUserId);
                return;
            }

            var disposal = new DisposalRecord
            {
                AssetId = model.AssetId,
                DisposalRequestDate = DateTime.UtcNow,
                DisposalReason = NormalizeText(model.DisposalReason),
                DisposalMethod = model.DisposalMethod,
                ApprovalStatus = ApprovalStatus.Pending,
                Notes = NormalizeText(model.Notes),
                RequestedById = requestedByUserId,
                CurrentApprovalStage = 1,
                ApprovalStageRoleIds = ApprovalWorkflowSettingsHelper.SerializeStageRoleIds(approvalConfig.StageRoleIds.Select(x => (int?)x)),
                ApprovalStageUserIds = ApprovalWorkflowSettingsHelper.SerializeStageUserIds(approvalConfig.StageUserIds),
                OriginalAssetStatus = asset.CurrentStatus,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var oldStatus = asset.CurrentStatus;
            asset.CurrentStatus = AssetStatus.AwaitingApproval;
            asset.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Repository<DisposalRecord>().Add(disposal);
            _unitOfWork.Repository<Asset>().Update(asset);
            _unitOfWork.SaveChanges();
            NotificationHelper.AddNotification(
                _unitOfWork,
                _outboxWriter,
                _organizationScope,
                requestedByUserId,
                NotificationType.PendingApproval,
                "Disposal submitted",
                "Disposal request #" + disposal.Id + " for asset " + (asset.AssetTag ?? ("#" + asset.Id)) + " is pending approval.",
                "/Assets/Details/" + asset.Id);
            NotificationHelper.AddStageApproverNotification(
                _unitOfWork,
                _outboxWriter,
                _organizationScope,
                _userService,
                approvalConfig.StageRoleIds.FirstOrDefault(),
                ApprovalWorkflowHelper.GetStageUserId(approvalConfig.StageUserIds, 1),
                "Disposal approval required",
                "Disposal request #" + disposal.Id + " for asset " + (asset.AssetTag ?? ("#" + asset.Id)) + " is awaiting Stage 1 approval.",
                "/Assets/Details/" + asset.Id);
            _unitOfWork.SaveChanges();
            _auditWriter.Write("Assets.RequestDisposal", nameof(Asset), asset.Id.ToString(), oldStatus.ToString(), AssetStatus.AwaitingApproval + "|" + model.DisposalMethod);
        }

        public void ApproveDisposal(AssetDisposalApprovalVm model, string approvedByUserId, int? approverRoleId, bool isSuperAdmin)
        {
            EnsureDisposalApprovalInput(model, approvedByUserId);
            var asset = _unitOfWork.Repository<Asset>().GetById(model.AssetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            if (asset.CurrentStatus != AssetStatus.AwaitingApproval)
            {
                throw new BusinessException("Asset is not in awaiting approval state for disposal.");
            }

            var pendingRequest = GetPendingDisposalRequest(model.AssetId);
            var stageRoleIds = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(pendingRequest.ApprovalStageRoleIds);
            var stageUserIds = ApprovalWorkflowSettingsHelper.ParseStageUserIds(pendingRequest.ApprovalStageUserIds);
            var stageNumber = pendingRequest.CurrentApprovalStage <= 0 ? 1 : pendingRequest.CurrentApprovalStage;
            var expectedRoleId = ApprovalWorkflowHelper.GetStageRoleId(stageRoleIds, stageNumber, ApprovalProcessCodes.Disposal);
            var expectedUserId = ApprovalWorkflowHelper.GetStageUserId(stageUserIds, stageNumber);
            var isFinalStage = stageNumber >= stageRoleIds.Count;

            _approvalEngine.ExecuteStageDecision(new ApprovalStageDecisionRequest
            {
                ProcessCode = ApprovalProcessCodes.Disposal,
                ActingUserId = approvedByUserId,
                ApproverRoleId = approverRoleId,
                IsSuperAdmin = isSuperAdmin,
                RequesterUserId = pendingRequest.RequestedById,
                Notes = NormalizeText(model.Notes),
                Decision = ApprovalStatus.Approved,
                RequestEntity = pendingRequest,
                RequestEntityType = typeof(DisposalRecord),
                StageNumber = stageNumber,
                StageRoleIds = stageRoleIds,
                ExpectedRoleId = expectedRoleId,
                ExpectedUserId = expectedUserId,
                ApprovalActionEntity = new DisposalApprovalAction
                {
                    DisposalRecordId = pendingRequest.Id,
                    StageNumber = stageNumber,
                    RoleId = expectedRoleId,
                    ApproverUserId = approvedByUserId,
                    Decision = ApprovalStatus.Approved,
                    Notes = NormalizeText(model.Notes),
                    DecisionDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                },
                OnApprovedFinal = outcome => FinalizeApprovedDisposalInTransaction(asset, pendingRequest, approvedByUserId, model),
                WriteNotifications = uow =>
                {
                    if (isFinalStage)
                    {
                        NotificationHelper.AddNotification(
                            uow,
                            _outboxWriter,
                            _organizationScope,
                            pendingRequest.RequestedById,
                            NotificationType.General,
                            "Disposal approved",
                            "Disposal request #" + pendingRequest.Id + " for asset " + (asset.AssetTag ?? ("#" + asset.Id)) + " has been fully approved.",
                            "/Assets/Details/" + asset.Id);
                    }
                    else
                    {
                        NotificationHelper.AddNotification(
                            uow,
                            _outboxWriter,
                            _organizationScope,
                            pendingRequest.RequestedById,
                            NotificationType.PendingApproval,
                            "Disposal stage approved",
                            "Disposal request #" + pendingRequest.Id + " moved to Stage " + pendingRequest.CurrentApprovalStage + " approval.",
                            "/Assets/Details/" + asset.Id);
                        NotificationHelper.AddStageApproverNotification(
                            uow,
                            _outboxWriter,
                            _organizationScope,
                            _userService,
                            ApprovalWorkflowHelper.GetStageRoleId(stageRoleIds, pendingRequest.CurrentApprovalStage, ApprovalProcessCodes.Disposal),
                            ApprovalWorkflowHelper.GetStageUserId(stageUserIds, pendingRequest.CurrentApprovalStage),
                            "Disposal approval stage advanced",
                            "Disposal request #" + pendingRequest.Id + " is now awaiting Stage " + pendingRequest.CurrentApprovalStage + " approval.",
                            "/Assets/Details/" + asset.Id);
                    }
                }
            });

            _auditWriter.Write(
                "Assets.ApproveDisposal",
                isFinalStage ? nameof(Asset) : nameof(DisposalRecord),
                isFinalStage ? asset.Id.ToString() : pendingRequest.Id.ToString(),
                isFinalStage ? asset.CurrentStatus.ToString() : "Stage" + stageNumber,
                isFinalStage ? "Disposed" : "Stage" + pendingRequest.CurrentApprovalStage);

            if (isFinalStage)
            {
                _webhookService.QueueDelivery(
                    "disposal.finalized",
                    "{\"assetId\":" + asset.Id + ",\"disposalRecordId\":" + pendingRequest.Id + "}");
            }
        }

        public void RejectDisposal(AssetDisposalApprovalVm model, string rejectedByUserId, int? approverRoleId, bool isSuperAdmin)
        {
            EnsureDisposalApprovalInput(model, rejectedByUserId);
            var asset = _unitOfWork.Repository<Asset>().GetById(model.AssetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            var pendingRequest = GetPendingDisposalRequest(model.AssetId);
            var stageRoleIds = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(pendingRequest.ApprovalStageRoleIds);
            var stageUserIds = ApprovalWorkflowSettingsHelper.ParseStageUserIds(pendingRequest.ApprovalStageUserIds);
            var stageNumber = pendingRequest.CurrentApprovalStage <= 0 ? 1 : pendingRequest.CurrentApprovalStage;
            var expectedRoleId = ApprovalWorkflowHelper.GetStageRoleId(stageRoleIds, stageNumber, ApprovalProcessCodes.Disposal);
            var expectedUserId = ApprovalWorkflowHelper.GetStageUserId(stageUserIds, stageNumber);

            _approvalEngine.ExecuteStageDecision(new ApprovalStageDecisionRequest
            {
                ProcessCode = ApprovalProcessCodes.Disposal,
                ActingUserId = rejectedByUserId,
                ApproverRoleId = approverRoleId,
                IsSuperAdmin = isSuperAdmin,
                RequesterUserId = pendingRequest.RequestedById,
                Notes = NormalizeText(model.Notes),
                Decision = ApprovalStatus.Rejected,
                RequestEntity = pendingRequest,
                RequestEntityType = typeof(DisposalRecord),
                StageNumber = stageNumber,
                StageRoleIds = stageRoleIds,
                ExpectedRoleId = expectedRoleId,
                ExpectedUserId = expectedUserId,
                ApprovalActionEntity = new DisposalApprovalAction
                {
                    DisposalRecordId = pendingRequest.Id,
                    StageNumber = stageNumber,
                    RoleId = expectedRoleId,
                    ApproverUserId = rejectedByUserId,
                    Decision = ApprovalStatus.Rejected,
                    Notes = NormalizeText(model.Notes),
                    DecisionDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                },
                OnRejected = outcome =>
                {
                    ApplyApprovalNotes(pendingRequest, model.Notes);
                    asset.CurrentStatus = pendingRequest.OriginalAssetStatus;
                    asset.UpdatedAt = DateTime.UtcNow;
                    _unitOfWork.Repository<DisposalRecord>().Update(pendingRequest);
                    _unitOfWork.Repository<Asset>().Update(asset);
                },
                WriteNotifications = uow =>
                {
                    NotificationHelper.AddNotification(
                        uow,
                        _outboxWriter,
                        _organizationScope,
                        pendingRequest.RequestedById,
                        NotificationType.General,
                        "Disposal rejected",
                        "Disposal request #" + pendingRequest.Id + " was rejected at Stage " + stageNumber + ".",
                        "/Assets/Details/" + asset.Id);
                }
            });

            _auditWriter.Write("Assets.ApproveDisposal", nameof(DisposalRecord), pendingRequest.Id.ToString(), "PendingApproval", "Rejected");
        }

        private void CompleteDisposalWithoutApproval(Asset asset, AssetDisposalRequestVm model, string requestedByUserId)
        {
            var disposal = new DisposalRecord
            {
                AssetId = model.AssetId,
                DisposalRequestDate = DateTime.UtcNow,
                DisposalReason = NormalizeText(model.DisposalReason),
                DisposalMethod = model.DisposalMethod,
                ApprovalStatus = ApprovalStatus.Approved,
                Notes = NormalizeText(model.Notes),
                RequestedById = requestedByUserId,
                DisposalApprovedById = requestedByUserId,
                DisposalDate = DateTime.UtcNow,
                OriginalAssetStatus = asset.CurrentStatus,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var oldStatus = asset.CurrentStatus;
            var fromDepartmentId = asset.DepartmentId;
            asset.CurrentStatus = AssetStatus.Disposed;
            asset.CurrentCustodianId = null;
            asset.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Repository<DisposalRecord>().Add(disposal);
            _unitOfWork.Repository<Asset>().Update(asset);
            AddDisposalCustodyEvent(asset, disposal, requestedByUserId, null, fromDepartmentId);
            _unitOfWork.SaveChanges();
            NotificationHelper.AddNotification(
                _unitOfWork,
                _outboxWriter,
                _organizationScope,
                requestedByUserId,
                NotificationType.General,
                "Disposal completed",
                "Disposal request #" + disposal.Id + " for asset " + (asset.AssetTag ?? ("#" + asset.Id)) + " was completed immediately.",
                "/Assets/Details/" + asset.Id);
            _unitOfWork.SaveChanges();
            _auditWriter.Write("Assets.RequestDisposal", nameof(Asset), asset.Id.ToString(), oldStatus.ToString(), "Disposed");
            _webhookService.QueueDelivery(
                "disposal.finalized",
                "{\"assetId\":" + asset.Id + ",\"disposalRecordId\":" + disposal.Id + "}");
        }

        private void FinalizeApprovedDisposalInTransaction(Asset asset, DisposalRecord pendingRequest, string approvedByUserId, AssetDisposalApprovalVm model)
        {
            pendingRequest.DisposalApprovedById = approvedByUserId;
            pendingRequest.DisposalDate = model.DisposalDate ?? DateTime.UtcNow;
            pendingRequest.DisposalAmount = model.DisposalAmount;
            ApplyApprovalNotes(pendingRequest, model.Notes);
            pendingRequest.UpdatedAt = DateTime.UtcNow;

            var fromUserId = asset.CurrentCustodianId;
            var fromDepartmentId = asset.DepartmentId;
            asset.CurrentStatus = AssetStatus.Disposed;
            asset.CurrentCustodianId = null;
            asset.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Repository<DisposalRecord>().Update(pendingRequest);
            _unitOfWork.Repository<Asset>().Update(asset);
            AddDisposalCustodyEvent(asset, pendingRequest, approvedByUserId, fromUserId, fromDepartmentId);
        }

        private PendingTransferApprovalVm BuildPendingTransferVm(AssetTransfer transfer, IDictionary<int, string> roleLookup, IDictionary<int, string> departmentLookup, IEnumerable<TransferApprovalAction> actions)
        {
            var stageRoleIds = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(transfer.ApprovalStageRoleIds);
            var stageUserIds = ApprovalWorkflowSettingsHelper.ParseStageUserIds(transfer.ApprovalStageUserIds);
            var stageRoleId = transfer.CurrentApprovalStage > 0 && transfer.CurrentApprovalStage <= stageRoleIds.Count ? (int?)stageRoleIds[transfer.CurrentApprovalStage - 1] : null;
            var stageUserId = ApprovalWorkflowSettingsHelper.TryGetCurrentStageUserId(transfer.ApprovalStageUserIds, transfer.CurrentApprovalStage);

            return new PendingTransferApprovalVm
            {
                Id = transfer.Id,
                RequestedByName = transfer.RequestedById,
                RequestedDateText = transfer.TransferDate.ToString("yyyy-MM-dd HH:mm"),
                FromEntity = BuildTransferEntityLabel(transfer.FromUserId, transfer.FromDepartmentId, departmentLookup),
                ToEntity = BuildTransferEntityLabel(transfer.ToUserId, transfer.ToDepartmentId, departmentLookup),
                Reason = transfer.Reason,
                CurrentApprovalStage = transfer.CurrentApprovalStage,
                CurrentStageRoleId = stageRoleId,
                CurrentStageRoleName = ApprovalWorkflowSettingsHelper.ResolveRoleName(roleLookup, stageRoleId),
                CurrentStageUserId = stageUserId,
                CurrentStageUserName = ResolveApproverDisplayName(stageUserId),
                ApprovalHistory = ApprovalWorkflowHelper.MapDecisionHistory(
                    actions.Select(x => ApprovalWorkflowHelper.ToSnapshot(
                        x.StageNumber, x.RoleId, x.ApproverUserId, x.Decision, x.Notes, x.DecisionDate)),
                    roleLookup)
            };
        }

        private PendingDisposalApprovalVm BuildPendingDisposalVm(DisposalRecord disposal, IDictionary<int, string> roleLookup, IEnumerable<DisposalApprovalAction> actions)
        {
            var stageRoleIds = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(disposal.ApprovalStageRoleIds);
            var stageRoleId = disposal.CurrentApprovalStage > 0 && disposal.CurrentApprovalStage <= stageRoleIds.Count ? (int?)stageRoleIds[disposal.CurrentApprovalStage - 1] : null;
            var stageUserId = ApprovalWorkflowSettingsHelper.TryGetCurrentStageUserId(disposal.ApprovalStageUserIds, disposal.CurrentApprovalStage);

            return new PendingDisposalApprovalVm
            {
                DisposalRecordId = disposal.Id,
                RequestedByName = disposal.RequestedById,
                RequestedDateText = disposal.DisposalRequestDate.ToString("yyyy-MM-dd HH:mm"),
                DisposalMethod = disposal.DisposalMethod.ToString(),
                DisposalReason = disposal.DisposalReason,
                Notes = disposal.Notes,
                CurrentApprovalStage = disposal.CurrentApprovalStage,
                CurrentStageRoleId = stageRoleId,
                CurrentStageRoleName = ApprovalWorkflowSettingsHelper.ResolveRoleName(roleLookup, stageRoleId),
                CurrentStageUserId = stageUserId,
                CurrentStageUserName = ResolveApproverDisplayName(stageUserId),
                ApprovalHistory = ApprovalWorkflowHelper.MapDecisionHistory(
                    actions.Select(x => ApprovalWorkflowHelper.ToSnapshot(
                        x.StageNumber, x.RoleId, x.ApproverUserId, x.Decision, x.Notes, x.DecisionDate)),
                    roleLookup)
            };
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

        private static void ApplyApprovalNotes(DisposalRecord pendingRequest, string notes)
        {
            pendingRequest.Notes = string.IsNullOrWhiteSpace(notes) ? pendingRequest.Notes : notes.Trim();
        }

        private void AddDisposalCustodyEvent(Asset asset, DisposalRecord pendingRequest, string approvedByUserId, string fromUserId, int? fromDepartmentId)
        {
            _unitOfWork.Repository<AssetCustodyEvent>().Add(new AssetCustodyEvent
            {
                AssetId = asset.Id,
                ActionType = CustodyActionType.Disposed,
                ActionDate = pendingRequest.DisposalDate ?? DateTime.UtcNow,
                FromUserId = fromUserId,
                FromDepartmentId = fromDepartmentId,
                Reason = pendingRequest.DisposalReason,
                ApprovedById = approvedByUserId,
                Notes = pendingRequest.Notes,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        private void EnsureDisposalRequestInput(AssetDisposalRequestVm model, string requestedByUserId)
        {
            if (model == null)
            {
                throw new BusinessException("Disposal request payload is required.");
            }

            if (string.IsNullOrWhiteSpace(requestedByUserId))
            {
                throw new BusinessException("Current user is required for disposal request.");
            }

            if (string.IsNullOrWhiteSpace(model.DisposalReason))
            {
                throw new BusinessException("Disposal reason is required.");
            }
        }

        private void EnsureDisposalApprovalInput(AssetDisposalApprovalVm model, string approvedByUserId)
        {
            if (model == null)
            {
                throw new BusinessException("Disposal approval payload is required.");
            }

            if (string.IsNullOrWhiteSpace(approvedByUserId))
            {
                throw new BusinessException("Approver user is required.");
            }
        }

        private void EnsureCanRequestDisposal(Asset asset, int assetId)
        {
            if (asset.CurrentStatus == AssetStatus.Disposed || asset.CurrentStatus == AssetStatus.Retired)
            {
                throw new BusinessException("Disposed or retired assets cannot be submitted for disposal.");
            }

            var hasPendingDisposal = _unitOfWork.Repository<DisposalRecord>()
                .Find(x => x.AssetId == assetId && x.ApprovalStatus == ApprovalStatus.Pending && x.IsActive)
                .Any();
            if (hasPendingDisposal)
            {
                throw new BusinessException("A pending disposal request already exists for this asset.");
            }

            if (asset.CurrentStatus == AssetStatus.AwaitingApproval)
            {
                throw new BusinessException("This asset has another approval workflow in progress. Resolve that request before submitting disposal.");
            }
        }

        private DisposalRecord GetPendingDisposalRequest(int assetId)
        {
            var pendingRequest = _unitOfWork.Repository<DisposalRecord>()
                .Find(x => x.AssetId == assetId && x.ApprovalStatus == ApprovalStatus.Pending && x.IsActive)
                .OrderByDescending(x => x.DisposalRequestDate)
                .FirstOrDefault();
            if (pendingRequest == null)
            {
                throw new BusinessException("No pending disposal request found for this asset.");
            }

            return pendingRequest;
        }

        private string ResolveAssetTagForCreate(AssetCreateVm model)
        {
            if (!string.IsNullOrWhiteSpace(model.AssetTag))
            {
                return model.AssetTag;
            }

            Department department = null;
            if (model.DepartmentId.HasValue && model.DepartmentId.Value > 0)
            {
                department = _unitOfWork.Repository<Department>().GetById(model.DepartmentId.Value);
                if (department == null)
                {
                    throw new BusinessException("Department was not found.");
                }
            }

            var assetType = _unitOfWork.Repository<AssetType>().GetById(model.AssetTypeId);
            if (assetType == null)
            {
                throw new BusinessException("Asset type was not found.");
            }

            var departmentCode = AssetTagHelper.ResolveDepartmentCode(department);
            var activeAssets = _unitOfWork.Repository<Asset>().Query().Where(x => x.IsActive);
            var prefix = AssetTagHelper.BuildTagPrefix(departmentCode, assetType.Name) + "-";
            var sequence = AssetTagHelper.GetNextSequence(activeAssets, prefix);

            for (var attempt = 0; attempt < 10; attempt++)
            {
                var candidate = prefix + sequence.ToString("000");
                if (!activeAssets.Any(x => x.AssetTag == candidate))
                {
                    return candidate;
                }

                sequence++;
            }

            throw new BusinessException("Unable to generate a unique asset tag. Please try again.");
        }

        private void ValidateUniqueness(string assetTag, string serialNumber, int? ignoreId)
        {
            var tagQuery = _unitOfWork.Repository<Asset>().Query()
                .Where(x => x.IsActive && x.AssetTag == assetTag);
            if (ignoreId.HasValue)
            {
                tagQuery = tagQuery.Where(x => x.Id != ignoreId.Value);
            }

            if (tagQuery.Any())
            {
                throw new BusinessException("AssetTag must be unique.");
            }

            if (!string.IsNullOrWhiteSpace(serialNumber))
            {
                var serialQuery = _unitOfWork.Repository<Asset>().Query()
                    .Where(x => x.IsActive && x.SerialNumber == serialNumber);
                if (ignoreId.HasValue)
                {
                    serialQuery = serialQuery.Where(x => x.Id != ignoreId.Value);
                }

                if (serialQuery.Any())
                {
                    throw new BusinessException("SerialNumber must be unique when provided.");
                }
            }
        }

        public static string NormalizeAssetTag(string assetTag)
        {
            if (string.IsNullOrWhiteSpace(assetTag))
            {
                throw new BusinessException("Asset tag is required.");
            }

            return assetTag.Trim().ToUpperInvariant();
        }

        private static string BuildStatusGuidance(AssetStatus status)
        {
            switch (status)
            {
                case AssetStatus.Assigned:
                    return "This asset is assigned. Use transfer to move custody; return before disposal.";
                case AssetStatus.InStore:
                    return "Asset is in store and available for assignment.";
                case AssetStatus.AwaitingApproval:
                    return "A workflow is awaiting approval. Resolve pending transfer or disposal before other actions.";
                case AssetStatus.Disposed:
                case AssetStatus.Retired:
                    return "This asset is closed. No assignment, transfer, or disposal actions are allowed.";
                case AssetStatus.UnderMaintenance:
                    return "Asset is under maintenance. Complete maintenance before reassignment when possible.";
                case AssetStatus.Lost:
                case AssetStatus.Stolen:
                    return "This asset is reported lost or stolen. Custody changes are blocked until it is recovered or written off.";
                case AssetStatus.Damaged:
                    return "This asset is marked damaged. Resolve the incident before assigning or transferring custody.";
                case AssetStatus.Returned:
                    return "Asset has been returned to store and is available for assignment.";
                default:
                    return "Review custody history before initiating assignment or transfer.";
            }
        }

        private static string BuildTransferEntityLabel(string userId, int? departmentId, IDictionary<int, string> departmentLookup)
        {
            if (!string.IsNullOrWhiteSpace(userId))
            {
                return userId;
            }

            if (departmentId.HasValue && departmentLookup != null && departmentLookup.ContainsKey(departmentId.Value))
            {
                return departmentLookup[departmentId.Value];
            }

            return null;
        }

        private static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static int? NormalizeOptionalId(int? value)
        {
            return value.HasValue && value.Value > 0 ? value : null;
        }

        private bool CanViewAssetForPendingTransferApproval(Asset asset)
        {
            var userId = _currentUser == null ? null : _currentUser.UserId;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            return ApprovalWorkflowHelper.CanAccessAssetForPendingApproval(
                _unitOfWork,
                _userService,
                userId,
                asset,
                _departmentScope.BypassesDepartmentScope);
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public class MaintenanceService : IMaintenanceService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;
        private readonly IDepartmentScopeService _departmentScope;
        private readonly IAssignmentService _assignmentService;
        private readonly IUserService _userService;

        public MaintenanceService(IUnitOfWork unitOfWork)
            : this(unitOfWork, null, null, null, null)
        {
        }

        public MaintenanceService(IUnitOfWork unitOfWork, IAuditWriter auditWriter)
            : this(unitOfWork, auditWriter, null, null, null)
        {
        }

        public MaintenanceService(
            IUnitOfWork unitOfWork,
            IAuditWriter auditWriter,
            IDepartmentScopeService departmentScope,
            IAssignmentService assignmentService,
            IUserService userService)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
            _departmentScope = departmentScope;
            _assignmentService = assignmentService;
            _userService = userService;
        }

        public void Create(AssetMaintenanceVm model)
        {
            if (model == null)
            {
                throw new BusinessException("Maintenance request is required.");
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(model.AssetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            if (_departmentScope != null)
            {
                _departmentScope.EnsureCanAccessAsset(asset);
            }

            if (asset.CurrentStatus == AssetStatus.Disposed || asset.CurrentStatus == AssetStatus.Retired)
            {
                throw new BusinessException("Disposed or retired assets cannot be sent for maintenance.");
            }

            if (string.IsNullOrWhiteSpace(model.ReportedIssue))
            {
                throw new BusinessException("Reported issue is required.");
            }

            MaintenanceType type;
            if (!TryParseMaintenanceType(model.MaintenanceType, out type))
            {
                throw new BusinessException("Invalid maintenance type.");
            }

            var hasOpenMaintenance = _unitOfWork.Repository<AssetMaintenanceRecord>()
                .Find(x => x.AssetId == model.AssetId
                           && x.IsActive
                           && (x.Status == MaintenanceStatus.Open || x.Status == MaintenanceStatus.InProgress))
                .Any();
            if (hasOpenMaintenance)
            {
                throw new BusinessException("An open maintenance ticket already exists for this asset.");
            }

            var now = DateTime.UtcNow;
            var maintenance = new AssetMaintenanceRecord
            {
                AssetId = model.AssetId,
                MaintenanceTicketNumber = "MT-" + now.Ticks,
                ReportedIssue = NormalizeText(model.ReportedIssue),
                MaintenanceType = type,
                ServiceDate = now,
                Status = MaintenanceStatus.Open,
                CreatedAt = now
            };

            _unitOfWork.Repository<AssetMaintenanceRecord>().Add(maintenance);

            if (asset.CurrentStatus != AssetStatus.UnderMaintenance)
            {
                asset.CurrentStatus = AssetStatus.UnderMaintenance;
                asset.UpdatedAt = now;
                _unitOfWork.Repository<Asset>().Update(asset);
            }

            _unitOfWork.SaveChanges();
            _auditWriter?.Write("Maintenance.Create", nameof(AssetMaintenanceRecord), maintenance.Id.ToString(), null, maintenance.AssetId.ToString());
        }

        public MaintenanceDetailsVm GetById(int id)
        {
            var record = _unitOfWork.Repository<AssetMaintenanceRecord>().GetById(id);
            if (record == null || !record.IsActive)
            {
                return null;
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(record.AssetId);
            if (asset == null)
            {
                return null;
            }

            if (_departmentScope != null)
            {
                _departmentScope.EnsureCanAccessAsset(asset);
            }

            var previousOwnerId = ResolvePreviousCustodianId(asset, record.ServiceDate);
            return MapDetails(record, asset, previousOwnerId, canComplete: IsOpenStatus(record.Status));
        }

        public MaintenanceCompleteVm GetCompleteModel(int id)
        {
            var record = _unitOfWork.Repository<AssetMaintenanceRecord>().GetById(id);
            if (record == null || !record.IsActive)
            {
                throw new BusinessException("Maintenance ticket was not found.");
            }

            if (!IsOpenStatus(record.Status))
            {
                throw new BusinessException("This maintenance ticket is already closed.");
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(record.AssetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            if (_departmentScope != null)
            {
                _departmentScope.EnsureCanAccessAsset(asset);
            }

            var previousOwnerId = ResolvePreviousCustodianId(asset, record.ServiceDate);
            return new MaintenanceCompleteVm
            {
                Id = record.Id,
                AssetId = record.AssetId,
                MaintenanceTicketNumber = record.MaintenanceTicketNumber,
                AssetTag = asset.AssetTag,
                AssetName = asset.AssetName,
                PreviousOwnerUserId = previousOwnerId,
                PreviousOwnerName = ResolveUserDisplayName(previousOwnerId),
                CompletionDate = DateTime.UtcNow.Date,
                Disposition = previousOwnerId == null
                    ? MaintenanceDisposition.KeepInStore.ToString()
                    : MaintenanceDisposition.ReturnToPreviousOwner.ToString(),
                ConditionAfter = asset.Condition.ToString()
            };
        }

        public void Complete(MaintenanceCompleteVm model)
        {
            if (model == null)
            {
                throw new BusinessException("Maintenance completion request is required.");
            }

            MaintenanceDisposition disposition;
            if (!TryParseDisposition(model.Disposition, out disposition))
            {
                throw new BusinessException("Select what should happen after repair.");
            }

            if (model.CompletionDate == default(DateTime))
            {
                throw new BusinessException("Returned from repair date is required.");
            }

            if (string.IsNullOrWhiteSpace(model.Outcome))
            {
                throw new BusinessException("Repair outcome is required.");
            }

            if (!string.IsNullOrWhiteSpace(model.ConditionAfter))
            {
                AssetCondition parsedCondition;
                if (!Enum.TryParse(model.ConditionAfter, true, out parsedCondition)
                    || !Enum.IsDefined(typeof(AssetCondition), parsedCondition))
                {
                    throw new BusinessException("Select a valid condition after repair.");
                }
            }

            _unitOfWork.ExecuteInTransaction(() =>
            {
                var record = _unitOfWork.Repository<AssetMaintenanceRecord>().GetById(model.Id);
                if (record == null || !record.IsActive)
                {
                    throw new BusinessException("Maintenance ticket was not found.");
                }

                if (!IsOpenStatus(record.Status))
                {
                    throw new BusinessException("This maintenance ticket is already closed.");
                }

                var asset = _unitOfWork.Repository<Asset>().GetById(record.AssetId);
                if (asset == null)
                {
                    throw new BusinessException("Asset not found.");
                }

                if (_departmentScope != null)
                {
                    _departmentScope.EnsureCanAccessAsset(asset);
                }

                var completionDate = model.CompletionDate.Date.Add(DateTime.UtcNow.TimeOfDay);
                record.CompletionDate = completionDate;
                record.Outcome = NormalizeText(model.Outcome);
                record.Status = MaintenanceStatus.Completed;
                record.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Repository<AssetMaintenanceRecord>().Update(record);
                _unitOfWork.SaveChanges();

                ApplyConditionAfter(asset, model.ConditionAfter);
                asset.UpdatedAt = DateTime.UtcNow;

                switch (disposition)
                {
                    case MaintenanceDisposition.KeepInStore:
                        ApplyKeepInStore(asset, record, model);
                        break;
                    case MaintenanceDisposition.ReturnToPreviousOwner:
                        ApplyReturnToPreviousOwner(asset, record, model, completionDate);
                        break;
                    case MaintenanceDisposition.AssignToUser:
                        ApplyAssignToUser(asset, record, model, completionDate);
                        break;
                    default:
                        throw new BusinessException("Select what should happen after repair.");
                }

                _unitOfWork.Repository<Asset>().Update(asset);
            });

            _auditWriter?.Write("Maintenance.Complete", nameof(AssetMaintenanceRecord), model.Id.ToString(), model.Disposition, model.AssetId.ToString());
        }

        public IEnumerable<MaintenanceRecordListVm> GetByAsset(int assetId)
        {
            return GetMaintenanceRecords(null, assetId);
        }

        public IEnumerable<MaintenanceRecordListVm> GetMaintenanceRecords(string search, int? assetId)
        {
            var visibleAssetIds = GetVisibleAssetIds();
            var assets = _unitOfWork.Repository<Asset>().GetAll()
                .Where(x => visibleAssetIds.Contains(x.Id))
                .ToDictionary(x => x.Id, x => x);
            var query = _unitOfWork.Repository<AssetMaintenanceRecord>().GetAll().AsEnumerable()
                .Where(x => x.IsActive && visibleAssetIds.Contains(x.AssetId));
            if (assetId.HasValue)
            {
                query = query.Where(x => x.AssetId == assetId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                query = query.Where(x =>
                    (x.MaintenanceTicketNumber != null && x.MaintenanceTicketNumber.ToLowerInvariant().Contains(term))
                    || (x.ReportedIssue != null && x.ReportedIssue.ToLowerInvariant().Contains(term))
                    || (assets.ContainsKey(x.AssetId) && (
                        (assets[x.AssetId].AssetTag != null && assets[x.AssetId].AssetTag.ToLowerInvariant().Contains(term))
                        || (assets[x.AssetId].AssetName != null && assets[x.AssetId].AssetName.ToLowerInvariant().Contains(term)))));
            }

            return query.OrderByDescending(x => x.ServiceDate)
                .Select(x => MapListItem(x, assets))
                .ToList();
        }

        private void ApplyKeepInStore(Asset asset, AssetMaintenanceRecord record, MaintenanceCompleteVm model)
        {
            var fromUserId = asset.CurrentCustodianId;
            asset.CurrentStatus = AssetStatus.InStore;
            asset.CurrentCustodianId = null;
            AddMaintenanceCompleteCustodyEvent(asset, record, model, fromUserId, null, null, "Returned from maintenance to store.");
        }

        private void ApplyReturnToPreviousOwner(Asset asset, AssetMaintenanceRecord record, MaintenanceCompleteVm model, DateTime completionDate)
        {
            var previousOwnerId = ResolvePreviousCustodianId(asset, record.ServiceDate);
            if (string.IsNullOrWhiteSpace(previousOwnerId))
            {
                throw new BusinessException("No previous owner was found for this asset. Choose store or assign to a new user instead.");
            }

            var previousUser = _userService.GetById(previousOwnerId);
            if (previousUser == null || !previousUser.IsActive)
            {
                throw new BusinessException("The previous owner is no longer active. Assign the asset to another user instead.");
            }

            if (!previousUser.DepartmentId.HasValue)
            {
                throw new BusinessException("The previous owner has no department. Assign the asset manually instead.");
            }

            asset.CurrentStatus = AssetStatus.InStore;
            asset.CurrentCustodianId = null;
            _unitOfWork.Repository<Asset>().Update(asset);

            if (_assignmentService == null)
            {
                throw new BusinessException("Assignment service is not available.");
            }

            _assignmentService.AssignWithoutSave(new AssetAssignmentVm
            {
                AssetId = asset.Id,
                ToUserId = previousOwnerId,
                ToDepartmentId = previousUser.DepartmentId,
                AssignmentType = AssignmentType.Permanent.ToString(),
                AssignedDate = completionDate,
                ConditionBeforeHandover = model.ConditionAfter,
                HandoverNotes = NormalizeText(model.HandoverNotes) ?? "Returned from maintenance to previous owner.",
                HandedOverById = null
            });
        }

        private void ApplyAssignToUser(Asset asset, AssetMaintenanceRecord record, MaintenanceCompleteVm model, DateTime completionDate)
        {
            if (string.IsNullOrWhiteSpace(model.ToUserId))
            {
                throw new BusinessException("Select a user to assign the asset to.");
            }

            if (!model.ToDepartmentId.HasValue)
            {
                throw new BusinessException("Select a department for the new assignment.");
            }

            asset.CurrentStatus = AssetStatus.InStore;
            asset.CurrentCustodianId = null;
            _unitOfWork.Repository<Asset>().Update(asset);

            if (_assignmentService == null)
            {
                throw new BusinessException("Assignment service is not available.");
            }

            _assignmentService.AssignWithoutSave(new AssetAssignmentVm
            {
                AssetId = asset.Id,
                ToUserId = model.ToUserId,
                ToDepartmentId = model.ToDepartmentId,
                AssignmentType = AssignmentType.Permanent.ToString(),
                AssignedDate = completionDate,
                ConditionBeforeHandover = model.ConditionAfter,
                HandoverNotes = NormalizeText(model.HandoverNotes) ?? "Assigned after maintenance completion.",
                HandedOverById = null
            });
        }

        private void AddMaintenanceCompleteCustodyEvent(
            Asset asset,
            AssetMaintenanceRecord record,
            MaintenanceCompleteVm model,
            string fromUserId,
            string toUserId,
            int? toDepartmentId,
            string defaultNotes)
        {
            _unitOfWork.Repository<AssetCustodyEvent>().Add(new AssetCustodyEvent
            {
                AssetId = asset.Id,
                ActionType = CustodyActionType.Recovered,
                ActionDate = record.CompletionDate ?? DateTime.UtcNow,
                FromUserId = fromUserId,
                ToUserId = toUserId,
                FromDepartmentId = asset.DepartmentId,
                ToDepartmentId = toDepartmentId ?? asset.DepartmentId,
                ConditionAfter = NormalizeText(model.ConditionAfter),
                Notes = NormalizeText(model.HandoverNotes) ?? NormalizeText(model.Outcome) ?? defaultNotes,
                CreatedAt = DateTime.UtcNow
            });
        }

        private static void ApplyConditionAfter(Asset asset, string conditionAfter)
        {
            if (string.IsNullOrWhiteSpace(conditionAfter))
            {
                return;
            }

            AssetCondition parsedCondition;
            if (Enum.TryParse(conditionAfter, true, out parsedCondition) && Enum.IsDefined(typeof(AssetCondition), parsedCondition))
            {
                asset.Condition = parsedCondition;
            }
        }

        private string ResolvePreviousCustodianId(Asset asset, DateTime maintenanceStarted)
        {
            if (!string.IsNullOrWhiteSpace(asset.CurrentCustodianId))
            {
                return asset.CurrentCustodianId;
            }

            var lastAssignment = _unitOfWork.Repository<AssetAssignment>()
                .Find(x => x.AssetId == asset.Id && x.IsActive && x.AssignedDate <= maintenanceStarted)
                .OrderByDescending(x => x.AssignedDate)
                .FirstOrDefault();

            return lastAssignment == null ? null : lastAssignment.ToUserId;
        }

        private string ResolveUserDisplayName(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId) || _userService == null)
            {
                return null;
            }

            var user = _userService.GetById(userId);
            if (user == null)
            {
                return userId;
            }

            var name = (user.FirstName + " " + user.LastName).Trim();
            return string.IsNullOrWhiteSpace(name) ? user.Email ?? userId : name;
        }

        private MaintenanceDetailsVm MapDetails(AssetMaintenanceRecord record, Asset asset, string previousOwnerId, bool canComplete)
        {
            return new MaintenanceDetailsVm
            {
                Id = record.Id,
                AssetId = record.AssetId,
                AssetTag = asset.AssetTag,
                AssetName = asset.AssetName,
                MaintenanceTicketNumber = record.MaintenanceTicketNumber,
                MaintenanceType = record.MaintenanceType.ToString(),
                ReportedIssue = record.ReportedIssue,
                Status = record.Status,
                ServiceDate = record.ServiceDate,
                CompletionDate = record.CompletionDate,
                AssignedTechnicianOrVendor = record.AssignedTechnicianOrVendor,
                Cost = record.Cost,
                Outcome = record.Outcome,
                PreviousOwnerUserId = previousOwnerId,
                PreviousOwnerName = ResolveUserDisplayName(previousOwnerId),
                CanComplete = canComplete
            };
        }

        private static MaintenanceRecordListVm MapListItem(AssetMaintenanceRecord record, IDictionary<int, Asset> assets)
        {
            Asset asset;
            assets.TryGetValue(record.AssetId, out asset);
            return new MaintenanceRecordListVm
            {
                Id = record.Id,
                AssetId = record.AssetId,
                AssetTag = asset == null ? null : asset.AssetTag,
                AssetName = asset == null ? null : asset.AssetName,
                MaintenanceTicketNumber = record.MaintenanceTicketNumber,
                MaintenanceType = record.MaintenanceType.ToString(),
                ReportedIssue = record.ReportedIssue,
                Status = record.Status,
                ServiceDate = record.ServiceDate,
                CompletionDate = record.CompletionDate,
                Outcome = record.Outcome
            };
        }

        private static bool IsOpenStatus(MaintenanceStatus status)
        {
            return status == MaintenanceStatus.Open || status == MaintenanceStatus.InProgress;
        }

        private static bool TryParseDisposition(string value, out MaintenanceDisposition disposition)
        {
            disposition = MaintenanceDisposition.KeepInStore;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!Enum.TryParse(value, true, out disposition))
            {
                return false;
            }

            return Enum.IsDefined(typeof(MaintenanceDisposition), disposition);
        }

        private static bool TryParseMaintenanceType(string value, out MaintenanceType maintenanceType)
        {
            maintenanceType = MaintenanceType.Corrective;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!Enum.TryParse(value, true, out maintenanceType))
            {
                return false;
            }

            return Enum.IsDefined(typeof(MaintenanceType), maintenanceType);
        }

        private static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private HashSet<int> GetVisibleAssetIds()
        {
            if (_departmentScope == null)
            {
                return new HashSet<int>(_unitOfWork.Repository<Asset>().GetAll().Select(x => x.Id));
            }

            return new HashSet<int>(_departmentScope.ApplyAssetScope(_unitOfWork.Repository<Asset>().Query()).Select(x => x.Id));
        }
    }
}

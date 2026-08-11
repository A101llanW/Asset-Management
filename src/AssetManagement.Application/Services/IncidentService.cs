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
    public class IncidentService : IIncidentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;
        private readonly IDepartmentScopeService _departmentScope;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IOperationsQueryRepository _operationsQueryRepository;

        public IncidentService(IUnitOfWork unitOfWork)
            : this(unitOfWork, null, null, null, null)
        {
        }

        public IncidentService(IUnitOfWork unitOfWork, IAuditWriter auditWriter)
            : this(unitOfWork, auditWriter, null, null, null)
        {
        }

        public IncidentService(
            IUnitOfWork unitOfWork,
            IAuditWriter auditWriter,
            IDepartmentScopeService departmentScope,
            IOrganizationScopeService organizationScope = null,
            IOperationsQueryRepository operationsQueryRepository = null)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
            _departmentScope = departmentScope;
            _organizationScope = organizationScope;
            _operationsQueryRepository = operationsQueryRepository;
        }

        public int Create(AssetIncidentVm model)
        {
            if (model == null)
            {
                throw new BusinessException("Incident request is required.");
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
                throw new BusinessException("Disposed or retired assets cannot be reported in new incidents.");
            }

            if (string.IsNullOrWhiteSpace(model.Description))
            {
                throw new BusinessException("Incident description is required.");
            }

            IncidentType type;
            if (!TryParseIncidentType(model.IncidentType, out type))
            {
                throw new BusinessException("Invalid incident type.");
            }

            IncidentSeverity severity;
            if (!TryParseSeverity(model.Severity, out severity))
            {
                throw new BusinessException("Invalid incident severity.");
            }

            var incidentDate = model.IncidentDate == default(DateTime) ? DateTime.UtcNow : model.IncidentDate;
            if (incidentDate > DateTime.UtcNow.AddMinutes(5))
            {
                throw new BusinessException("Incident date cannot be in the future.");
            }

            var now = DateTime.UtcNow;
            var normalizedDescription = NormalizeText(model.Description);
            var duplicateWindowStart = now.AddMinutes(-2);
            var hasRecentDuplicate = _unitOfWork.Repository<AssetIncident>()
                .Find(x => x.AssetId == model.AssetId
                           && x.IsActive
                           && x.IncidentType == type
                           && x.Description == normalizedDescription
                           && x.CreatedAt >= duplicateWindowStart)
                .Any();
            if (hasRecentDuplicate)
            {
                throw new BusinessException("This incident was already submitted. Refresh the asset page to view it.");
            }

            var incident = new AssetIncident
            {
                AssetId = model.AssetId,
                IncidentNumber = "INC-" + now.Ticks,
                IncidentType = type,
                IncidentDate = incidentDate,
                Description = normalizedDescription,
                Severity = severity,
                ResolutionStatus = IncidentResolutionStatusHelper.Open,
                CreatedAt = now
            };

            _unitOfWork.Repository<AssetIncident>().Add(incident);

            var targetStatus = DetermineAssetStatus(type);
            if (targetStatus.HasValue)
            {
                asset.CurrentStatus = targetStatus.Value;
                asset.UpdatedAt = now;
                _unitOfWork.Repository<Asset>().Update(asset);
            }

            _unitOfWork.SaveChanges();
            _auditWriter?.Write("Incidents.Create", nameof(AssetIncident), incident.Id.ToString(), null, incident.AssetId.ToString());
            return incident.Id;
        }

        public IEnumerable<IncidentListVm> GetIncidents(string search, int? assetId)
        {
            var visibleAssetIds = GetVisibleAssetIds();
            var assets = _unitOfWork.Repository<Asset>().GetAll()
                .Where(x => visibleAssetIds.Contains(x.Id))
                .ToDictionary(x => x.Id, x => x);
            var query = _unitOfWork.Repository<AssetIncident>().GetAll().AsEnumerable()
                .Where(x => visibleAssetIds.Contains(x.AssetId));
            if (assetId.HasValue)
            {
                query = query.Where(x => x.AssetId == assetId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                query = query.Where(x =>
                    (x.IncidentNumber != null && x.IncidentNumber.ToLowerInvariant().Contains(term))
                    || (x.Description != null && x.Description.ToLowerInvariant().Contains(term))
                    || (assets.ContainsKey(x.AssetId) && (
                        (assets[x.AssetId].AssetTag != null && assets[x.AssetId].AssetTag.ToLowerInvariant().Contains(term))
                        || (assets[x.AssetId].AssetName != null && assets[x.AssetId].AssetName.ToLowerInvariant().Contains(term)))));
            }

            return query.OrderByDescending(x => x.IncidentDate)
                .Select(x => MapListItem(x, assets))
                .ToList();
        }

        public PagedListVm<IncidentListVm> GetListPage(string search, int? assetId, int page, int pageSize)
        {
            var organizationId = _organizationScope?.GetCurrentOrganizationId();
            if (!organizationId.HasValue || _operationsQueryRepository == null)
            {
                var items = GetIncidents(search, assetId).ToList();
                var safePageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);
                var totalCount = items.Count;
                var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / safePageSize));
                var safePage = Math.Min(Math.Max(page, 1), totalPages);
                return new PagedListVm<IncidentListVm>
                {
                    Items = items.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToList(),
                    TotalCount = totalCount,
                    Search = search,
                    Page = safePage,
                    PageSize = safePageSize
                };
            }

            int? departmentId;
            bool bypassDepartmentScope;
            bool denyDepartmentScope;
            ResolveDepartmentScope(out departmentId, out bypassDepartmentScope, out denyDepartmentScope);
            return _operationsQueryRepository.GetIncidentListPage(
                organizationId.Value,
                departmentId,
                bypassDepartmentScope,
                denyDepartmentScope,
                search,
                assetId,
                page,
                pageSize);
        }

        public IncidentDetailsVm GetById(int id)
        {
            var incident = _unitOfWork.Repository<AssetIncident>().GetById(id);
            if (incident == null)
            {
                return null;
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(incident.AssetId);
            return new IncidentDetailsVm
            {
                Id = incident.Id,
                IncidentNumber = incident.IncidentNumber,
                AssetId = incident.AssetId,
                AssetTag = asset == null ? null : asset.AssetTag,
                AssetName = asset == null ? null : asset.AssetName,
                IncidentType = incident.IncidentType.ToString(),
                Severity = incident.Severity,
                IncidentDate = incident.IncidentDate,
                Description = incident.Description,
                WitnessComments = incident.WitnessComments,
                PoliceCaseReference = incident.PoliceCaseReference,
                LiabilityNotes = incident.LiabilityNotes,
                ResolutionStatus = incident.ResolutionStatus,
                CreatedAt = incident.CreatedAt
            };
        }

        public void UpdateResolutionStatus(int id, string resolutionStatus)
        {
            if (!IncidentResolutionStatusHelper.IsValid(resolutionStatus))
            {
                throw new BusinessException("Select a valid resolution status.");
            }

            var incident = _unitOfWork.Repository<AssetIncident>().GetById(id);
            if (incident == null)
            {
                throw new BusinessException("Incident not found.");
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(incident.AssetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            if (_departmentScope != null)
            {
                _departmentScope.EnsureCanAccessAsset(asset);
            }

            var normalizedStatus = IncidentResolutionStatusHelper.Normalize(resolutionStatus);
            IncidentClaimLinkageHelper.ApplyIncidentResolutionEffects(_unitOfWork, incident, normalizedStatus, asset);

            incident.ResolutionStatus = normalizedStatus;
            incident.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<AssetIncident>().Update(incident);
            _unitOfWork.SaveChanges();
            _auditWriter?.Write("Incidents.Edit", nameof(AssetIncident), incident.Id.ToString(), null, incident.ResolutionStatus);
        }

        private static IncidentListVm MapListItem(AssetIncident incident, System.Collections.Generic.IDictionary<int, Asset> assets)
        {
            Asset asset;
            assets.TryGetValue(incident.AssetId, out asset);
            return new IncidentListVm
            {
                Id = incident.Id,
                IncidentNumber = incident.IncidentNumber,
                AssetId = incident.AssetId,
                AssetTag = asset == null ? null : asset.AssetTag,
                AssetName = asset == null ? null : asset.AssetName,
                IncidentType = incident.IncidentType.ToString(),
                Severity = incident.Severity,
                IncidentDate = incident.IncidentDate,
                ResolutionStatus = incident.ResolutionStatus
            };
        }

        private static bool TryParseIncidentType(string value, out IncidentType incidentType)
        {
            incidentType = default(IncidentType);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!Enum.TryParse(value.Trim(), true, out incidentType))
            {
                return false;
            }

            return Enum.IsDefined(typeof(IncidentType), incidentType);
        }

        private static bool TryParseSeverity(string value, out IncidentSeverity severity)
        {
            severity = default(IncidentSeverity);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!Enum.TryParse(value, true, out severity))
            {
                return false;
            }

            return Enum.IsDefined(typeof(IncidentSeverity), severity);
        }

        private static AssetStatus? DetermineAssetStatus(IncidentType type)
        {
            switch (type)
            {
                case IncidentType.Lost:
                    return AssetStatus.Lost;
                case IncidentType.Stolen:
                    return AssetStatus.Stolen;
                case IncidentType.Damaged:
                case IncidentType.FireDamage:
                case IncidentType.WaterDamage:
                case IncidentType.Accident:
                case IncidentType.Negligence:
                case IncidentType.Misuse:
                    return AssetStatus.Damaged;
                default:
                    return null;
            }
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

        private void ResolveDepartmentScope(out int? departmentId, out bool bypassDepartmentScope, out bool denyDepartmentScope)
        {
            bypassDepartmentScope = _departmentScope == null || _departmentScope.BypassesDepartmentScope;
            departmentId = null;
            denyDepartmentScope = false;
            if (!bypassDepartmentScope)
            {
                departmentId = _departmentScope.ScopedDepartmentId;
                denyDepartmentScope = !departmentId.HasValue;
            }
        }
    }
}

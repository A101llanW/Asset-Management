using System;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public class IncidentService : IIncidentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;

        public IncidentService(IUnitOfWork unitOfWork)
            : this(unitOfWork, null)
        {
        }

        public IncidentService(IUnitOfWork unitOfWork, IAuditWriter auditWriter)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
        }

        public void Create(AssetIncidentVm model)
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

            if (asset.CurrentStatus == AssetStatus.Disposed || asset.CurrentStatus == AssetStatus.Retired)
            {
                throw new BusinessException("Disposed or retired assets cannot be reported in new incidents.");
            }

            if (string.IsNullOrWhiteSpace(model.Description))
            {
                throw new BusinessException("Incident description is required.");
            }

            if (!TryParseIncidentType(model.IncidentType, out var type))
            {
                throw new BusinessException("Invalid incident type.");
            }

            var incidentDate = model.IncidentDate == default(DateTime) ? DateTime.UtcNow : model.IncidentDate;
            if (incidentDate > DateTime.UtcNow.AddMinutes(5))
            {
                throw new BusinessException("Incident date cannot be in the future.");
            }

            var now = DateTime.UtcNow;
            var incident = new AssetIncident
            {
                AssetId = model.AssetId,
                IncidentNumber = "INC-" + now.Ticks,
                IncidentType = type,
                IncidentDate = incidentDate,
                Description = NormalizeText(model.Description),
                Severity = DetermineSeverity(type),
                ResolutionStatus = "Open",
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
        }

        private static bool TryParseIncidentType(string value, out IncidentType incidentType)
        {
            incidentType = IncidentType.Damaged;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!Enum.TryParse(value, true, out incidentType))
            {
                return false;
            }

            return Enum.IsDefined(typeof(IncidentType), incidentType);
        }

        private static IncidentSeverity DetermineSeverity(IncidentType type)
        {
            switch (type)
            {
                case IncidentType.Stolen:
                    return IncidentSeverity.Critical;
                case IncidentType.Lost:
                case IncidentType.FireDamage:
                case IncidentType.WaterDamage:
                    return IncidentSeverity.High;
                case IncidentType.Accident:
                case IncidentType.Negligence:
                case IncidentType.Misuse:
                    return IncidentSeverity.Medium;
                default:
                    return IncidentSeverity.Medium;
            }
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
    }
}

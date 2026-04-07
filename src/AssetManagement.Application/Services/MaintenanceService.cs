using System;
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

        public MaintenanceService(IUnitOfWork unitOfWork)
            : this(unitOfWork, null)
        {
        }

        public MaintenanceService(IUnitOfWork unitOfWork, IAuditWriter auditWriter)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
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

            if (asset.CurrentStatus == AssetStatus.Disposed || asset.CurrentStatus == AssetStatus.Retired)
            {
                throw new BusinessException("Disposed or retired assets cannot be sent for maintenance.");
            }

            if (string.IsNullOrWhiteSpace(model.ReportedIssue))
            {
                throw new BusinessException("Reported issue is required.");
            }

            if (!TryParseMaintenanceType(model.MaintenanceType, out var type))
            {
                throw new BusinessException("Invalid maintenance type.");
            }

            var hasOpenMaintenance = _unitOfWork.Repository<AssetMaintenanceRecord>()
                .Find(x => x.AssetId == model.AssetId
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
    }
}

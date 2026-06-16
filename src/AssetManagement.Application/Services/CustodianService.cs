using System;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public class CustodianService : ICustodianService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;
        private readonly IOutboxWriter _outboxWriter;
        private readonly IOrganizationScopeService _organizationScope;

        public CustodianService(
            IUnitOfWork unitOfWork,
            IAuditWriter auditWriter,
            IOutboxWriter outboxWriter,
            IOrganizationScopeService organizationScope)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
            _outboxWriter = outboxWriter;
            _organizationScope = organizationScope;
        }

        public void AcknowledgeReceipt(int assetId, string custodianUserId)
        {
            if (string.IsNullOrWhiteSpace(custodianUserId))
            {
                throw new BusinessException("Custodian identity is required.");
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(assetId);
            if (asset == null || !asset.IsActive)
            {
                throw new BusinessException("Asset not found.");
            }

            if (!string.Equals(asset.CurrentCustodianId, custodianUserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("Only the current custodian can acknowledge receipt.");
            }

            if (asset.CurrentStatus != AssetStatus.Assigned)
            {
                throw new BusinessException("Only assigned assets can be acknowledged.");
            }

            var assignment = _unitOfWork.Repository<AssetAssignment>().Find(x => x.AssetId == assetId)
                .OrderByDescending(x => x.AssignedDate)
                .FirstOrDefault();

            if (assignment == null)
            {
                throw new BusinessException("No assignment record found for this asset.");
            }

            if (assignment.RecipientAcknowledged)
            {
                throw new BusinessException("Receipt was already acknowledged.");
            }

            assignment.RecipientAcknowledged = true;
            assignment.AcknowledgedAt = DateTime.UtcNow;
            assignment.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<AssetAssignment>().Update(assignment);
            _unitOfWork.SaveChanges();
            _auditWriter.Write("Assets.AcknowledgeReceipt", nameof(AssetAssignment), assignment.Id.ToString(), null, assetId.ToString());
        }

        public void RequestReturn(int assetId, string custodianUserId, string notes)
        {
            if (string.IsNullOrWhiteSpace(custodianUserId))
            {
                throw new BusinessException("Custodian identity is required.");
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(assetId);
            if (asset == null || !asset.IsActive)
            {
                throw new BusinessException("Asset not found.");
            }

            if (!string.Equals(asset.CurrentCustodianId, custodianUserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("Only the current custodian can request a return.");
            }

            if (asset.CurrentStatus != AssetStatus.Assigned)
            {
                throw new BusinessException("Only assigned assets can be returned.");
            }

            var message = string.IsNullOrWhiteSpace(notes)
                ? "Custodian requested return for asset " + asset.AssetTag + "."
                : notes.Trim();

            NotificationHelper.AddNotification(
                _unitOfWork,
                _outboxWriter,
                _organizationScope,
                null,
                NotificationType.General,
                "Return requested: " + asset.AssetTag,
                message,
                "/Assets/Details/" + asset.Id);

            _unitOfWork.SaveChanges();
            _auditWriter.Write("Assets.RequestReturn", nameof(Asset), asset.Id.ToString(), null, custodianUserId);
        }
    }
}

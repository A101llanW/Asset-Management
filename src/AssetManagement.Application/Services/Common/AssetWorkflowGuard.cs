using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public class AssetWorkflowGuard : IAssetWorkflowGuard
    {
        private readonly IUnitOfWork _unitOfWork;

        public AssetWorkflowGuard(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public void EnsureNoBlockingWorkflow(int assetId)
        {
            var asset = _unitOfWork.Repository<Asset>().GetById(assetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            if (asset.CurrentStatus == AssetStatus.AwaitingApproval)
            {
                throw new BusinessException("This asset has a pending approval workflow. Resolve it before continuing.");
            }

            var pendingTransfer = _unitOfWork.Repository<AssetTransfer>()
                .Find(x => x.AssetId == assetId && x.ApprovalStatus == ApprovalStatus.Pending && x.IsActive)
                .Any();
            if (pendingTransfer)
            {
                throw new BusinessException("A transfer request is pending approval for this asset.");
            }

            var pendingDisposal = _unitOfWork.Repository<DisposalRecord>()
                .Find(x => x.AssetId == assetId && x.ApprovalStatus == ApprovalStatus.Pending && x.IsActive)
                .Any();
            if (pendingDisposal)
            {
                throw new BusinessException("A disposal request is pending approval for this asset.");
            }

            var openMaintenance = _unitOfWork.Repository<AssetMaintenanceRecord>()
                .Find(x => x.AssetId == assetId
                    && x.IsActive
                    && (x.Status == MaintenanceStatus.Open || x.Status == MaintenanceStatus.InProgress))
                .Any();
            if (openMaintenance)
            {
                throw new BusinessException("An open maintenance ticket exists for this asset. Complete or cancel it first.");
            }
        }
    }
}

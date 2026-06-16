using System;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    public static class IncidentClaimLinkageHelper
    {
        public static bool IsActiveClaimStatus(ClaimStatus status)
        {
            return status == ClaimStatus.Draft
                || status == ClaimStatus.Submitted
                || status == ClaimStatus.UnderReview
                || status == ClaimStatus.Approved;
        }

        public static bool HasActiveClaimsForIncident(IUnitOfWork unitOfWork, int incidentId, int? excludeClaimId = null)
        {
            return unitOfWork.Repository<InsuranceClaim>().Find(x => x.IncidentId == incidentId)
                .Any(x => (!excludeClaimId.HasValue || x.Id != excludeClaimId.Value) && IsActiveClaimStatus(x.ClaimStatus));
        }

        public static void SyncIncidentForClaim(IUnitOfWork unitOfWork, InsuranceClaim claim, ClaimStatus claimStatus)
        {
            if (!claim.IncidentId.HasValue)
            {
                return;
            }

            var incident = unitOfWork.Repository<AssetIncident>().GetById(claim.IncidentId.Value);
            if (incident == null)
            {
                return;
            }

            string targetStatus = null;
            switch (claimStatus)
            {
                case ClaimStatus.Submitted:
                case ClaimStatus.UnderReview:
                case ClaimStatus.Approved:
                    targetStatus = IncidentResolutionStatusHelper.UnderReview;
                    break;
                case ClaimStatus.Settled:
                    targetStatus = IncidentResolutionStatusHelper.Closed;
                    break;
                case ClaimStatus.Rejected:
                    targetStatus = HasActiveClaimsForIncident(unitOfWork, incident.Id, claim.Id)
                        ? IncidentResolutionStatusHelper.UnderReview
                        : IncidentResolutionStatusHelper.Open;
                    break;
            }

            if (string.IsNullOrWhiteSpace(targetStatus))
            {
                return;
            }

            incident.ResolutionStatus = targetStatus;
            incident.UpdatedAt = DateTime.UtcNow;
            unitOfWork.Repository<AssetIncident>().Update(incident);
        }

        public static void ApplyIncidentResolutionEffects(IUnitOfWork unitOfWork, AssetIncident incident, string normalizedStatus, Asset asset)
        {
            if (incident == null || asset == null)
            {
                return;
            }

            if (normalizedStatus == IncidentResolutionStatusHelper.Closed)
            {
                if (HasActiveClaimsForIncident(unitOfWork, incident.Id))
                {
                    throw new BusinessException("Settle or reject all open insurance claims before closing this incident.");
                }

                if (asset.CurrentStatus == AssetStatus.Damaged)
                {
                    asset.CurrentStatus = AssetStatus.InStore;
                    asset.UpdatedAt = DateTime.UtcNow;
                    unitOfWork.Repository<Asset>().Update(asset);
                }

                return;
            }

            if (normalizedStatus != IncidentResolutionStatusHelper.WrittenOff)
            {
                return;
            }

            if (HasActiveClaimsForIncident(unitOfWork, incident.Id))
            {
                throw new BusinessException("Settle or reject all open insurance claims before writing off this incident.");
            }

            if (asset.CurrentStatus != AssetStatus.Lost && asset.CurrentStatus != AssetStatus.Stolen)
            {
                asset.CurrentStatus = AssetStatus.Retired;
                asset.UpdatedAt = DateTime.UtcNow;
                unitOfWork.Repository<Asset>().Update(asset);
            }
        }
    }
}

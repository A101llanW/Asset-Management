using System;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public class ClaimService : IClaimService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;

        public ClaimService(IUnitOfWork unitOfWork)
            : this(unitOfWork, null)
        {
        }

        public ClaimService(IUnitOfWork unitOfWork, IAuditWriter auditWriter)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
        }

        public void Create(InsuranceClaimVm model)
        {
            if (model == null)
            {
                throw new BusinessException("Claim request is required.");
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(model.AssetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            var hasInsurancePolicy = _unitOfWork.Repository<InsurancePolicy>().Find(x => x.AssetId == asset.Id).Any();
            if (!asset.IsInsured && !hasInsurancePolicy)
            {
                throw new BusinessException("Claims can only be created for insured assets.");
            }

            if (string.IsNullOrWhiteSpace(model.ClaimType))
            {
                throw new BusinessException("Claim type is required.");
            }

            if (string.IsNullOrWhiteSpace(model.Insurer))
            {
                throw new BusinessException("Insurer is required.");
            }

            var claimDate = model.ClaimDate == default(DateTime) ? DateTime.UtcNow : model.ClaimDate;
            if (claimDate > DateTime.UtcNow.AddMinutes(5))
            {
                throw new BusinessException("Claim date cannot be in the future.");
            }

            if (model.IncidentId.HasValue)
            {
                var incident = _unitOfWork.Repository<AssetIncident>().GetById(model.IncidentId.Value);
                if (incident == null)
                {
                    throw new BusinessException("Selected incident does not exist.");
                }

                if (incident.AssetId != model.AssetId)
                {
                    throw new BusinessException("Selected incident does not belong to the chosen asset.");
                }

                var hasOpenClaimForIncident = _unitOfWork.Repository<InsuranceClaim>().Find(x => x.IncidentId == model.IncidentId.Value)
                    .Any(x => x.ClaimStatus == ClaimStatus.Draft
                              || x.ClaimStatus == ClaimStatus.Submitted
                              || x.ClaimStatus == ClaimStatus.UnderReview
                              || x.ClaimStatus == ClaimStatus.Approved);
                if (hasOpenClaimForIncident)
                {
                    throw new BusinessException("An active claim already exists for this incident.");
                }
            }

            var now = DateTime.UtcNow;
            var claim = new InsuranceClaim
            {
                ClaimNumber = "CLM-" + now.Ticks,
                AssetId = model.AssetId,
                IncidentId = model.IncidentId,
                ClaimDate = claimDate,
                ClaimType = NormalizeText(model.ClaimType),
                Insurer = NormalizeText(model.Insurer),
                ClaimStatus = ClaimStatus.Submitted,
                CreatedAt = now
            };

            _unitOfWork.Repository<InsuranceClaim>().Add(claim);

            _unitOfWork.SaveChanges();
            _auditWriter?.Write("Claims.Create", nameof(InsuranceClaim), claim.Id.ToString(), null, claim.AssetId.ToString());
        }

        private static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}

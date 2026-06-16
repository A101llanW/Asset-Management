using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public class ClaimService : IClaimService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;
        private readonly IDepartmentScopeService _departmentScope;

        public ClaimService(IUnitOfWork unitOfWork)
            : this(unitOfWork, null, null)
        {
        }

        public ClaimService(IUnitOfWork unitOfWork, IAuditWriter auditWriter)
            : this(unitOfWork, auditWriter, null)
        {
        }

        public ClaimService(IUnitOfWork unitOfWork, IAuditWriter auditWriter, IDepartmentScopeService departmentScope)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
            _departmentScope = departmentScope;
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

            if (_departmentScope != null)
            {
                _departmentScope.EnsureCanAccessAsset(asset);
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

            if (string.Equals(model.ClaimType.Trim(), "Other", StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("Describe the other claim type before submitting.");
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

            if (model.IncidentId.HasValue)
            {
                IncidentClaimLinkageHelper.SyncIncidentForClaim(_unitOfWork, claim, ClaimStatus.Submitted);
            }

            _unitOfWork.SaveChanges();
            _auditWriter?.Write("Claims.Create", nameof(InsuranceClaim), claim.Id.ToString(), null, claim.AssetId.ToString());
        }

        public IEnumerable<ClaimListVm> GetClaims(string search, int? assetId)
        {
            var visibleAssetIds = GetVisibleAssetIds();
            var assets = _unitOfWork.Repository<Asset>().GetAll()
                .Where(x => visibleAssetIds.Contains(x.Id))
                .ToDictionary(x => x.Id, x => x);
            var query = _unitOfWork.Repository<InsuranceClaim>().GetAll().AsEnumerable()
                .Where(x => visibleAssetIds.Contains(x.AssetId));
            if (assetId.HasValue)
            {
                query = query.Where(x => x.AssetId == assetId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                query = query.Where(x =>
                    (x.ClaimNumber != null && x.ClaimNumber.ToLowerInvariant().Contains(term))
                    || (x.Insurer != null && x.Insurer.ToLowerInvariant().Contains(term))
                    || (assets.ContainsKey(x.AssetId) && (
                        (assets[x.AssetId].AssetTag != null && assets[x.AssetId].AssetTag.ToLowerInvariant().Contains(term))
                        || (assets[x.AssetId].AssetName != null && assets[x.AssetId].AssetName.ToLowerInvariant().Contains(term)))));
            }

            return query.OrderByDescending(x => x.ClaimDate)
                .Select(x => MapListItem(x, assets))
                .ToList();
        }

        public ClaimDetailsVm GetById(int id)
        {
            var claim = _unitOfWork.Repository<InsuranceClaim>().GetById(id);
            if (claim == null)
            {
                return null;
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(claim.AssetId);
            var incident = claim.IncidentId.HasValue
                ? _unitOfWork.Repository<AssetIncident>().GetById(claim.IncidentId.Value)
                : null;

            return new ClaimDetailsVm
            {
                Id = claim.Id,
                ClaimNumber = claim.ClaimNumber,
                AssetId = claim.AssetId,
                AssetTag = asset == null ? null : asset.AssetTag,
                AssetName = asset == null ? null : asset.AssetName,
                IncidentId = claim.IncidentId,
                IncidentNumber = incident == null ? null : incident.IncidentNumber,
                ClaimType = claim.ClaimType,
                Insurer = claim.Insurer,
                Assessor = claim.Assessor,
                ClaimStatus = claim.ClaimStatus,
                ApprovedAmount = claim.ApprovedAmount,
                ClaimDate = claim.ClaimDate,
                SettlementDate = claim.SettlementDate,
                SettlementNotes = claim.SettlementNotes,
                CreatedAt = claim.CreatedAt
            };
        }

        public void UpdateStatus(int id, ClaimStatus status, decimal? approvedAmount, string settlementNotes)
        {
            var claim = _unitOfWork.Repository<InsuranceClaim>().GetById(id);
            if (claim == null)
            {
                throw new BusinessException("Claim not found.");
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(claim.AssetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            if (_departmentScope != null)
            {
                _departmentScope.EnsureCanAccessAsset(asset);
            }

            if (status == ClaimStatus.Settled && (!approvedAmount.HasValue || approvedAmount.Value < 0))
            {
                throw new BusinessException("Approved amount is required when settling a claim.");
            }

            var now = DateTime.UtcNow;
            claim.ClaimStatus = status;
            if (approvedAmount.HasValue)
            {
                claim.ApprovedAmount = approvedAmount.Value;
            }

            if (status == ClaimStatus.Settled)
            {
                claim.SettlementDate = now;
                claim.SettlementNotes = NormalizeText(settlementNotes);
            }

            claim.UpdatedAt = now;
            _unitOfWork.Repository<InsuranceClaim>().Update(claim);
            IncidentClaimLinkageHelper.SyncIncidentForClaim(_unitOfWork, claim, status);

            if (status == ClaimStatus.Settled && claim.IncidentId.HasValue)
            {
                var incident = _unitOfWork.Repository<AssetIncident>().GetById(claim.IncidentId.Value);
                if (incident != null)
                {
                    IncidentClaimLinkageHelper.ApplyIncidentResolutionEffects(
                        _unitOfWork,
                        incident,
                        IncidentResolutionStatusHelper.Closed,
                        asset);
                }
            }

            _unitOfWork.SaveChanges();
            _auditWriter?.Write("Claims.Edit", nameof(InsuranceClaim), claim.Id.ToString(), null, status.ToString());
        }

        private static ClaimListVm MapListItem(InsuranceClaim claim, System.Collections.Generic.IDictionary<int, Asset> assets)
        {
            Asset asset;
            assets.TryGetValue(claim.AssetId, out asset);
            return new ClaimListVm
            {
                Id = claim.Id,
                ClaimNumber = claim.ClaimNumber,
                AssetId = claim.AssetId,
                AssetTag = asset == null ? null : asset.AssetTag,
                AssetName = asset == null ? null : asset.AssetName,
                ClaimType = claim.ClaimType,
                Insurer = claim.Insurer,
                ClaimStatus = claim.ClaimStatus,
                ClaimDate = claim.ClaimDate
            };
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

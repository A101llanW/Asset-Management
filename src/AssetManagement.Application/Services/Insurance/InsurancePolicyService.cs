using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services
{
    public class InsurancePolicyService : IInsurancePolicyService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDepartmentScopeService _departmentScope;
        private readonly IAuditWriter _auditWriter;

        public InsurancePolicyService(
            IUnitOfWork unitOfWork,
            IDepartmentScopeService departmentScope,
            IAuditWriter auditWriter)
        {
            _unitOfWork = unitOfWork;
            _departmentScope = departmentScope;
            _auditWriter = auditWriter;
        }

        public IEnumerable<InsurancePolicyListVm> GetByAsset(int assetId)
        {
            var asset = RequireAsset(assetId);
            return _unitOfWork.Repository<InsurancePolicy>().Find(x => x.AssetId == assetId)
                .OrderByDescending(x => x.PolicyEndDate)
                .Select(MapList)
                .ToList();
        }

        public InsurancePolicyEditVm GetForEdit(int id)
        {
            var entity = RequirePolicy(id);
            return MapEdit(entity);
        }

        public int Create(InsurancePolicyEditVm model)
        {
            ValidateModel(model);
            RequireAsset(model.AssetId);

            var entity = MapEntity(new InsurancePolicy(), model);
            entity.CreatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<InsurancePolicy>().Add(entity);
            _unitOfWork.SaveChanges();
            _auditWriter.Write("Insurance.Create", nameof(InsurancePolicy), entity.Id.ToString(), null, entity.PolicyNumber);
            return entity.Id;
        }

        public void Update(InsurancePolicyEditVm model)
        {
            ValidateModel(model);
            var entity = RequirePolicy(model.Id);
            RequireAsset(model.AssetId);
            MapEntity(entity, model);
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<InsurancePolicy>().Update(entity);
            _unitOfWork.SaveChanges();
            _auditWriter.Write("Insurance.Update", nameof(InsurancePolicy), entity.Id.ToString(), null, entity.PolicyNumber);
        }

        public void Delete(int id)
        {
            var entity = RequirePolicy(id);
            RequireAsset(entity.AssetId);
            _unitOfWork.Repository<InsurancePolicy>().Remove(entity);
            _unitOfWork.SaveChanges();
            _auditWriter.Write("Insurance.Delete", nameof(InsurancePolicy), entity.Id.ToString(), entity.PolicyNumber, null);
        }

        private Asset RequireAsset(int assetId)
        {
            var asset = _unitOfWork.Repository<Asset>().GetById(assetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            _departmentScope.EnsureCanAccessAsset(asset);
            return asset;
        }

        private InsurancePolicy RequirePolicy(int id)
        {
            var entity = _unitOfWork.Repository<InsurancePolicy>().GetById(id);
            if (entity == null)
            {
                throw new BusinessException("Insurance policy not found.");
            }

            RequireAsset(entity.AssetId);
            return entity;
        }

        private static void ValidateModel(InsurancePolicyEditVm model)
        {
            if (model == null)
            {
                throw new BusinessException("Insurance policy is required.");
            }

            if (model.PolicyEndDate < model.PolicyStartDate)
            {
                throw new BusinessException("Policy end date must be on or after the start date.");
            }
        }

        private static InsurancePolicyListVm MapList(InsurancePolicy entity)
        {
            return new InsurancePolicyListVm
            {
                Id = entity.Id,
                AssetId = entity.AssetId,
                InsurerName = entity.InsurerName,
                PolicyNumber = entity.PolicyNumber,
                PolicyStartDate = entity.PolicyStartDate,
                PolicyEndDate = entity.PolicyEndDate,
                InsuredValue = entity.InsuredValue,
                ClaimEligibility = entity.ClaimEligibility
            };
        }

        private static InsurancePolicyEditVm MapEdit(InsurancePolicy entity)
        {
            return new InsurancePolicyEditVm
            {
                Id = entity.Id,
                AssetId = entity.AssetId,
                InsurerName = entity.InsurerName,
                PolicyNumber = entity.PolicyNumber,
                PolicyStartDate = entity.PolicyStartDate,
                PolicyEndDate = entity.PolicyEndDate,
                InsuredValue = entity.InsuredValue,
                ValuationDate = entity.ValuationDate,
                ClaimEligibility = entity.ClaimEligibility,
                DeductibleAmount = entity.DeductibleAmount,
                ClaimNotes = entity.ClaimNotes
            };
        }

        private static InsurancePolicy MapEntity(InsurancePolicy entity, InsurancePolicyEditVm model)
        {
            entity.AssetId = model.AssetId;
            entity.InsurerName = model.InsurerName.Trim();
            entity.PolicyNumber = model.PolicyNumber.Trim();
            entity.PolicyStartDate = model.PolicyStartDate.Date;
            entity.PolicyEndDate = model.PolicyEndDate.Date;
            entity.InsuredValue = model.InsuredValue;
            entity.ValuationDate = model.ValuationDate;
            entity.ClaimEligibility = model.ClaimEligibility;
            entity.DeductibleAmount = model.DeductibleAmount;
            entity.ClaimNotes = model.ClaimNotes;
            return entity;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public class PurchaseService : IPurchaseService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOperationsQueryRepository _operationsQueryRepository;
        private readonly IOrganizationScopeService _organizationScope;

        public PurchaseService(
            IUnitOfWork unitOfWork,
            IOperationsQueryRepository operationsQueryRepository,
            IOrganizationScopeService organizationScope)
        {
            _unitOfWork = unitOfWork;
            _operationsQueryRepository = operationsQueryRepository;
            _organizationScope = organizationScope;
        }

        public IEnumerable<PurchaseRecordVm> GetAll()
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return new List<PurchaseRecordVm>();
            }

            return _operationsQueryRepository.GetPurchaseRecordList(organizationId.Value);
        }

        public PurchaseRecordVm GetById(int id)
        {
            var suppliers = _unitOfWork.Repository<Supplier>().GetAll()
                .ToDictionary(x => x.Id, x => x.SupplierName);
            var entity = _unitOfWork.Repository<PurchaseRecord>().GetById(id);
            if (entity == null)
            {
                return null;
            }

            var requestNumber = entity.PurchaseRequestId.HasValue
                ? _unitOfWork.Repository<PurchaseRequest>().GetById(entity.PurchaseRequestId.Value)?.RequestNumber
                : null;

            return new PurchaseRecordVm
            {
                Id = entity.Id,
                PurchaseRequestId = entity.PurchaseRequestId,
                PurchaseRequestNumber = requestNumber,
                PurchaseOrderNumber = entity.PurchaseOrderNumber,
                SupplierId = entity.SupplierId,
                SupplierName = suppliers.ContainsKey(entity.SupplierId) ? suppliers[entity.SupplierId] : null,
                InvoiceNumber = entity.InvoiceNumber,
                PurchaseDate = entity.PurchaseDate,
                Quantity = entity.Quantity,
                UnitCost = entity.UnitCost,
                TotalCost = entity.TotalCost,
                Currency = entity.Currency
            };
        }

        public int Create(PurchaseRecordVm model)
        {
            if (model.PurchaseRequestId.HasValue)
            {
                var request = _unitOfWork.Repository<PurchaseRequest>().GetById(model.PurchaseRequestId.Value);
                if (request == null)
                {
                    throw new BusinessException("Purchase request not found.");
                }

                if (request.ApprovalStatus != ApprovalStatus.Approved)
                {
                    throw new BusinessException("Only approved purchase requests can be linked to a purchase record.");
                }

                var alreadyLinked = _unitOfWork.Repository<PurchaseRecord>().Find(x => x.PurchaseRequestId == model.PurchaseRequestId.Value).Any();
                if (alreadyLinked)
                {
                    throw new BusinessException("A purchase record is already linked to this request.");
                }
            }

            var entity = new PurchaseRecord
            {
                PurchaseRequestId = model.PurchaseRequestId,
                PurchaseOrderNumber = model.PurchaseOrderNumber,
                SupplierId = model.SupplierId,
                InvoiceNumber = model.InvoiceNumber,
                PurchaseDate = model.PurchaseDate,
                Quantity = model.Quantity,
                UnitCost = model.UnitCost,
                TotalCost = model.TotalCost <= 0 ? model.UnitCost * model.Quantity : model.TotalCost,
                Currency = string.IsNullOrWhiteSpace(model.Currency)
                    ? ApprovalWorkflowSettingsHelper.GetDefaultCurrencyCode(_unitOfWork.Repository<SystemSetting>().GetAll())
                    : model.Currency.Trim().ToUpperInvariant(),
                CreatedAt = DateTime.UtcNow
            };

            _unitOfWork.Repository<PurchaseRecord>().Add(entity);
            _unitOfWork.SaveChanges();
            return entity.Id;
        }
    }
}

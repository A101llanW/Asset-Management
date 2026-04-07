using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services
{
    public class PurchaseService : IPurchaseService
    {
        private readonly IUnitOfWork _unitOfWork;

        public PurchaseService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IEnumerable<PurchaseRecordVm> GetAll()
        {
            var suppliers = _unitOfWork.Repository<Supplier>().GetAll()
                .ToDictionary(x => x.Id, x => x.SupplierName);

            return _unitOfWork.Repository<PurchaseRecord>().GetAll()
                .OrderByDescending(x => x.PurchaseDate)
                .Select(x => new PurchaseRecordVm
                {
                    Id = x.Id,
                    PurchaseOrderNumber = x.PurchaseOrderNumber,
                    SupplierId = x.SupplierId,
                    SupplierName = suppliers.ContainsKey(x.SupplierId) ? suppliers[x.SupplierId] : null,
                    InvoiceNumber = x.InvoiceNumber,
                    PurchaseDate = x.PurchaseDate,
                    Quantity = x.Quantity,
                    UnitCost = x.UnitCost,
                    TotalCost = x.TotalCost,
                    Currency = x.Currency
                })
                .ToList();
        }

        public void Create(PurchaseRecordVm model)
        {
            _unitOfWork.Repository<PurchaseRecord>().Add(new PurchaseRecord
            {
                PurchaseOrderNumber = model.PurchaseOrderNumber,
                SupplierId = model.SupplierId,
                InvoiceNumber = model.InvoiceNumber,
                PurchaseDate = model.PurchaseDate,
                Quantity = model.Quantity,
                UnitCost = model.UnitCost,
                TotalCost = model.TotalCost <= 0 ? model.UnitCost * model.Quantity : model.TotalCost,
                Currency = model.Currency,
                CreatedAt = DateTime.UtcNow
            });

            _unitOfWork.SaveChanges();
        }
    }
}

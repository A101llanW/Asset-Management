using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services
{
    public class SupplierService : ISupplierService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISupplierCatalogService _supplierCatalogService;

        public SupplierService(IUnitOfWork unitOfWork, ISupplierCatalogService supplierCatalogService)
        {
            _unitOfWork = unitOfWork;
            _supplierCatalogService = supplierCatalogService;
        }

        public IEnumerable<SupplierVm> GetAll()
        {
            var catalogStats = BuildCatalogStats();
            return _unitOfWork.Repository<Supplier>().GetAll()
                .OrderBy(x => x.SupplierName)
                .Select(x => MapSupplier(x, catalogStats))
                .ToList();
        }

        public SupplierVm GetById(int id)
        {
            var entity = _unitOfWork.Repository<Supplier>().GetById(id);
            if (entity == null)
            {
                return null;
            }

            var catalogStats = BuildCatalogStats();
            return MapSupplier(entity, catalogStats);
        }

        public int Create(SupplierVm model)
        {
            var entity = MapToEntity(model);
            entity.CreatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Supplier>().Add(entity);
            _unitOfWork.SaveChanges();
            return entity.Id;
        }

        public int CreateWithCatalog(SupplierVm model, IEnumerable<SupplierCatalogItemVm> catalogItems)
        {
            if (model == null)
            {
                throw new BusinessException("Supplier details are required.");
            }

            var items = NormalizeCatalogItems(catalogItems);
            if (items.Count == 0)
            {
                throw new BusinessException("Add at least one supply item with an item name and unit price for purchase-order comparison.");
            }

            int supplierId = 0;
            _unitOfWork.ExecuteInTransaction(() =>
            {
                var entity = MapToEntity(model);
                entity.CreatedAt = DateTime.UtcNow;
                entity.IsActive = model.IsActive;
                _unitOfWork.Repository<Supplier>().Add(entity);
                _unitOfWork.SaveChanges();
                supplierId = entity.Id;
                _supplierCatalogService.AddItemsForSupplier(supplierId, items);
                _unitOfWork.SaveChanges();
            });

            return supplierId;
        }

        public void Update(SupplierVm model)
        {
            var entity = _unitOfWork.Repository<Supplier>().GetById(model.Id);
            if (entity == null)
            {
                return;
            }

            ApplyModel(entity, model);
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Supplier>().Update(entity);
            _unitOfWork.SaveChanges();
        }

        private static List<SupplierCatalogItemVm> NormalizeCatalogItems(IEnumerable<SupplierCatalogItemVm> catalogItems)
        {
            if (catalogItems == null)
            {
                return new List<SupplierCatalogItemVm>();
            }

            return catalogItems
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.ItemName))
                .ToList();
        }

        private static Supplier MapToEntity(SupplierVm model)
        {
            var entity = new Supplier();
            ApplyModel(entity, model);
            return entity;
        }

        private static void ApplyModel(Supplier entity, SupplierVm model)
        {
            entity.SupplierName = model.SupplierName;
            entity.ContactPerson = model.ContactPerson;
            entity.Email = model.Email;
            entity.Phone = model.Phone;
            entity.Address = model.Address;
            entity.RegistrationNumber = model.RegistrationNumber;
            entity.TaxId = model.TaxId;
            entity.PaymentTerms = model.PaymentTerms;
            entity.DefaultLeadTimeDays = model.DefaultLeadTimeDays;
            entity.Website = model.Website;
            entity.IsPreferred = model.IsPreferred;
            entity.Country = model.Country;
            entity.PaymentInstructions = model.PaymentInstructions;
            entity.Notes = model.Notes;
            entity.IsActive = model.IsActive;
        }

        private static SupplierVm MapSupplier(Supplier entity, IDictionary<int, CatalogStats> catalogStats)
        {
            CatalogStats stats;
            catalogStats.TryGetValue(entity.Id, out stats);
            return new SupplierVm
            {
                Id = entity.Id,
                SupplierName = entity.SupplierName,
                ContactPerson = entity.ContactPerson,
                Email = entity.Email,
                Phone = entity.Phone,
                Address = entity.Address,
                RegistrationNumber = entity.RegistrationNumber,
                TaxId = entity.TaxId,
                PaymentTerms = entity.PaymentTerms,
                DefaultLeadTimeDays = entity.DefaultLeadTimeDays,
                Website = entity.Website,
                IsPreferred = entity.IsPreferred,
                Country = entity.Country,
                PaymentInstructions = entity.PaymentInstructions,
                Notes = entity.Notes,
                IsActive = entity.IsActive,
                CatalogItemCount = stats == null ? 0 : stats.Count,
                CatalogMinPrice = stats == null ? (decimal?)null : stats.MinPrice,
                CatalogMaxPrice = stats == null ? (decimal?)null : stats.MaxPrice
            };
        }

        private IDictionary<int, CatalogStats> BuildCatalogStats()
        {
            return _unitOfWork.Repository<SupplierCatalogItem>().GetAll()
                .Where(x => x.IsActive)
                .GroupBy(x => x.SupplierId)
                .ToDictionary(
                    g => g.Key,
                    g => new CatalogStats
                    {
                        Count = g.Count(),
                        MinPrice = g.Min(x => x.UnitPrice),
                        MaxPrice = g.Max(x => x.UnitPrice)
                    });
        }

        private sealed class CatalogStats
        {
            public int Count { get; set; }

            public decimal MinPrice { get; set; }

            public decimal MaxPrice { get; set; }
        }
    }
}

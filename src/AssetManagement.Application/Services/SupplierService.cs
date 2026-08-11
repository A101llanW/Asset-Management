using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services
{
    public class SupplierService : ISupplierService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISupplierCatalogService _supplierCatalogService;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly ICatalogQueryRepository _catalogQueryRepository;
        private readonly IAuditWriter _auditWriter;

        public SupplierService(
            IUnitOfWork unitOfWork,
            ISupplierCatalogService supplierCatalogService,
            IOrganizationScopeService organizationScope = null,
            ICatalogQueryRepository catalogQueryRepository = null,
            IAuditWriter auditWriter = null)
        {
            _unitOfWork = unitOfWork;
            _supplierCatalogService = supplierCatalogService;
            _organizationScope = organizationScope;
            _catalogQueryRepository = catalogQueryRepository;
            _auditWriter = auditWriter;
        }

        public IEnumerable<SupplierVm> GetAll()
        {
            var catalogStats = BuildCatalogStats();
            return _unitOfWork.Repository<Supplier>().GetAll()
                .OrderBy(x => x.SupplierName)
                .Select(x => MapSupplier(x, catalogStats))
                .ToList();
        }

        public PagedListVm<SupplierVm> GetListPage(
            string search,
            string sort,
            string direction,
            int page,
            int pageSize)
        {
            var organizationId = _organizationScope?.GetCurrentOrganizationId();
            if (!organizationId.HasValue || _catalogQueryRepository == null)
            {
                return PaginateSuppliersInMemory(GetAll(), search, sort, direction, page, pageSize);
            }

            return _catalogQueryRepository.GetSupplierListPage(
                organizationId.Value,
                search,
                sort,
                direction,
                page,
                pageSize);
        }

        private static PagedListVm<SupplierVm> PaginateSuppliersInMemory(
            IEnumerable<SupplierVm> source,
            string search,
            string sort,
            string direction,
            int page,
            int pageSize)
        {
            var items = source;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                items = items.Where(x => (x.SupplierName ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.ContactPerson ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.Email ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.Phone ?? string.Empty).ToLowerInvariant().Contains(term));
            }

            var safePageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);
            var materialized = items.ToList();
            var totalCount = materialized.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / safePageSize));
            var safePage = Math.Min(Math.Max(page, 1), totalPages);
            return new PagedListVm<SupplierVm>
            {
                Items = materialized.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToList(),
                TotalCount = totalCount,
                Search = search,
                Sort = sort,
                Direction = direction,
                Page = safePage,
                PageSize = safePageSize
            };
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
            _auditWriter?.Write("Suppliers.Create", nameof(Supplier), entity.Id.ToString(), null, entity.SupplierName);
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

            _auditWriter?.Write("Suppliers.Create", nameof(Supplier), supplierId.ToString(), null, model.SupplierName);
            return supplierId;
        }

        public void Update(SupplierVm model)
        {
            var entity = _unitOfWork.Repository<Supplier>().GetById(model.Id);
            if (entity == null)
            {
                return;
            }

            var previousName = entity.SupplierName;
            ApplyModel(entity, model);
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Supplier>().Update(entity);
            _unitOfWork.SaveChanges();
            _auditWriter?.Write("Suppliers.Edit", nameof(Supplier), entity.Id.ToString(), previousName, entity.SupplierName);
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

using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services
{
    public class SupplierService : ISupplierService
    {
        private readonly IUnitOfWork _unitOfWork;

        public SupplierService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IEnumerable<SupplierVm> GetAll()
        {
            return _unitOfWork.Repository<Supplier>().GetAll()
                .OrderBy(x => x.SupplierName)
                .Select(x => new SupplierVm
                {
                    Id = x.Id,
                    SupplierName = x.SupplierName,
                    ContactPerson = x.ContactPerson,
                    Email = x.Email,
                    Phone = x.Phone,
                    Address = x.Address,
                    RegistrationNumber = x.RegistrationNumber,
                    Notes = x.Notes,
                    IsActive = x.IsActive
                })
                .ToList();
        }

        public SupplierVm GetById(int id)
        {
            var entity = _unitOfWork.Repository<Supplier>().GetById(id);
            if (entity == null)
            {
                return null;
            }

            return new SupplierVm
            {
                Id = entity.Id,
                SupplierName = entity.SupplierName,
                ContactPerson = entity.ContactPerson,
                Email = entity.Email,
                Phone = entity.Phone,
                Address = entity.Address,
                RegistrationNumber = entity.RegistrationNumber,
                Notes = entity.Notes,
                IsActive = entity.IsActive
            };
        }

        public void Create(SupplierVm model)
        {
            _unitOfWork.Repository<Supplier>().Add(new Supplier
            {
                SupplierName = model.SupplierName,
                ContactPerson = model.ContactPerson,
                Email = model.Email,
                Phone = model.Phone,
                Address = model.Address,
                RegistrationNumber = model.RegistrationNumber,
                Notes = model.Notes,
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow
            });

            _unitOfWork.SaveChanges();
        }

        public void Update(SupplierVm model)
        {
            var entity = _unitOfWork.Repository<Supplier>().GetById(model.Id);
            if (entity == null)
            {
                return;
            }

            entity.SupplierName = model.SupplierName;
            entity.ContactPerson = model.ContactPerson;
            entity.Email = model.Email;
            entity.Phone = model.Phone;
            entity.Address = model.Address;
            entity.RegistrationNumber = model.RegistrationNumber;
            entity.Notes = model.Notes;
            entity.IsActive = model.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Repository<Supplier>().Update(entity);
            _unitOfWork.SaveChanges();
        }
    }
}

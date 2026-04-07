using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services
{
    public class DepartmentService : IDepartmentService
    {
        private readonly IUnitOfWork _unitOfWork;

        public DepartmentService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IEnumerable<DepartmentVm> GetAll()
        {
            return _unitOfWork.Repository<Department>().GetAll()
                .OrderBy(x => x.Name)
                .Select(x => new DepartmentVm
                {
                    Id = x.Id,
                    Name = x.Name,
                    Code = x.Code,
                    Description = x.Description,
                    IsActive = x.IsActive
                })
                .ToList();
        }

        public DepartmentVm GetById(int id)
        {
            var entity = _unitOfWork.Repository<Department>().GetById(id);
            if (entity == null)
            {
                return null;
            }

            return new DepartmentVm
            {
                Id = entity.Id,
                Name = entity.Name,
                Code = entity.Code,
                Description = entity.Description,
                IsActive = entity.IsActive
            };
        }

        public void Create(DepartmentVm model)
        {
            var entity = new Department
            {
                Name = model.Name,
                Code = model.Code,
                Description = model.Description,
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _unitOfWork.Repository<Department>().Add(entity);
            _unitOfWork.SaveChanges();
        }

        public void Update(DepartmentVm model)
        {
            var entity = _unitOfWork.Repository<Department>().GetById(model.Id);
            if (entity == null)
            {
                return;
            }

            entity.Name = model.Name;
            entity.Code = model.Code;
            entity.Description = model.Description;
            entity.IsActive = model.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Repository<Department>().Update(entity);
            _unitOfWork.SaveChanges();
        }
    }
}

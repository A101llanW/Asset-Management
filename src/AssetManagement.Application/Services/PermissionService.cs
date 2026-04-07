using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly IUnitOfWork _unitOfWork;

        public PermissionService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IEnumerable<PermissionVm> GetAll()
        {
            return _unitOfWork.Repository<Permission>().GetAll()
                .OrderBy(x => x.Module)
                .ThenBy(x => x.Name)
                .Select(x => new PermissionVm
                {
                    Id = x.Id,
                    Name = x.Name,
                    Code = x.Code,
                    Module = x.Module,
                    Description = x.Description
                })
                .ToList();
        }

        public IEnumerable<PermissionGroupVm> GetGroupedPermissions()
        {
            return GetAll().GroupBy(x => x.Module)
                .Select(g => new PermissionGroupVm
                {
                    Module = g.Key,
                    Permissions = g.ToList()
                })
                .OrderBy(x => x.Module)
                .ToList();
        }
    }
}

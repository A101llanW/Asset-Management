using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IDepartmentService
    {
        IEnumerable<DepartmentVm> GetAll();

        DepartmentVm GetById(int id);

        void Create(DepartmentVm model);

        void Update(DepartmentVm model);
    }
}

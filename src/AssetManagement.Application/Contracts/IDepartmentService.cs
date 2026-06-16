using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IDepartmentService
    {
        IEnumerable<DepartmentVm> GetAll();

        DepartmentVm GetById(int id);

        int Create(DepartmentVm model);

        void Update(DepartmentVm model);
    }
}

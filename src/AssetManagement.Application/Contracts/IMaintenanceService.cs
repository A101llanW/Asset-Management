using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IMaintenanceService
    {
        void Create(AssetMaintenanceVm model);

        MaintenanceDetailsVm GetById(int id);

        MaintenanceCompleteVm GetCompleteModel(int id);

        void Complete(MaintenanceCompleteVm model);

        IEnumerable<MaintenanceRecordListVm> GetByAsset(int assetId);

        IEnumerable<MaintenanceRecordListVm> GetMaintenanceRecords(string search, int? assetId);
    }
}

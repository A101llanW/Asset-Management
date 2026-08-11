using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IIncidentService
    {
        int Create(AssetIncidentVm model);

        IEnumerable<IncidentListVm> GetIncidents(string search, int? assetId);

        PagedListVm<IncidentListVm> GetListPage(string search, int? assetId, int page, int pageSize);

        IncidentDetailsVm GetById(int id);

        void UpdateResolutionStatus(int id, string resolutionStatus);
    }
}

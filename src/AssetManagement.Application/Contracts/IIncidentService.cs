using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IIncidentService
    {
        void Create(AssetIncidentVm model);

        IEnumerable<IncidentListVm> GetIncidents(string search, int? assetId);

        IncidentDetailsVm GetById(int id);

        void UpdateResolutionStatus(int id, string resolutionStatus);
    }
}

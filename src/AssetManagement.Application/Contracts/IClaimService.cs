using System.Collections.Generic;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Contracts
{
    public interface IClaimService
    {
        void Create(InsuranceClaimVm model);

        IEnumerable<ClaimListVm> GetClaims(string search, int? assetId);

        PagedListVm<ClaimListVm> GetListPage(string search, int? assetId, int page, int pageSize);

        ClaimDetailsVm GetById(int id);

        void UpdateStatus(int id, ClaimStatus status, decimal? approvedAmount, string settlementNotes);
    }
}

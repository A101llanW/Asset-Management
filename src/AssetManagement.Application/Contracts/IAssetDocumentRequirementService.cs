using System.Collections.Generic;
using System.IO;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IAssetDocumentRequirementService
    {
        int CreateIncidentPhotoRequirement(int assetId, int incidentId, string incidentNumber);

        IEnumerable<AssetDocumentStatusRowVm> GetStatusRowsByAsset(int assetId);

        AssetDocumentRequirementVm GetPendingIncidentPhotoRequirement(int incidentId);

        void FulfillRequirement(int requirementId, int documentId);

        void ClearRequirementOnDocumentDelete(int documentId);
    }
}

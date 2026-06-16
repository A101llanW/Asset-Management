using System.Collections.Generic;
using System.IO;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IAssetDocumentService
    {
        IEnumerable<AssetDocumentVm> GetByAsset(int assetId);

        AssetDocumentVm GetById(int id);

        int Upload(int assetId, string documentType, string fileName, string contentType, Stream content, string uploadedByUserId);

        void Delete(int documentId, string deletedByUserId);

        string GetStoredRelativePath(int documentId, string userId);
    }
}

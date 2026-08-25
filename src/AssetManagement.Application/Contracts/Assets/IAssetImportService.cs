using System.IO;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IAssetImportService
    {
        byte[] GetImportTemplate();

        AssetImportResultVm Import(Stream content, string fileName, string actorUserId);
    }
}

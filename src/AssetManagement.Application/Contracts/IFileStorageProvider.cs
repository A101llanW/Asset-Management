using System.IO;

namespace AssetManagement.Application.Contracts
{
    public interface IFileStorageProvider
    {
        string Save(Stream stream, string fileName, string contentType, string folder);

        void Delete(string relativePath);

        string GetFullPath(string relativePath);
    }
}

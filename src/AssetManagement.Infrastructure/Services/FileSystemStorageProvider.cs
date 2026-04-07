using System;
using System.IO;
using AssetManagement.Application.Contracts;

namespace AssetManagement.Infrastructure.Services
{
    public class FileSystemStorageProvider : IFileStorageProvider
    {
        private readonly string _rootPath;

        public FileSystemStorageProvider(string rootPath)
        {
            _rootPath = rootPath;
        }

        public string Save(Stream stream, string fileName, string contentType, string folder)
        {
            var directory = Path.Combine(_rootPath, folder ?? string.Empty);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var safeFileName = Path.GetFileNameWithoutExtension(fileName) + "_" + DateTime.UtcNow.Ticks + Path.GetExtension(fileName);
            var fullPath = Path.Combine(directory, safeFileName);
            using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
            {
                stream.CopyTo(fileStream);
            }

            return Path.Combine(folder ?? string.Empty, safeFileName).Replace("\\", "/");
        }

        public void Delete(string relativePath)
        {
            var fullPath = Path.Combine(_rootPath, relativePath ?? string.Empty);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
    }
}

using System;
using System.IO;
using AssetManagement.Application.Contracts;

namespace AssetManagement.Infrastructure.Services
{
    public class FileSystemStorageProvider : IFileStorageProvider
    {
        private readonly string _rootPath;
        private readonly string _canonicalRootPath;

        public FileSystemStorageProvider(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentException("Storage root path is required.", "rootPath");
            }

            _rootPath = rootPath;
            _canonicalRootPath = GetCanonicalRootPath(rootPath);
        }

        public string Save(Stream stream, string fileName, string contentType, string folder)
        {
            var safeFileName = Path.GetFileName(fileName);
            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                throw new ArgumentException("File name is required.", "fileName");
            }

            var relativePath = Path.Combine(folder ?? string.Empty, safeFileName).Replace("\\", "/");
            var fullPath = GetFullPath(relativePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write))
            {
                stream.CopyTo(fileStream);
            }

            return relativePath;
        }

        public void Delete(string relativePath)
        {
            var fullPath = GetFullPath(relativePath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        public string GetFullPath(string relativePath)
        {
            var combined = Path.Combine(_rootPath, (relativePath ?? string.Empty).Replace("/", "\\"));
            var fullPath = Path.GetFullPath(combined);
            EnsureWithinRoot(fullPath);
            return fullPath;
        }

        private static string GetCanonicalRootPath(string rootPath)
        {
            var fullRoot = Path.GetFullPath(rootPath);
            if (!fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                fullRoot += Path.DirectorySeparatorChar;
            }

            return fullRoot;
        }

        private void EnsureWithinRoot(string fullPath)
        {
            var canonicalPath = Path.GetFullPath(fullPath);
            if (!canonicalPath.StartsWith(_canonicalRootPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Resolved file path is outside the configured storage root.");
            }
        }
    }
}

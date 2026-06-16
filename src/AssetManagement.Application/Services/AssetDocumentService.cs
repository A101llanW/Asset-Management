using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services
{
    public class AssetDocumentService : IAssetDocumentService
    {
        private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".doc", ".docx", ".xls", ".xlsx", ".txt" };
        private const long MaxFileSizeBytes = 10 * 1024 * 1024;

        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageProvider _storage;
        private readonly IUserService _userService;
        private readonly IAuditWriter _auditWriter;
        private readonly IDepartmentScopeService _departmentScope;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICurrentUserContext _currentUserContext;

        public AssetDocumentService(
            IUnitOfWork unitOfWork,
            IFileStorageProvider storage,
            IUserService userService,
            IAuditWriter auditWriter,
            IDepartmentScopeService departmentScope,
            IAuthorizationService authorizationService,
            ICurrentUserContext currentUserContext)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
            _userService = userService;
            _auditWriter = auditWriter;
            _departmentScope = departmentScope;
            _authorizationService = authorizationService;
            _currentUserContext = currentUserContext;
        }

        public IEnumerable<AssetDocumentVm> GetByAsset(int assetId)
        {
            var asset = _unitOfWork.Repository<Asset>().GetById(assetId);
            if (asset == null)
            {
                return Enumerable.Empty<AssetDocumentVm>();
            }

            EnsureCanAccessParentAsset(assetId, ResolveUserId(null));

            return _unitOfWork.Repository<AssetDocument>()
                .Find(x => x.AssetId == assetId && x.IsActive)
                .OrderByDescending(x => x.UploadedAt)
                .Select(MapDocument)
                .ToList();
        }

        public AssetDocumentVm GetById(int id)
        {
            var entity = _unitOfWork.Repository<AssetDocument>().GetById(id);
            if (entity == null || !entity.IsActive)
            {
                return null;
            }

            EnsureCanAccessParentAsset(entity.AssetId, ResolveUserId(null));
            return MapDocument(entity);
        }

        public int Upload(int assetId, string documentType, string fileName, string contentType, Stream content, string uploadedByUserId)
        {
            if (content == null)
            {
                throw new BusinessException("File content is required.");
            }

            if (string.IsNullOrWhiteSpace(uploadedByUserId))
            {
                throw new BusinessException("Uploader identity is required.");
            }

            var asset = EnsureCanAccessParentAsset(assetId, uploadedByUserId);
            EnsureCanUpload(asset, uploadedByUserId);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new BusinessException("File name is required.");
            }

            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension.ToLowerInvariant()))
            {
                throw new BusinessException("File type is not allowed.");
            }

            if (content.Length > MaxFileSizeBytes)
            {
                throw new BusinessException("File exceeds the maximum allowed size of 10 MB.");
            }

            var fileSizeBytes = content.Length;
            ValidateFileSignature(content, extension);

            var safeType = string.IsNullOrWhiteSpace(documentType) ? "General" : documentType.Trim();
            var folder = "assets/" + assetId;
            var storedFileName = Guid.NewGuid().ToString("N") + extension.ToLowerInvariant();
            var relativePath = _storage.Save(content, storedFileName, contentType, folder);
            var now = DateTime.UtcNow;

            var document = new AssetDocument
            {
                AssetId = assetId,
                DocumentType = safeType,
                FileName = Path.GetFileName(fileName),
                FilePath = relativePath,
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                FileSizeBytes = fileSizeBytes,
                UploadedById = uploadedByUserId,
                UploadedAt = now,
                CreatedAt = now,
                IsActive = true
            };

            _unitOfWork.Repository<AssetDocument>().Add(document);
            _unitOfWork.SaveChanges();
            _auditWriter?.Write("Documents.Upload", nameof(AssetDocument), document.Id.ToString(), null, asset.Id.ToString());
            return document.Id;
        }

        public void Delete(int documentId, string deletedByUserId)
        {
            var entity = _unitOfWork.Repository<AssetDocument>().GetById(documentId);
            if (entity == null || !entity.IsActive)
            {
                throw new BusinessException("Document not found.");
            }

            EnsureCanAccessParentAsset(entity.AssetId, ResolveUserId(deletedByUserId));

            entity.IsActive = false;
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<AssetDocument>().Update(entity);
            _storage.Delete(entity.FilePath);
            _unitOfWork.SaveChanges();
            _auditWriter?.Write("Documents.Delete", nameof(AssetDocument), entity.Id.ToString(), "Active", "Deleted");
        }

        public string GetStoredRelativePath(int documentId, string userId)
        {
            var entity = _unitOfWork.Repository<AssetDocument>().GetById(documentId);
            if (entity == null || !entity.IsActive)
            {
                throw new BusinessException("Document not found.");
            }

            var asset = EnsureCanAccessParentAsset(entity.AssetId, ResolveUserId(userId));
            EnsureCanDownload(asset, ResolveUserId(userId));
            return entity.FilePath;
        }

        private static void ValidateFileSignature(Stream content, string extension)
        {
            var normalizedExtension = extension.ToLowerInvariant();
            if (normalizedExtension == ".txt")
            {
                return;
            }

            var signatureLength = GetRequiredSignatureLength(normalizedExtension);
            if (signatureLength <= 0)
            {
                throw new BusinessException("File type is not allowed.");
            }

            if (!content.CanSeek)
            {
                throw new BusinessException("Unable to validate file content.");
            }

            var originalPosition = content.Position;
            var header = new byte[signatureLength];
            var bytesRead = content.Read(header, 0, header.Length);
            content.Position = originalPosition;

            if (!MatchesSignature(header, bytesRead, normalizedExtension))
            {
                throw new BusinessException("File content does not match the declared file type.");
            }
        }

        private static int GetRequiredSignatureLength(string extension)
        {
            switch (extension)
            {
                case ".pdf":
                    return 4;
                case ".jpg":
                case ".jpeg":
                    return 3;
                case ".png":
                    return 8;
                case ".gif":
                    return 6;
                case ".doc":
                case ".xls":
                    return 8;
                case ".docx":
                case ".xlsx":
                    return 4;
                default:
                    return 0;
            }
        }

        private static bool MatchesSignature(byte[] header, int bytesRead, string extension)
        {
            if (header == null || bytesRead < GetRequiredSignatureLength(extension))
            {
                return false;
            }

            switch (extension)
            {
                case ".pdf":
                    return header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46;
                case ".jpg":
                case ".jpeg":
                    return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
                case ".png":
                    return header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                        && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
                case ".gif":
                    return header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38
                        && (header[4] == 0x37 || header[4] == 0x39) && header[5] == 0x61;
                case ".doc":
                case ".xls":
                    return header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0
                        && header[4] == 0xA1 && header[5] == 0xB1 && header[6] == 0x1A && header[7] == 0xE1;
                case ".docx":
                case ".xlsx":
                    return header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;
                default:
                    return false;
            }
        }

        private Asset EnsureCanAccessParentAsset(int assetId, string userId)
        {
            var asset = _unitOfWork.Repository<Asset>().GetById(assetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            if (AssetDocumentAccessRules.IsCurrentCustodian(asset, userId))
            {
                return asset;
            }

            _departmentScope.EnsureCanAccessAsset(asset);
            return asset;
        }

        private void EnsureCanUpload(Asset asset, string userId)
        {
            if (_authorizationService != null && _authorizationService.HasPermission(userId, "Documents.Upload"))
            {
                return;
            }

            if (AssetDocumentAccessRules.IsCurrentCustodian(asset, userId))
            {
                return;
            }

            throw new BusinessException("You do not have permission to upload documents for this asset.");
        }

        private void EnsureCanDownload(Asset asset, string userId)
        {
            if (_authorizationService != null && _authorizationService.HasPermission(userId, "Documents.Download"))
            {
                return;
            }

            if (AssetDocumentAccessRules.IsCurrentCustodian(asset, userId))
            {
                return;
            }

            throw new BusinessException("You do not have permission to download this document.");
        }

        private string ResolveUserId(string explicitUserId)
        {
            if (!string.IsNullOrWhiteSpace(explicitUserId))
            {
                return explicitUserId;
            }

            return _currentUserContext == null ? null : _currentUserContext.UserId;
        }

        private AssetDocumentVm MapDocument(AssetDocument entity)
        {
            var uploader = string.IsNullOrWhiteSpace(entity.UploadedById)
                ? null
                : _userService.GetById(entity.UploadedById);
            var uploaderName = uploader == null
                ? entity.UploadedById
                : ((uploader.FirstName + " " + uploader.LastName).Trim());
            if (string.IsNullOrWhiteSpace(uploaderName))
            {
                uploaderName = uploader == null ? entity.UploadedById : uploader.Email;
            }

            return new AssetDocumentVm
            {
                Id = entity.Id,
                AssetId = entity.AssetId,
                DocumentType = entity.DocumentType,
                FileName = entity.FileName,
                ContentType = entity.ContentType,
                FileSizeBytes = entity.FileSizeBytes,
                UploadedByName = uploaderName,
                UploadedAt = entity.UploadedAt
            };
        }
    }
}

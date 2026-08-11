using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services
{
    public class AssetDocumentRequirementService : IAssetDocumentRequirementService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUserService _userService;
        private readonly IDepartmentScopeService _departmentScope;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly IAuditWriter _auditWriter;

        public AssetDocumentRequirementService(
            IUnitOfWork unitOfWork,
            IUserService userService,
            IDepartmentScopeService departmentScope,
            ICurrentUserContext currentUserContext,
            IAuditWriter auditWriter = null)
        {
            _unitOfWork = unitOfWork;
            _userService = userService;
            _departmentScope = departmentScope;
            _currentUserContext = currentUserContext;
            _auditWriter = auditWriter;
        }

        public int CreateIncidentPhotoRequirement(int assetId, int incidentId, string incidentNumber)
        {
            EnsureCanAccessAsset(assetId);

            var existing = _unitOfWork.Repository<AssetDocumentRequirement>()
                .Find(x => x.AssetId == assetId
                           && x.ProcessType == AssetDocumentProcessCodes.Incident
                           && x.ProcessId == incidentId
                           && x.IsActive)
                .FirstOrDefault();
            if (existing != null)
            {
                return existing.Id;
            }

            var now = DateTime.UtcNow;
            var requirement = new AssetDocumentRequirement
            {
                AssetId = assetId,
                ProcessType = AssetDocumentProcessCodes.Incident,
                ProcessId = incidentId,
                DocumentType = AssetDocumentProcessCodes.IncidentDamagePhotoType,
                Label = AssetDocumentProcessHelper.BuildIncidentProcessReference(incidentNumber),
                CreatedAt = now,
                IsActive = true
            };

            _unitOfWork.Repository<AssetDocumentRequirement>().Add(requirement);
            _unitOfWork.SaveChanges();
            _auditWriter?.Write(
                "Documents.Requirement.Create",
                nameof(AssetDocumentRequirement),
                requirement.Id.ToString(),
                null,
                assetId.ToString());
            return requirement.Id;
        }

        public IEnumerable<AssetDocumentStatusRowVm> GetStatusRowsByAsset(int assetId)
        {
            EnsureCanAccessAsset(assetId);

            var rows = new List<AssetDocumentStatusRowVm>();
            var requirements = _unitOfWork.Repository<AssetDocumentRequirement>()
                .Find(x => x.AssetId == assetId && x.IsActive)
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            foreach (var requirement in requirements)
            {
                AssetDocument document = null;
                if (requirement.DocumentId.HasValue)
                {
                    document = _unitOfWork.Repository<AssetDocument>().GetById(requirement.DocumentId.Value);
                    if (document == null || !document.IsActive)
                    {
                        document = null;
                    }
                }

                rows.Add(new AssetDocumentStatusRowVm
                {
                    RequirementId = requirement.Id,
                    DocumentId = document?.Id,
                    ProcessType = requirement.ProcessType,
                    ProcessId = requirement.ProcessId,
                    ProcessReference = requirement.Label,
                    DocumentType = requirement.DocumentType,
                    IsPending = document == null,
                    FileName = document?.FileName,
                    FileSizeBytes = document?.FileSizeBytes,
                    UploadedByName = document == null ? null : ResolveUploaderName(document.UploadedById),
                    UploadedAt = document?.UploadedAt
                });
            }

            var linkedDocumentIds = new HashSet<int>(
                requirements.Where(x => x.DocumentId.HasValue).Select(x => x.DocumentId.Value));

            var generalDocuments = _unitOfWork.Repository<AssetDocument>()
                .Find(x => x.AssetId == assetId && x.IsActive && !x.RequirementId.HasValue)
                .Where(x => !linkedDocumentIds.Contains(x.Id))
                .OrderByDescending(x => x.UploadedAt)
                .ToList();

            foreach (var document in generalDocuments)
            {
                rows.Add(new AssetDocumentStatusRowVm
                {
                    DocumentId = document.Id,
                    ProcessReference = "General upload",
                    DocumentType = document.DocumentType,
                    IsPending = false,
                    FileName = document.FileName,
                    FileSizeBytes = document.FileSizeBytes,
                    UploadedByName = ResolveUploaderName(document.UploadedById),
                    UploadedAt = document.UploadedAt
                });
            }

            return rows;
        }

        public AssetDocumentRequirementVm GetPendingIncidentPhotoRequirement(int incidentId)
        {
            var requirement = _unitOfWork.Repository<AssetDocumentRequirement>()
                .Find(x => x.ProcessType == AssetDocumentProcessCodes.Incident
                           && x.ProcessId == incidentId
                           && x.IsActive
                           && !x.DocumentId.HasValue)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();
            if (requirement == null)
            {
                return null;
            }

            EnsureCanAccessAsset(requirement.AssetId);
            return MapRequirement(requirement);
        }

        public void FulfillRequirement(int requirementId, int documentId)
        {
            var requirement = _unitOfWork.Repository<AssetDocumentRequirement>().GetById(requirementId);
            if (requirement == null || !requirement.IsActive)
            {
                throw new BusinessException("Document requirement not found.");
            }

            var document = _unitOfWork.Repository<AssetDocument>().GetById(documentId);
            if (document == null || !document.IsActive || document.AssetId != requirement.AssetId)
            {
                throw new BusinessException("Uploaded document does not match this requirement.");
            }

            var now = DateTime.UtcNow;
            requirement.DocumentId = documentId;
            requirement.FulfilledAt = now;
            requirement.UpdatedAt = now;
            _unitOfWork.Repository<AssetDocumentRequirement>().Update(requirement);
            _unitOfWork.SaveChanges();
            _auditWriter?.Write(
                "Documents.Requirement.Fulfill",
                nameof(AssetDocumentRequirement),
                requirement.Id.ToString(),
                null,
                requirement.AssetId.ToString());
        }

        public void ClearRequirementOnDocumentDelete(int documentId)
        {
            var requirements = _unitOfWork.Repository<AssetDocumentRequirement>()
                .Find(x => x.DocumentId == documentId && x.IsActive)
                .ToList();
            if (requirements.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var requirement in requirements)
            {
                requirement.DocumentId = null;
                requirement.FulfilledAt = null;
                requirement.UpdatedAt = now;
                _unitOfWork.Repository<AssetDocumentRequirement>().Update(requirement);
            }

            _unitOfWork.SaveChanges();
            foreach (var requirement in requirements)
            {
                _auditWriter?.Write(
                    "Documents.Requirement.Clear",
                    nameof(AssetDocumentRequirement),
                    requirement.Id.ToString(),
                    documentId.ToString(),
                    requirement.AssetId.ToString());
            }
        }

        private AssetDocumentRequirementVm MapRequirement(AssetDocumentRequirement requirement)
        {
            return new AssetDocumentRequirementVm
            {
                Id = requirement.Id,
                AssetId = requirement.AssetId,
                ProcessType = requirement.ProcessType,
                ProcessId = requirement.ProcessId,
                DocumentType = requirement.DocumentType,
                Label = requirement.Label,
                DocumentId = requirement.DocumentId,
                IsPending = !requirement.DocumentId.HasValue
            };
        }

        private void EnsureCanAccessAsset(int assetId)
        {
            var asset = _unitOfWork.Repository<Asset>().GetById(assetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            var userId = _currentUserContext == null ? null : _currentUserContext.UserId;
            if (AssetDocumentAccessRules.IsCurrentCustodian(asset, userId))
            {
                return;
            }

            _departmentScope.EnsureCanAccessAsset(asset);
        }

        private string ResolveUploaderName(string uploadedById)
        {
            if (string.IsNullOrWhiteSpace(uploadedById))
            {
                return null;
            }

            var uploader = _userService.GetById(uploadedById);
            if (uploader == null)
            {
                return uploadedById;
            }

            var name = ((uploader.FirstName ?? string.Empty) + " " + (uploader.LastName ?? string.Empty)).Trim();
            return string.IsNullOrWhiteSpace(name) ? uploader.Email : name;
        }
    }
}

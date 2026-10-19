using System;
using System.Linq;
using System.Text.RegularExpressions;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services
{
    /// <summary>
    /// rel1019g: inline "new asset type + its first sub-type" from the asset classification picker (Assets/Create, Edit).
    /// Mirrors AssetSubTypeService.CreateFromAsset; categories stay select-only.
    /// </summary>
    public class AssetClassificationCreateService : IAssetClassificationCreateService
    {
        public const int MaxNameLength = 200;
        public const int MaxModelLength = 100;
        public const string AuditActionCreateType = "AssetTypes.CreateFromAsset";
        public const string AuditActionCreateSubType = "AssetSubTypes.CreateFromAsset";

        private static readonly Regex WhitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);

        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IAuditWriter _auditWriter;

        public AssetClassificationCreateService(
            IUnitOfWork unitOfWork,
            IOrganizationScopeService organizationScope = null,
            IAuditWriter auditWriter = null)
        {
            _unitOfWork = unitOfWork;
            _organizationScope = organizationScope;
            _auditWriter = auditWriter;
        }

        public AssetTypeWithSubTypeCreatedVm CreateTypeWithSubType(AssetTypeWithSubTypeCreateVm model)
        {
            if (model == null)
            {
                throw new BusinessException("Asset type details are required.");
            }

            var typeName = NormalizeName(model.TypeName);
            var subTypeName = AssetSubTypeNormalizer.NormalizeName(NormalizeName(model.SubTypeName));
            if (model.CategoryId <= 0)
            {
                throw new BusinessException("Please select a category first. New categories cannot be created here.");
            }

            if (string.IsNullOrEmpty(typeName))
            {
                throw new BusinessException("Enter a name for the new asset type.");
            }

            if (string.IsNullOrEmpty(subTypeName))
            {
                throw new BusinessException("Enter the first sub-type for the new asset type. A type cannot be created without a sub-type.");
            }

            if (typeName.Length > MaxNameLength)
            {
                throw new BusinessException("The asset type name can be at most " + MaxNameLength + " characters.");
            }

            if (subTypeName.Length > MaxNameLength)
            {
                throw new BusinessException("The sub-type name can be at most " + MaxNameLength + " characters.");
            }

            // The repository is tenant-filtered (another organisation's category reads as null); the explicit check below
            // also covers an unscoped context.
            var category = _unitOfWork.Repository<AssetCategory>().GetById(model.CategoryId);
            var currentOrgId = _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId();
            if (category == null
                || (currentOrgId.HasValue && category.OrganizationId.HasValue && category.OrganizationId.Value != currentOrgId.Value))
            {
                throw new BusinessException("The selected category was not found.");
            }

            if (!category.IsActive)
            {
                throw new BusinessException("The selected category is inactive. Choose an active category.");
            }

            var existingType = _unitOfWork.Repository<AssetType>()
                .Find(x => x.AssetCategoryId == category.Id)
                .FirstOrDefault(x => NamesEqual(x.Name, typeName));
            if (existingType != null)
            {
                throw new BusinessException(existingType.IsActive
                    ? "\"" + category.Name + "\" already has an asset type named \"" + existingType.Name + "\". Select it from the list instead."
                    : "\"" + category.Name + "\" already has an inactive asset type named \"" + existingType.Name + "\". Reactivate it under Asset types instead of creating a duplicate.");
            }

            var organizationId = category.OrganizationId ?? currentOrgId;
            AssetType type = null;
            AssetSubType subType = null;
            _unitOfWork.ExecuteInTransaction(() =>
            {
                var now = DateTime.UtcNow;
                type = new AssetType
                {
                    OrganizationId = organizationId,
                    AssetCategoryId = category.Id,
                    Name = typeName,
                    IsActive = true,
                    CreatedAt = now
                };
                _unitOfWork.Repository<AssetType>().Add(type);
                _unitOfWork.SaveChanges();
                if (type.Id <= 0)
                {
                    throw new BusinessException("The asset type could not be created.");
                }

                EnsureSubTypeNameIsFree(type.Id, subTypeName);
                var itemModel = AssetSubTypeNormalizer.NormalizeModel(subTypeName);
                if (itemModel.Length > MaxModelLength)
                {
                    itemModel = itemModel.Substring(0, MaxModelLength).TrimEnd();
                }

                subType = new AssetSubType
                {
                    OrganizationId = organizationId,
                    AssetTypeId = type.Id,
                    Name = subTypeName,
                    Brand = string.Empty,
                    Model = itemModel,
                    IsActive = true,
                    CreatedAt = now
                };
                _unitOfWork.Repository<AssetSubType>().Add(subType);
                _unitOfWork.SaveChanges();

                // AuditWriter enlists in the active transaction, so the audit rows commit or roll back with the data.
                if (_auditWriter != null)
                {
                    _auditWriter.Write(AuditActionCreateType, nameof(AssetType), type.Id.ToString(), null,
                        "Name=" + type.Name + "; CategoryId=" + category.Id + "; Category=" + category.Name + "; FirstSubTypeId=" + subType.Id);
                    _auditWriter.Write(AuditActionCreateSubType, nameof(AssetSubType), subType.Id.ToString(), null,
                        "Name=" + subType.Name + "; AssetTypeId=" + type.Id);
                }
            });

            return new AssetTypeWithSubTypeCreatedVm
            {
                CategoryId = category.Id,
                CategoryName = category.Name,
                AssetTypeId = type.Id,
                AssetTypeName = type.Name,
                AssetSubTypeId = subType.Id,
                AssetSubTypeName = subType.Name
            };
        }

        /// <summary>Trim, collapse inner whitespace; null-safe.</summary>
        public static string NormalizeName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : WhitespaceRegex.Replace(value.Trim(), " ");
        }

        /// <summary>Case-insensitive, trimmed (and inner-whitespace collapsed) name comparison.</summary>
        public static bool NamesEqual(string left, string right)
        {
            return string.Equals(NormalizeName(left), NormalizeName(right), StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureSubTypeNameIsFree(int assetTypeId, string subTypeName)
        {
            var duplicate = _unitOfWork.Repository<AssetSubType>()
                .Find(x => x.AssetTypeId == assetTypeId)
                .Any(x => NamesEqual(x.Name, subTypeName));
            if (duplicate)
            {
                throw new BusinessException("This asset type already has a sub-type named \"" + subTypeName + "\".");
            }
        }
    }
}
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
    /// Mirrors AssetSubTypeService.CreateFromAsset; categories stay select-only there.
    /// CreateChain (requisition form / receiving "Add new..."): sub-type, type + first sub-type, or category + first type
    /// + first sub-type, always in one transaction (see ClassificationInlineCreatePolicy for who may call it). New rows
    /// always get the caller's own organisation and record CreatedById / CreatedVia (migration 110) plus an AuditLog row.
    /// </summary>
    public class AssetClassificationCreateService : IAssetClassificationCreateService
    {
        public const int MaxNameLength = 200;
        public const int MaxModelLength = 100;
        public const string AuditActionCreateType = "AssetTypes.CreateFromAsset";
        public const string AuditActionCreateSubType = "AssetSubTypes.CreateFromAsset";
        public const string AuditActionCreateCategoryInline = "AssetCategories.CreateInline";
        public const string AuditActionCreateTypeInline = "AssetTypes.CreateInline";
        public const string AuditActionCreateSubTypeInline = "AssetSubTypes.CreateInline";

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

        public ClassificationChainCreatedVm CreateChain(ClassificationChainCreateVm model, string createdById, string createdVia)
        {
            if (model == null)
            {
                throw new BusinessException("Classification details are required.");
            }

            // Who / where (migration 110 CreatedById / CreatedVia + the AuditLog row) come from the controller, never from the post.
            var via = ClassificationCreateOrigin.Normalize(createdVia);
            if (via == null)
            {
                throw new BusinessException("Categories, types and sub-types can only be added here from the requisition form or the receiving screen.");
            }

            if (string.IsNullOrWhiteSpace(createdById))
            {
                throw new BusinessException("Sign in again to add a category, type or sub-type.");
            }

            var origin = new Origin { UserId = createdById.Trim(), Via = via };

            var subTypeName = AssetSubTypeNormalizer.NormalizeName(NormalizeName(model.SubTypeName));
            if (string.IsNullOrEmpty(subTypeName))
            {
                throw new BusinessException("Enter the sub-type name. Every asset type needs at least one sub-type.");
            }

            EnsureLength(subTypeName, "sub-type");

            // New rows always belong to the signed-in user's own organisation (there is no organisation field to post).
            var scopedOrgId = _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId();
            if (!scopedOrgId.HasValue || scopedOrgId.Value <= 0)
            {
                throw new BusinessException("Open this page inside your organisation to add a category, type or sub-type.");
            }

            var currentOrgId = scopedOrgId.Value;
            if (model.AssetTypeId.HasValue && model.AssetTypeId.Value > 0)
            {
                return CreateSubTypeUnderExistingType(model.AssetTypeId.Value, subTypeName, currentOrgId, origin);
            }

            if (model.CategoryId.HasValue && model.CategoryId.Value > 0)
            {
                return CreateTypeUnderExistingCategory(model.CategoryId.Value, model.TypeName, subTypeName, currentOrgId, origin);
            }

            return CreateCategoryTypeAndSubType(model, subTypeName, currentOrgId, origin);
        }

        private sealed class Origin
        {
            public string UserId { get; set; }

            public string Via { get; set; }

            public string AuditSuffix
            {
                get { return "; CreatedVia=" + Via + "; CreatedById=" + UserId; }
            }
        }

        private ClassificationChainCreatedVm CreateTypeUnderExistingCategory(int categoryId, string rawTypeName, string subTypeName, int currentOrgId, Origin origin)
        {
            var typeName = NormalizeName(rawTypeName);
            if (string.IsNullOrEmpty(typeName))
            {
                throw new BusinessException("Enter a name for the new asset type.");
            }

            EnsureLength(typeName, "asset type");
            var category = _unitOfWork.Repository<AssetCategory>().GetById(categoryId);
            if (category == null || !InOrganization(category.OrganizationId, currentOrgId))
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

            AssetType type = null;
            AssetSubType subType = null;
            _unitOfWork.ExecuteInTransaction(() =>
            {
                var now = DateTime.UtcNow;
                type = AddType(category.Id, typeName, currentOrgId, now, origin);
                subType = AddSubType(type, subTypeName, currentOrgId, now, origin);
                WriteAudit(AuditActionCreateTypeInline, nameof(AssetType), type.Id, "Name=" + type.Name + "; CategoryId=" + category.Id + "; Category=" + category.Name + "; FirstSubTypeId=" + subType.Id + origin.AuditSuffix);
                WriteAudit(AuditActionCreateSubTypeInline, nameof(AssetSubType), subType.Id, "Name=" + subType.Name + "; AssetTypeId=" + type.Id + origin.AuditSuffix);
            });

            return new ClassificationChainCreatedVm
            {
                CategoryId = category.Id,
                CategoryName = category.Name,
                AssetTypeId = type.Id,
                AssetTypeName = type.Name,
                AssetSubTypeId = subType.Id,
                AssetSubTypeName = subType.Name,
                TypeCreated = true
            };
        }

        private ClassificationChainCreatedVm CreateSubTypeUnderExistingType(int assetTypeId, string subTypeName, int currentOrgId, Origin origin)
        {
            var type = _unitOfWork.Repository<AssetType>().GetById(assetTypeId);
            if (type == null || !InOrganization(type.OrganizationId, currentOrgId))
            {
                throw new BusinessException("The selected asset type was not found.");
            }

            if (!type.IsActive)
            {
                throw new BusinessException("The selected asset type is inactive. Choose an active type.");
            }

            var category = _unitOfWork.Repository<AssetCategory>().GetById(type.AssetCategoryId);
            if (category == null || !InOrganization(category.OrganizationId, currentOrgId))
            {
                throw new BusinessException("The selected asset type's category was not found.");
            }

            if (!category.IsActive)
            {
                throw new BusinessException("The category \"" + category.Name + "\" is inactive. Choose an active category.");
            }

            EnsureSubTypeNameIsFree(type.Id, subTypeName);
            AssetSubType subType = null;
            _unitOfWork.ExecuteInTransaction(() =>
            {
                subType = AddSubType(type, subTypeName, currentOrgId, DateTime.UtcNow, origin);
                WriteAudit(AuditActionCreateSubTypeInline, nameof(AssetSubType), subType.Id, "Name=" + subType.Name + "; AssetTypeId=" + type.Id + origin.AuditSuffix);
            });

            return new ClassificationChainCreatedVm
            {
                CategoryId = category.Id,
                CategoryName = category.Name,
                AssetTypeId = type.Id,
                AssetTypeName = type.Name,
                AssetSubTypeId = subType.Id,
                AssetSubTypeName = subType.Name
            };
        }

        private ClassificationChainCreatedVm CreateCategoryTypeAndSubType(ClassificationChainCreateVm model, string subTypeName, int currentOrgId, Origin origin)
        {
            var categoryName = NormalizeName(model.CategoryName);
            var typeName = NormalizeName(model.TypeName);
            if (string.IsNullOrEmpty(categoryName))
            {
                throw new BusinessException("Choose a category, or enter a name for the new category.");
            }

            if (string.IsNullOrEmpty(typeName))
            {
                throw new BusinessException("Enter the first asset type for the new category. A category is created together with its first type and sub-type.");
            }

            EnsureLength(categoryName, "category");
            EnsureLength(typeName, "asset type");

            var existing = _unitOfWork.Repository<AssetCategory>().GetAll()
                .Where(x => InOrganization(x.OrganizationId, currentOrgId))
                .FirstOrDefault(x => NamesEqual(x.Name, categoryName));
            if (existing != null)
            {
                throw new BusinessException(existing.IsActive
                    ? "A category named \"" + existing.Name + "\" already exists. Select it from the list instead."
                    : "An inactive category named \"" + existing.Name + "\" already exists. Reactivate it under Asset Categories instead of creating a duplicate.");
            }

            AssetCategory category = null;
            AssetType type = null;
            AssetSubType subType = null;
            _unitOfWork.ExecuteInTransaction(() =>
            {
                var now = DateTime.UtcNow;
                category = new AssetCategory
                {
                    OrganizationId = currentOrgId,
                    Name = categoryName,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedById = origin.UserId,
                    CreatedVia = origin.Via
                };
                _unitOfWork.Repository<AssetCategory>().Add(category);
                _unitOfWork.SaveChanges();
                if (category.Id <= 0)
                {
                    throw new BusinessException("The category could not be created.");
                }

                type = AddType(category.Id, typeName, currentOrgId, now, origin);
                subType = AddSubType(type, subTypeName, currentOrgId, now, origin);
                WriteAudit(AuditActionCreateCategoryInline, nameof(AssetCategory), category.Id, "Name=" + category.Name + "; FirstTypeId=" + type.Id + origin.AuditSuffix);
                WriteAudit(AuditActionCreateTypeInline, nameof(AssetType), type.Id, "Name=" + type.Name + "; CategoryId=" + category.Id + "; FirstSubTypeId=" + subType.Id + origin.AuditSuffix);
                WriteAudit(AuditActionCreateSubTypeInline, nameof(AssetSubType), subType.Id, "Name=" + subType.Name + "; AssetTypeId=" + type.Id + origin.AuditSuffix);
            });

            return new ClassificationChainCreatedVm
            {
                CategoryId = category.Id,
                CategoryName = category.Name,
                AssetTypeId = type.Id,
                AssetTypeName = type.Name,
                AssetSubTypeId = subType.Id,
                AssetSubTypeName = subType.Name,
                CategoryCreated = true,
                TypeCreated = true
            };
        }

        private AssetType AddType(int categoryId, string typeName, int organizationId, DateTime now, Origin origin)
        {
            var type = new AssetType
            {
                OrganizationId = organizationId,
                AssetCategoryId = categoryId,
                Name = typeName,
                IsActive = true,
                CreatedAt = now,
                CreatedById = origin.UserId,
                CreatedVia = origin.Via
            };
            _unitOfWork.Repository<AssetType>().Add(type);
            _unitOfWork.SaveChanges();
            if (type.Id <= 0)
            {
                throw new BusinessException("The asset type could not be created.");
            }

            return type;
        }

        private AssetSubType AddSubType(AssetType type, string subTypeName, int organizationId, DateTime now, Origin origin)
        {
            // Same duplicate rule as every other sub-type create (case-insensitive under the same type).
            EnsureSubTypeNameIsFree(type.Id, subTypeName);
            var itemModel = AssetSubTypeNormalizer.NormalizeModel(subTypeName);
            if (itemModel.Length > MaxModelLength)
            {
                itemModel = itemModel.Substring(0, MaxModelLength).TrimEnd();
            }

            var subType = new AssetSubType
            {
                OrganizationId = organizationId,
                AssetTypeId = type.Id,
                Name = subTypeName,
                Brand = string.Empty,
                Model = itemModel,
                IsActive = true,
                CreatedAt = now,
                CreatedById = origin.UserId,
                CreatedVia = origin.Via
            };
            _unitOfWork.Repository<AssetSubType>().Add(subType);
            _unitOfWork.SaveChanges();
            return subType;
        }

        private void WriteAudit(string action, string entity, int id, string newValue)
        {
            // AuditWriter enlists in the active transaction, so the audit rows commit or roll back with the data.
            if (_auditWriter != null)
            {
                _auditWriter.Write(action, entity, id.ToString(), null, newValue);
            }
        }

        private static void EnsureLength(string value, string what)
        {
            if (value != null && value.Length > MaxNameLength)
            {
                throw new BusinessException("The " + what + " name can be at most " + MaxNameLength + " characters.");
            }
        }

        private static bool InOrganization(int? entityOrganizationId, int? currentOrgId)
        {
            return !currentOrgId.HasValue || !entityOrganizationId.HasValue || entityOrganizationId.Value == currentOrgId.Value;
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
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public class AssetImportService : IAssetImportService
    {
        private const int ImportBatchSize = 100;
        private const string TemplateSampleAssetTag = "HR-CHR-001";

        // Required columns first (left), then optional by importance. AssetTag is omitted — auto-generated on import.
        // CategoryId/AssetTypeId/DepartmentId/SupplierId and UsefulLifeMonths are omitted from the template
        // (IDs still accepted if present; useful life is resolved from type/category).
        private static readonly string[] TemplateHeaders =
        {
            "AssetName", "AssetCategory", "AssetType", "Brand", "Model", "PurchaseDate", "AcquisitionCost",
            "AssetSubType", "SerialNumber", "Description", "Department", "Class",
            "Supplier", "Currency", "TaxAmount", "ConditionOnReceipt", "SalvageValue", "DepreciationMethod", "DepreciationStartDate",
            "DepreciationLifeMonths", "DepreciationRatePercent", "IsInsured", "InsuredValue",
            "WarrantyStartDate", "WarrantyEndDate", "CurrentStatus", "Condition", "Specifications", "IsLeased", "PolicyReference",
            "Quantity"
        };

        private static readonly HashSet<string> AlwaysRequiredColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AssetName", "AssetCategory", "AssetType", "PurchaseDate", "AcquisitionCost"
        };


        private readonly IUnitOfWork _unitOfWork;
        private readonly IAssignmentService _assignmentService;
        private readonly IDepartmentScopeService _departmentScope;
        private readonly IRoleService _roleService;
        private readonly IAuditWriter _auditWriter;
        private readonly IOperationsQueryRepository _operationsQueryRepository;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IReferenceDataCache _referenceDataCache;
        private readonly IAssetSubTypeService _assetSubTypeService;
        private HashSet<string> _reservedImportTags;

        public AssetImportService(
            IUnitOfWork unitOfWork,
            IAssignmentService assignmentService,
            IDepartmentScopeService departmentScope,
            IRoleService roleService,
            IAuditWriter auditWriter,
            IOperationsQueryRepository operationsQueryRepository,
            IOrganizationScopeService organizationScope,
            IReferenceDataCache referenceDataCache,
            IAssetSubTypeService assetSubTypeService)
        {
            _unitOfWork = unitOfWork;
            _assignmentService = assignmentService;
            _departmentScope = departmentScope;
            _roleService = roleService;
            _auditWriter = auditWriter;
            _operationsQueryRepository = operationsQueryRepository;
            _organizationScope = organizationScope;
            _referenceDataCache = referenceDataCache;
            _assetSubTypeService = assetSubTypeService;
        }

        public byte[] GetImportTemplate()
        {
            var headers = TemplateHeaders.Select(FormatTemplateHeader).ToArray();
            var requirementLabels = TemplateHeaders.Select(FormatRequirementLabel).ToArray();
            ImportLookups lookups = null;
            try
            {
                lookups = BuildLookups();
            }
            catch (BusinessException)
            {
                lookups = null;
            }

            var sampleRow = BuildSampleRowFromExistingAsset(lookups);
            var dropdowns = BuildTemplateDropdowns();

            return XlsxImportTemplateBuilder.Build(
                headers,
                requirementLabels,
                sampleRow,
                dropdowns,
                AssetImportTemplateInstructions.Lines);
        }

        private string[] BuildSampleRowFromExistingAsset(ImportLookups lookups)
        {
            var fallback = GetDefaultSampleRow();
            if (lookups == null)
            {
                return fallback;
            }

            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return fallback;
            }

            var sampleAsset = _unitOfWork.Repository<Asset>().GetAll()
                .FirstOrDefault(x => x.IsActive
                    && x.OrganizationId == organizationId.Value
                    && string.Equals(x.AssetTag, TemplateSampleAssetTag, StringComparison.OrdinalIgnoreCase));

            if (sampleAsset == null)
            {
                return fallback;
            }

            return MapAssetToSampleRow(sampleAsset, lookups);
        }

        private static string[] GetDefaultSampleRow()
        {
            return new[]
            {
                "Wooden desk", "Classrooms", "Desks", "Generic", "Standard desk", "2024-01-01", "5000.00",
                "", "", "12 units", "Classroom", "2A",
                "", "KES", "0", "Good", "0", DepreciationMethod.StraightLine.ToString(), "2024-01-01",
                "", "", "false", "",
                "", "", AssetStatus.InStore.ToString(), AssetCondition.Good.ToString(), "",
                "false", "", "12"
            };
        }

        private static Dictionary<string, string[]> BuildTemplateDropdowns()
        {
            // Category, AssetType, Department, and Supplier are free-text columns (no Excel dropdowns).
            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "ConditionOnReceipt", Enum.GetNames(typeof(AssetCondition)) },
                { "Condition", Enum.GetNames(typeof(AssetCondition)) },
                { "DepreciationMethod", Enum.GetNames(typeof(DepreciationMethod)) },
                { "CurrentStatus", Enum.GetNames(typeof(AssetStatus)) },
                { "IsInsured", new[] { "true", "false" } },
                { "IsLeased", new[] { "true", "false" } }
            };
        }

        private string[] MapAssetToSampleRow(Asset asset, ImportLookups lookups)
        {
            AssetCategory category;
            AssetType assetType;
            Department department = null;
            Supplier supplier = null;

            lookups.CategoriesById.TryGetValue(asset.CategoryId, out category);
            lookups.AssetTypesById.TryGetValue(asset.AssetTypeId, out assetType);
            if (asset.DepartmentId.HasValue)
            {
                lookups.DepartmentsById.TryGetValue(asset.DepartmentId.Value, out department);
            }

            if (asset.SupplierId.HasValue)
            {
                lookups.SuppliersById.TryGetValue(asset.SupplierId.Value, out supplier);
            }

            var subTypeName = ResolveAssetSubTypeName(asset.AssetSubTypeId);
            var depreciationMethod = asset.DepreciationMethod == 0
                ? DepreciationMethod.StraightLine
                : asset.DepreciationMethod;
            var currentStatus = asset.CurrentStatus == 0 ? AssetStatus.InStore : asset.CurrentStatus;

            return new[]
            {
                asset.AssetName,
                category == null ? string.Empty : category.Name,
                assetType == null ? string.Empty : assetType.Name,
                asset.Brand,
                asset.Model,
                FormatTemplateDate(asset.PurchaseDate),
                FormatTemplateDecimal(asset.AcquisitionCost),
                subTypeName,
                asset.SerialNumber,
                asset.Description,
                department == null ? string.Empty : department.Name,
                string.Empty,
                supplier == null ? string.Empty : supplier.SupplierName,
                string.IsNullOrWhiteSpace(asset.Currency) ? FinanceDefaults.DefaultCurrencyCode : asset.Currency,
                FormatTemplateDecimal(asset.TaxAmount),
                asset.ConditionOnReceipt,
                FormatTemplateDecimal(asset.SalvageValue),
                depreciationMethod.ToString(),
                FormatTemplateDate(asset.DepreciationStartDate),
                asset.DepreciationLifeMonths.HasValue ? asset.DepreciationLifeMonths.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                asset.DepreciationRatePercent.HasValue ? FormatTemplateDecimal(asset.DepreciationRatePercent.Value) : string.Empty,
                asset.IsInsured ? "true" : "false",
                asset.InsuredValue.HasValue ? FormatTemplateDecimal(asset.InsuredValue.Value) : string.Empty,
                FormatTemplateDate(asset.WarrantyStartDate),
                FormatTemplateDate(asset.WarrantyEndDate),
                currentStatus.ToString(),
                asset.Condition.ToString(),
                asset.Specifications,
                asset.IsLeased ? "true" : "false",
                asset.PolicyReference,
                "1"
            };
        }

        private string ResolveAssetSubTypeName(int? subTypeId)
        {
            if (!subTypeId.HasValue || subTypeId.Value <= 0)
            {
                return string.Empty;
            }

            var subType = _assetSubTypeService.GetById(subTypeId.Value);
            return subType == null ? string.Empty : AssetSubTypeNormalizer.NormalizeName(subType.Name);
        }

        private static string FormatTemplateDate(DateTime? value)
        {
            return value.HasValue && value.Value != default(DateTime)
                ? value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private static string FormatTemplateDecimal(decimal value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static string FormatTemplateHeader(string columnKey)
        {
            return AlwaysRequiredColumns.Contains(columnKey) ? "*" + columnKey : columnKey;
        }

        private static string FormatRequirementLabel(string columnKey)
        {
            return AlwaysRequiredColumns.Contains(columnKey) ? "Required" : "Optional";
        }

        public AssetImportResultVm Import(Stream content, string fileName, string actorUserId)
        {
            var rows = SpreadsheetImportHelper.ReadRows(content, fileName);
            var rowMaps = SpreadsheetImportHelper.ToRowMaps(rows);
            ValidateDuplicateRowsInFile(rowMaps);

            var provisioner = new SchoolImportProvisioner(_unitOfWork, _organizationScope, _referenceDataCache);
            var provisionSummary = provisioner.ProvisionFromRows(rowMaps, GetValue);
            _unitOfWork.ClearTracking();

            var lookups = BuildLookups();
            var defaultProcesses = AssetApprovalSettingsHelper
                .BuildDefaultProcesses(_unitOfWork, _roleService.GetRoles())
                .ToList();

            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                throw new BusinessException("Organization context is required for asset import.");
            }

            var result = new AssetImportResultVm();
            var preparedRows = new List<PreparedImportRow>();
            var rowNumber = 1;
            foreach (var row in rowMaps)
            {
                rowNumber++;
                if (string.IsNullOrWhiteSpace(GetValue(row, "AssetName")))
                {
                    continue;
                }

                try
                {
                    var quantity = ImportQuantityParser.ResolveQuantity(row, GetValue);
                    var model = MapRow(row, lookups, defaultProcesses);
                    ResolveImportSubType(model, row, result, rowNumber);
                    for (var unitIndex = 0; unitIndex < quantity; unitIndex++)
                    {
                        var unitModel = CloneImportModel(model, row, unitIndex, quantity);
                        ValidateSerialNotDuplicateInDatabase(organizationId.Value, unitModel.SerialNumber);
                        preparedRows.Add(new PreparedImportRow
                        {
                            RowNumber = rowNumber,
                            Model = unitModel,
                            RawRow = row
                        });
                    }
                }
                catch (BusinessException ex)
                {
                    result.SkippedCount++;
                    result.Messages.Add("Row " + rowNumber + ": " + ex.Message);
                }
            }

            _unitOfWork.ClearTracking();
            _reservedImportTags = LoadExistingAssetTags(organizationId.Value);

            for (var batchStart = 0; batchStart < preparedRows.Count; batchStart += ImportBatchSize)
            {
                var batch = preparedRows.Skip(batchStart).Take(ImportBatchSize).ToList();
                try
                {
                    _unitOfWork.ExecuteInTransaction(() =>
                    {
                        foreach (var prepared in batch)
                        {
                            ValidateSerialNotDuplicateInDatabase(organizationId.Value, prepared.Model.SerialNumber);
                            var entity = BuildImportAssetEntity(prepared.Model, prepared.RawRow, actorUserId);
                            _unitOfWork.Repository<Asset>().Add(entity);
                            prepared.AssetId = entity.Id;
                        }
                    });

                    result.ImportedCount += batch.Count;
                    ResetImportBatchState(organizationId.Value);
                }
                catch (BusinessException ex)
                {
                    ResetImportBatchState(organizationId.Value);
                    ImportBatchIndividually(batch, actorUserId, organizationId.Value, result);
                    if (!string.IsNullOrWhiteSpace(ex.Message))
                    {
                        result.Messages.Add("Batch starting at row " + batch[0].RowNumber + ": " + ex.Message);
                    }
                }
                catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                {
                    ResetImportBatchState(organizationId.Value);
                    ImportBatchIndividually(batch, actorUserId, organizationId.Value, result);
                    result.Messages.Add("Batch starting at row " + batch[0].RowNumber + ": duplicate asset tag or serial number detected.");
                }
                catch (Exception)
                {
                    ResetImportBatchState(organizationId.Value);
                    ImportBatchIndividually(batch, actorUserId, organizationId.Value, result);
                    result.Messages.Add("Batch starting at row " + batch[0].RowNumber + ": unexpected error during batch import.");
                }
            }

            if (result.ImportedCount > 0 || provisionSummary.DepartmentsCreated > 0)
            {
                _unitOfWork.ClearTracking();
                _auditWriter.Write(
                    "Assets.Import",
                    nameof(Asset),
                    null,
                    null,
                    "imported=" + result.ImportedCount + ";skipped=" + result.SkippedCount
                    + ";provisionDepts=" + provisionSummary.DepartmentsCreated
                    + ";provisionCategories=" + provisionSummary.CategoriesCreated
                    + ";provisionTypes=" + provisionSummary.AssetTypesCreated
                    + ";provisionSuppliers=" + provisionSummary.SuppliersCreated
                    + ";file=" + (fileName ?? string.Empty));
            }

            if (provisionSummary.DepartmentsCreated > 0
                || provisionSummary.CategoriesCreated > 0
                || provisionSummary.AssetTypesCreated > 0
                || provisionSummary.SuppliersCreated > 0)
            {
                result.Messages.Insert(0,
                    "Provisioned from template: "
                    + provisionSummary.DepartmentsCreated + " department(s), "
                    + provisionSummary.CategoriesCreated + " category(ies), "
                    + provisionSummary.AssetTypesCreated + " asset type(s), "
                    + provisionSummary.SuppliersCreated + " supplier(s).");
            }

            return result;
        }

        private static AssetCreateVm CloneImportModel(
            AssetCreateVm source,
            IDictionary<string, string> row,
            int unitIndex,
            int quantity)
        {
            var clone = new AssetCreateVm
            {
                AssetName = source.AssetName,
                AssetTag = null,
                CategoryId = source.CategoryId,
                AssetTypeId = source.AssetTypeId,
                AssetSubTypeId = source.AssetSubTypeId,
                Brand = source.Brand,
                Model = source.Model,
                SerialNumber = unitIndex == 0 ? NormalizeImportSerial(source.SerialNumber) : null,
                Description = source.Description,
                PurchaseDate = source.PurchaseDate,
                AcquisitionCost = source.AcquisitionCost,
                TaxAmount = source.TaxAmount,
                Currency = source.Currency,
                SupplierId = source.SupplierId,
                DepartmentId = source.DepartmentId,
                ConditionOnReceipt = source.ConditionOnReceipt,
                SalvageValue = source.SalvageValue,
                DepreciationMethod = source.DepreciationMethod,
                DepreciationStartDate = source.DepreciationStartDate,
                UseCustomDepreciationLife = source.UseCustomDepreciationLife,
                DepreciationLifeMonths = source.DepreciationLifeMonths,
                UseCustomDepreciationRate = source.UseCustomDepreciationRate,
                DepreciationRatePercent = source.DepreciationRatePercent,
                CanManageDepreciationSettings = source.CanManageDepreciationSettings,
                IsInsured = source.IsInsured,
                InsuredValue = source.InsuredValue,
                WarrantyStartDate = source.WarrantyStartDate,
                WarrantyEndDate = source.WarrantyEndDate,
                CurrentStatus = source.CurrentStatus,
                ApprovalProcesses = source.ApprovalProcesses
            };

            if (quantity > 1 && unitIndex > 0 && !string.IsNullOrWhiteSpace(clone.SerialNumber))
            {
                clone.SerialNumber = null;
            }

            return clone;
        }

        private static string NormalizeImportSerial(string serialNumber)
        {
            return string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim();
        }

        private static void ValidateDuplicateRowsInFile(IList<IDictionary<string, string>> rowMaps)
        {
            var serials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rowNumber = 1;
            foreach (var row in rowMaps)
            {
                rowNumber++;
                var serial = GetValue(row, "SerialNumber");
                if (!string.IsNullOrWhiteSpace(serial))
                {
                    var normalizedSerial = serial.Trim();
                    if (!serials.Add(normalizedSerial))
                    {
                        throw new BusinessException("Row " + rowNumber + ": Duplicate SerialNumber '" + normalizedSerial + "' in import file.");
                    }
                }
            }
        }

        private void ImportBatchIndividually(
            IList<PreparedImportRow> batch,
            string actorUserId,
            int organizationId,
            AssetImportResultVm result)
        {
            foreach (var prepared in batch)
            {
                try
                {
                    _unitOfWork.ExecuteInTransaction(() =>
                    {
                        ValidateSerialNotDuplicateInDatabase(organizationId, prepared.Model.SerialNumber);
                        var entity = BuildImportAssetEntity(prepared.Model, prepared.RawRow, actorUserId);
                        _unitOfWork.Repository<Asset>().Add(entity);
                        prepared.AssetId = entity.Id;
                    });
                    result.ImportedCount++;
                    ResetImportBatchState(organizationId);
                }
                catch (BusinessException ex)
                {
                    result.SkippedCount++;
                    result.Messages.Add("Row " + prepared.RowNumber + ": " + ex.Message);
                }
                catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                {
                    result.SkippedCount++;
                    result.Messages.Add("Row " + prepared.RowNumber + ": " + ex.Message);
                }
                catch (Exception)
                {
                    result.SkippedCount++;
                    result.Messages.Add("Row " + prepared.RowNumber + ": The row could not be imported due to an unexpected error.");
                }
            }
        }

        private void ValidateSerialNotDuplicateInDatabase(int organizationId, string serialNumber)
        {
            if (!string.IsNullOrWhiteSpace(serialNumber)
                && _operationsQueryRepository.ExistsActiveSerialNumber(organizationId, serialNumber))
            {
                throw new BusinessException("Serial number '" + serialNumber.Trim() + "' already exists.");
            }
        }

        private int CreateAssetInTransaction(AssetCreateVm model)
        {
            var entity = BuildImportAssetEntity(model, null, null);
            _unitOfWork.Repository<Asset>().Add(entity);
            _unitOfWork.SaveChanges();
            return entity.Id;
        }

        private Asset BuildImportAssetEntity(AssetCreateVm model, IDictionary<string, string> row, string actorUserId)
        {
            var assetTag = ResolveAssetTagForImport(model);

            var entity = new Asset
            {
                AssetName = model.AssetName,
                AssetTag = assetTag,
                CategoryId = model.CategoryId,
                AssetTypeId = model.AssetTypeId,
                AssetSubTypeId = model.AssetSubTypeId,
                Brand = model.Brand,
                Model = model.Model,
                SerialNumber = NormalizeImportSerial(model.SerialNumber),
                Description = model.Description,
                PurchaseDate = model.PurchaseDate,
                AcquisitionCost = model.AcquisitionCost,
                TaxAmount = model.TaxAmount,
                Currency = model.Currency,
                SupplierId = NormalizeOptionalId(model.SupplierId),
                DepartmentId = NormalizeOptionalId(model.DepartmentId),
                CurrentCustodianId = null,
                ConditionOnReceipt = model.ConditionOnReceipt,
                UsefulLifeMonths = UsefulLifeResolver.Resolve(_unitOfWork, model.AssetTypeId, model.CategoryId),
                SalvageValue = model.SalvageValue,
                DepreciationMethod = model.DepreciationMethod == 0 ? DepreciationMethod.StraightLine : model.DepreciationMethod,
                DepreciationStartDate = model.DepreciationStartDate == default(DateTime) ? model.PurchaseDate : model.DepreciationStartDate,
                CurrentBookValue = model.AcquisitionCost,
                AccumulatedDepreciation = 0,
                IsInsured = model.IsInsured,
                InsuredValue = model.InsuredValue,
                WarrantyStartDate = model.WarrantyStartDate,
                WarrantyEndDate = model.WarrantyEndDate,
                CurrentStatus = model.CurrentStatus == 0 ? AssetStatus.InStore : model.CurrentStatus,
                Condition = AssetCondition.New,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            DepreciationSettingsHelper.ApplyAssetOverrides(entity, model, model.CanManageDepreciationSettings);
            AssetApprovalSettingsHelper.ApplyToAsset(entity, model.ApprovalProcesses);

            if (row != null)
            {
                ApplyExtendedFieldsToEntity(entity, row, actorUserId);
            }

            return entity;
        }

        private string ResolveAssetTagForImport(AssetCreateVm model)
        {
            var takenTags = _unitOfWork.Repository<Asset>().Query()
                .Where(x => x.IsActive && x.AssetTag != null)
                .Select(x => x.AssetTag);
            var tag = AssetTagHelper.GenerateUniqueRandomTag(takenTags, _reservedImportTags);
            if (_reservedImportTags != null)
            {
                _reservedImportTags.Add(tag);
            }

            return tag;
        }

        private HashSet<string> LoadExistingAssetTags(int organizationId)
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var asset in _unitOfWork.Repository<Asset>().Query()
                .Where(x => x.IsActive && x.OrganizationId == organizationId && x.AssetTag != null))
            {
                tags.Add(asset.AssetTag);
            }

            return tags;
        }

        private void ResetImportBatchState(int organizationId)
        {
            _unitOfWork.ClearTracking();
            _reservedImportTags = LoadExistingAssetTags(organizationId);
        }

        private void ApplyExtendedFieldsInTransaction(int assetId, IDictionary<string, string> row, string actorUserId)
        {
            var entity = _unitOfWork.Repository<Asset>().GetById(assetId);
            if (entity == null)
            {
                throw new BusinessException("Imported asset could not be loaded.");
            }

            ApplyExtendedFieldsToEntity(entity, row, actorUserId);
            _unitOfWork.Repository<Asset>().Update(entity);
        }

        private void ApplyExtendedFieldsToEntity(Asset entity, IDictionary<string, string> row, string actorUserId)
        {
            if (entity == null || row == null)
            {
                return;
            }

            var specifications = GetValue(row, "Specifications");
            var condition = ParseOptionalEnum<AssetCondition>(row, "Condition");
            var custodianUserId = GetValue(row, "CustodianUserId");
            var impairmentNotes = GetValue(row, "ImpairmentNotes");
            var policyReference = GetValue(row, "PolicyReference");
            var isLeased = ParseOptionalBool(row, "IsLeased");

            if (!string.IsNullOrWhiteSpace(specifications))
            {
                entity.Specifications = specifications;
            }

            if (condition.HasValue)
            {
                entity.Condition = condition.Value;
            }

            if (!string.IsNullOrWhiteSpace(impairmentNotes))
            {
                entity.ImpairmentNotes = impairmentNotes;
            }

            if (!string.IsNullOrWhiteSpace(policyReference))
            {
                entity.PolicyReference = policyReference;
            }

            if (isLeased.HasValue)
            {
                entity.IsLeased = isLeased.Value;
            }

            if (entity.Id > 0 && !string.IsNullOrWhiteSpace(custodianUserId))
            {
                var assignment = _assignmentService.AssignWithoutSave(new AssetAssignmentVm
                {
                    AssetId = entity.Id,
                    ToUserId = custodianUserId.Trim(),
                    ToDepartmentId = entity.DepartmentId,
                    AssignmentType = AssignmentType.Permanent.ToString(),
                    AssignedDate = DateTime.UtcNow,
                    HandedOverById = actorUserId,
                    ReceivedById = custodianUserId.Trim()
                });
                _assignmentService.RecordAssignmentAudit(assignment, entity.Id);
            }
        }

        private void ResolveImportSubType(AssetCreateVm model, IDictionary<string, string> row, AssetImportResultVm result, int rowNumber)
        {
            var subTypeName = AssetSubTypeNormalizer.NormalizeName(GetValue(row, "AssetSubType"));
            if (!string.IsNullOrWhiteSpace(subTypeName))
            {
                var byName = _assetSubTypeService.GetByAssetTypeId(model.AssetTypeId)
                    .FirstOrDefault(x => string.Equals(
                        AssetSubTypeNormalizer.NormalizeName(x.Name),
                        subTypeName,
                        StringComparison.OrdinalIgnoreCase));
                if (byName == null)
                {
                    throw new BusinessException("AssetSubType '" + subTypeName + "' was not found for the selected asset type.");
                }

                model.AssetSubTypeId = byName.Id;
                model.Brand = byName.Brand;
                model.Model = byName.Model;
                return;
            }

            var resolver = new AssetSubTypeResolver(_assetSubTypeService);
            var resolution = resolver.Resolve(model.AssetTypeId, model.Brand, model.Model, model.AssetSubTypeId);
            if (resolution.IsMatched)
            {
                model.AssetSubTypeId = resolution.SubType.Id;
                model.Brand = resolution.SubType.Brand;
                model.Model = resolution.SubType.Model;
                return;
            }

            if (!resolution.RequiresAssignment)
            {
                return;
            }

            var createdId = _assetSubTypeService.CreateFromAsset(new AssetSubTypeCreateFromAssetVm
            {
                AssetTypeId = model.AssetTypeId,
                Name = AssetSubTypeNormalizer.BuildSuggestedName(model.Brand, model.Model),
                Brand = model.Brand,
                Model = model.Model
            });
            var created = _assetSubTypeService.GetById(createdId);
            if (created == null)
            {
                result.Messages.Add("Row " + rowNumber + ": Could not create asset sub-type for brand/model.");
                throw new BusinessException("Could not create asset sub-type for this brand and model.");
            }

            model.AssetSubTypeId = created.Id;
            model.Brand = created.Brand;
            model.Model = created.Model;
        }

        private AssetCreateVm MapRow(
            IDictionary<string, string> row,
            ImportLookups lookups,
            IList<ApprovalProcessSettingsVm> defaultProcesses)
        {
            var assetName = RequireValue(row, "AssetName");
            var brand = ResolveLegacyBrand(row);
            var modelName = ResolveLegacyModel(row);
            var purchaseDate = ResolveLegacyPurchaseDate(row);
            var acquisitionCost = ResolveLegacyAcquisitionCost(row);

            var category = ResolveCategory(row, lookups);
            var assetType = ResolveAssetType(row, lookups, category.Id);
            var department = ResolveDepartment(row, lookups);
            var supplier = ResolveSupplier(row, lookups);

            var currency = GetValue(row, "Currency");
            if (string.IsNullOrWhiteSpace(currency))
            {
                currency = ApprovalWorkflowSettingsHelper.GetDefaultCurrencyCode(_unitOfWork.Repository<SystemSetting>().GetAll());
            }

            var depreciationStartDate = ParseOptionalDate(row, "DepreciationStartDate") ?? purchaseDate;
            var currentStatus = ParseOptionalEnum<AssetStatus>(row, "CurrentStatus") ?? AssetStatus.InStore;
            var depreciationMethod = ParseOptionalEnum<DepreciationMethod>(row, "DepreciationMethod") ?? DepreciationMethod.StraightLine;
            var depreciationLifeMonths = ParseOptionalInt(row, "DepreciationLifeMonths");
            var depreciationRatePercent = ParseOptionalDecimal(row, "DepreciationRatePercent");

            var description = GetValue(row, "Description");

            return new AssetCreateVm
            {
                AssetName = assetName,
                AssetTag = null,
                CategoryId = category.Id,
                AssetTypeId = assetType.Id,
                Brand = brand,
                Model = modelName,
                SerialNumber = NormalizeImportSerial(GetValue(row, "SerialNumber")),
                Description = description,
                PurchaseDate = purchaseDate,
                AcquisitionCost = acquisitionCost,
                TaxAmount = ParseOptionalDecimal(row, "TaxAmount") ?? 0m,
                Currency = currency.Trim().ToUpperInvariant(),
                SupplierId = supplier == null ? (int?)null : supplier.Id,
                DepartmentId = department == null ? (int?)null : department.Id,
                ConditionOnReceipt = GetValue(row, "ConditionOnReceipt"),
                SalvageValue = ParseOptionalDecimal(row, "SalvageValue") ?? 0m,
                DepreciationMethod = depreciationMethod,
                DepreciationStartDate = depreciationStartDate,
                UseCustomDepreciationLife = depreciationLifeMonths.HasValue && depreciationLifeMonths.Value > 0,
                DepreciationLifeMonths = depreciationLifeMonths,
                UseCustomDepreciationRate = depreciationRatePercent.HasValue && depreciationRatePercent.Value > 0,
                DepreciationRatePercent = depreciationRatePercent,
                CanManageDepreciationSettings = true,
                IsInsured = ParseOptionalBool(row, "IsInsured") ?? false,
                InsuredValue = ParseOptionalDecimal(row, "InsuredValue"),
                WarrantyStartDate = ParseOptionalDate(row, "WarrantyStartDate"),
                WarrantyEndDate = ParseOptionalDate(row, "WarrantyEndDate"),
                CurrentStatus = currentStatus,
                ApprovalProcesses = defaultProcesses
            };
        }

        private void ApplyExtendedFields(int assetId, IDictionary<string, string> row)
        {
            // Kept for backward compatibility if referenced elsewhere; import uses ApplyExtendedFieldsInTransaction.
            ApplyExtendedFieldsInTransaction(assetId, row, null);
        }

        private AssetCategory ResolveCategory(IDictionary<string, string> row, ImportLookups lookups)
        {
            var categoryId = ParseOptionalInt(row, "CategoryId");
            if (categoryId.HasValue)
            {
                AssetCategory category;
                if (!lookups.CategoriesById.TryGetValue(categoryId.Value, out category))
                {
                    throw new BusinessException("CategoryId " + categoryId.Value + " was not found.");
                }

                return category;
            }

            var categoryName = GetValue(row, "AssetCategory");
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                categoryName = GetValue(row, "Category");
            }
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                throw new BusinessException("AssetCategory or Category is required.");
            }

            AssetCategory byName;
            if (!lookups.CategoriesByName.TryGetValue(NormalizeKey(categoryName), out byName))
            {
                throw new BusinessException("Category '" + categoryName + "' was not found.");
            }

            return byName;
        }

        private AssetType ResolveAssetType(IDictionary<string, string> row, ImportLookups lookups, int categoryId)
        {
            var assetTypeId = ParseOptionalInt(row, "AssetTypeId");
            if (assetTypeId.HasValue)
            {
                AssetType assetType;
                if (!lookups.AssetTypesById.TryGetValue(assetTypeId.Value, out assetType))
                {
                    throw new BusinessException("AssetTypeId " + assetTypeId.Value + " was not found.");
                }

                if (assetType.AssetCategoryId != categoryId)
                {
                    throw new BusinessException("AssetTypeId " + assetTypeId.Value + " does not belong to the selected category.");
                }

                return assetType;
            }

            var assetTypeName = GetValue(row, "AssetType");
            if (string.IsNullOrWhiteSpace(assetTypeName))
            {
                throw new BusinessException("AssetType or AssetTypeId is required.");
            }

            AssetType byName;
            if (!lookups.AssetTypesByName.TryGetValue(NormalizeKey(assetTypeName), out byName))
            {
                throw new BusinessException("AssetType '" + assetTypeName + "' was not found.");
            }

            if (byName.AssetCategoryId != categoryId)
            {
                throw new BusinessException("AssetType '" + assetTypeName + "' does not belong to category '" + GetCategoryLabel(row) + "'.");
            }

            return byName;
        }

        private Department ResolveDepartment(IDictionary<string, string> row, ImportLookups lookups)
        {
            var departmentId = ParseOptionalInt(row, "DepartmentId");
            if (departmentId.HasValue)
            {
                Department department;
                if (!lookups.DepartmentsById.TryGetValue(departmentId.Value, out department))
                {
                    throw new BusinessException("DepartmentId " + departmentId.Value + " was not found.");
                }

                _departmentScope.EnsureCanAccessDepartment(department);
                return department;
            }

            var departmentName = GetValue(row, "Department");
            var classValue = GetValue(row, "Class");
            if (SchoolClassCodeHelper.IsClassroomDepartment(departmentName))
            {
                var classCode = SchoolClassCodeHelper.BuildClassDepartmentCode(classValue);
                if (string.IsNullOrWhiteSpace(classCode))
                {
                    throw new BusinessException("Class is required when Department is Classroom (example: 2A).");
                }

                Department byCode;
                if (!lookups.DepartmentsByCode.TryGetValue(NormalizeKey(classCode), out byCode))
                {
                    throw new BusinessException("Class department '" + classCode + "' was not found.");
                }

                _departmentScope.EnsureCanAccessDepartment(byCode);
                return byCode;
            }

            if (SchoolDepartmentCodeHelper.ShouldResolveAsSubDepartment(departmentName, classValue))
            {
                var normalizedName = SchoolDepartmentCodeHelper.NormalizeAdminDepartmentName(departmentName);
                var parentCode = SchoolDepartmentCodeHelper.BuildAdminDepartmentCode(normalizedName);
                var subCode = SchoolDepartmentCodeHelper.BuildSubDepartmentCode(parentCode, classValue);
                Department subDepartment;
                if (!lookups.DepartmentsByCode.TryGetValue(NormalizeKey(subCode), out subDepartment))
                {
                    throw new BusinessException(
                        "Sub-department '" + classValue.Trim() + "' under '" + normalizedName + "' was not found.");
                }

                _departmentScope.EnsureCanAccessDepartment(subDepartment);
                return subDepartment;
            }

            if (string.IsNullOrWhiteSpace(departmentName))
            {
                if (_departmentScope.ScopedDepartmentId.HasValue)
                {
                    Department scoped;
                    if (lookups.DepartmentsById.TryGetValue(_departmentScope.ScopedDepartmentId.Value, out scoped))
                    {
                        return scoped;
                    }
                }

                return null;
            }

            var normalizedLookupKey = NormalizeKey(SchoolDepartmentCodeHelper.NormalizeAdminDepartmentName(departmentName));
            Department byName;
            if (lookups.DepartmentsByName.TryGetValue(normalizedLookupKey, out byName))
            {
                _departmentScope.EnsureCanAccessDepartment(byName);
                return byName;
            }

            if (lookups.DepartmentsByCode.TryGetValue(normalizedLookupKey, out byName))
            {
                _departmentScope.EnsureCanAccessDepartment(byName);
                return byName;
            }

            throw new BusinessException("Department '" + departmentName + "' was not found.");
        }

        private static string GetCategoryLabel(IDictionary<string, string> row)
        {
            var assetCategory = GetValue(row, "AssetCategory");
            if (!string.IsNullOrWhiteSpace(assetCategory))
            {
                return assetCategory;
            }

            return GetValue(row, "Category");
        }

        private static string ResolveLegacyBrand(IDictionary<string, string> row)
        {
            return LegacyImportDefaults.NormalizeForStorage(GetValue(row, "Brand"), LegacyImportDefaults.Brand);
        }

        private static string ResolveLegacyModel(IDictionary<string, string> row)
        {
            return LegacyImportDefaults.NormalizeForStorage(GetValue(row, "Model"), LegacyImportDefaults.Model);
        }

        private static DateTime ResolveLegacyPurchaseDate(IDictionary<string, string> row)
        {
            var parsed = ParseOptionalDate(row, "PurchaseDate");
            return parsed ?? new DateTime(2020, 1, 1);
        }

        private static decimal ResolveLegacyAcquisitionCost(IDictionary<string, string> row)
        {
            var parsed = ParseOptionalDecimal(row, "AcquisitionCost");
            return parsed.HasValue && parsed.Value > 0 ? parsed.Value : LegacyImportDefaults.AcquisitionCost;
        }

        private Supplier ResolveSupplier(IDictionary<string, string> row, ImportLookups lookups)
        {
            var supplierId = ParseOptionalInt(row, "SupplierId");
            if (supplierId.HasValue)
            {
                Supplier supplier;
                if (!lookups.SuppliersById.TryGetValue(supplierId.Value, out supplier))
                {
                    throw new BusinessException("SupplierId " + supplierId.Value + " was not found.");
                }

                return supplier;
            }

            var supplierName = GetValue(row, "Supplier");
            if (string.IsNullOrWhiteSpace(supplierName))
            {
                return null;
            }

            Supplier byName;
            if (!lookups.SuppliersByName.TryGetValue(NormalizeKey(supplierName), out byName))
            {
                throw new BusinessException("Supplier '" + supplierName + "' was not found.");
            }

            return byName;
        }

        private ImportLookups BuildLookups()
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                throw new BusinessException("Organization context is required for import.");
            }

            var categories = _referenceDataCache.GetCategories(organizationId.Value)
                .Where(x => x.IsActive)
                .Select(x => new AssetCategory
                {
                    Id = x.Id,
                    Name = x.Name,
                    IsActive = x.IsActive,
                    OrganizationId = organizationId.Value
                })
                .ToList();
            var assetTypes = _referenceDataCache.GetAssetTypes(organizationId.Value)
                .Where(x => x.IsActive)
                .Select(x => new AssetType
                {
                    Id = x.Id,
                    Name = x.Name,
                    AssetCategoryId = x.AssetCategoryId,
                    IsActive = x.IsActive,
                    OrganizationId = organizationId.Value
                })
                .ToList();
            var departments = _departmentScope.ApplyDepartmentScope(
                    _referenceDataCache.GetDepartments(organizationId.Value)
                        .Where(x => x.IsActive)
                        .Select(d => new Department
                        {
                            Id = d.Id,
                            Name = d.Name,
                            Code = d.Code,
                            Description = d.Description,
                            ParentDepartmentId = d.ParentDepartmentId,
                            DepartmentKind = d.DepartmentKind,
                            IsRequisitionTarget = d.IsRequisitionTarget,
                            IsActive = d.IsActive,
                            OrganizationId = organizationId.Value
                        }).AsQueryable())
                .ToList();
            var suppliers = _referenceDataCache.GetSuppliers(organizationId.Value)
                .Where(x => x.IsActive)
                .Select(x => new Supplier
                {
                    Id = x.Id,
                    SupplierName = x.SupplierName,
                    ContactPerson = x.ContactPerson,
                    Email = x.Email,
                    Phone = x.Phone,
                    Address = x.Address,
                    RegistrationNumber = x.RegistrationNumber,
                    Notes = x.Notes,
                    IsActive = x.IsActive,
                    OrganizationId = organizationId.Value
                })
                .ToList();

            return new ImportLookups
            {
                CategoriesById = categories.ToDictionary(x => x.Id),
                CategoriesByName = categories.GroupBy(x => NormalizeKey(x.Name)).ToDictionary(x => x.Key, x => x.First()),
                AssetTypesById = assetTypes.ToDictionary(x => x.Id),
                AssetTypesByName = assetTypes.GroupBy(x => NormalizeKey(x.Name)).ToDictionary(x => x.Key, x => x.First()),
                DepartmentsById = departments.ToDictionary(x => x.Id),
                DepartmentsByName = departments.GroupBy(x => NormalizeKey(x.Name)).ToDictionary(x => x.Key, x => x.First()),
                DepartmentsByCode = departments.GroupBy(x => NormalizeKey(x.Code)).ToDictionary(x => x.Key, x => x.First()),
                SuppliersById = suppliers.ToDictionary(x => x.Id),
                SuppliersByName = suppliers.GroupBy(x => NormalizeKey(x.SupplierName)).ToDictionary(x => x.Key, x => x.First())
            };
        }

        private static string RequireValue(IDictionary<string, string> row, string column)
        {
            var value = GetValue(row, column);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new BusinessException(column + " is required.");
            }

            return value.Trim();
        }

        private static string GetValue(IDictionary<string, string> row, string column)
        {
            if (row == null || string.IsNullOrWhiteSpace(column))
            {
                return string.Empty;
            }

            string value;
            return row.TryGetValue(column, out value) ? value : string.Empty;
        }

        private static string NormalizeKey(string value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static int? ParseOptionalInt(IDictionary<string, string> row, string column)
        {
            var value = GetValue(row, column);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            int parsed;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                throw new BusinessException(column + " must be a whole number.");
            }

            return parsed;
        }

        private static decimal? ParseOptionalDecimal(IDictionary<string, string> row, string column)
        {
            var value = GetValue(row, column);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            decimal parsed;
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed)
                && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed))
            {
                throw new BusinessException(column + " must be a number.");
            }

            return parsed;
        }

        private static decimal ParseRequiredDecimal(IDictionary<string, string> row, string column)
        {
            var parsed = ParseOptionalDecimal(row, column);
            if (!parsed.HasValue || parsed.Value <= 0)
            {
                throw new BusinessException(column + " must be greater than zero.");
            }

            return parsed.Value;
        }

        private static DateTime ParseRequiredDate(IDictionary<string, string> row, string column)
        {
            var parsed = ParseOptionalDate(row, column);
            if (!parsed.HasValue)
            {
                throw new BusinessException(column + " is required and must be a valid date (yyyy-MM-dd).");
            }

            return parsed.Value;
        }

        private static DateTime? ParseOptionalDate(IDictionary<string, string> row, string column)
        {
            var value = GetValue(row, column);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            DateTime parsed;
            if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                return parsed.Date;
            }

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
                || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed))
            {
                return parsed.Date;
            }

            throw new BusinessException(column + " must be a valid date (yyyy-MM-dd recommended).");
        }

        private static bool? ParseOptionalBool(IDictionary<string, string> row, string column)
        {
            var value = GetValue(row, column);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            throw new BusinessException(column + " must be true/false or yes/no.");
        }

        private static T? ParseOptionalEnum<T>(IDictionary<string, string> row, string column) where T : struct
        {
            var value = GetValue(row, column);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            T parsed;
            if (Enum.TryParse(value, true, out parsed))
            {
                return parsed;
            }

            throw new BusinessException(column + " has an invalid value.");
        }

        private sealed class PreparedImportRow
        {
            public int RowNumber { get; set; }

            public AssetCreateVm Model { get; set; }

            public IDictionary<string, string> RawRow { get; set; }

            public int AssetId { get; set; }
        }

        private sealed class ImportLookups
        {
            public Dictionary<int, AssetCategory> CategoriesById { get; set; }

            public Dictionary<string, AssetCategory> CategoriesByName { get; set; }

            public Dictionary<int, AssetType> AssetTypesById { get; set; }

            public Dictionary<string, AssetType> AssetTypesByName { get; set; }

            public Dictionary<int, Department> DepartmentsById { get; set; }

            public Dictionary<string, Department> DepartmentsByName { get; set; }

            public Dictionary<string, Department> DepartmentsByCode { get; set; }

            public Dictionary<int, Supplier> SuppliersById { get; set; }

            public Dictionary<string, Supplier> SuppliersByName { get; set; }
        }

        private static int? NormalizeOptionalId(int? value)
        {
            return value.HasValue && value.Value > 0 ? value : null;
        }
    }
}

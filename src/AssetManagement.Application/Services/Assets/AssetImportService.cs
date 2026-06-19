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
        private const int ImportBatchSize = 500;

        private static readonly string[] TemplateHeaders =
        {
            "AssetName", "AssetTag", "Category", "CategoryId", "AssetType", "AssetTypeId",
            "Brand", "Model", "SerialNumber", "Description", "PurchaseDate", "AcquisitionCost",
            "TaxAmount", "Currency", "Supplier", "SupplierId", "Department", "DepartmentId",
            "ConditionOnReceipt", "UsefulLifeMonths", "SalvageValue", "DepreciationMethod",
            "DepreciationStartDate", "IsInsured", "InsuredValue",
            "WarrantyStartDate", "WarrantyEndDate", "CurrentStatus", "BarcodeOrQRCode",
            "Specifications", "Condition", "CustodianUserId", "ImpairmentNotes", "PolicyReference", "IsLeased"
        };

        private readonly IUnitOfWork _unitOfWork;
        private readonly IAssignmentService _assignmentService;
        private readonly IDepartmentScopeService _departmentScope;
        private readonly IRoleService _roleService;
        private readonly IAuditWriter _auditWriter;
        private readonly IOperationsQueryRepository _operationsQueryRepository;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IReferenceDataCache _referenceDataCache;

        public AssetImportService(
            IUnitOfWork unitOfWork,
            IAssignmentService assignmentService,
            IDepartmentScopeService departmentScope,
            IRoleService roleService,
            IAuditWriter auditWriter,
            IOperationsQueryRepository operationsQueryRepository,
            IOrganizationScopeService organizationScope,
            IReferenceDataCache referenceDataCache)
        {
            _unitOfWork = unitOfWork;
            _assignmentService = assignmentService;
            _departmentScope = departmentScope;
            _roleService = roleService;
            _auditWriter = auditWriter;
            _operationsQueryRepository = operationsQueryRepository;
            _organizationScope = organizationScope;
            _referenceDataCache = referenceDataCache;
        }

        public byte[] GetImportTemplate()
        {
            var rows = new List<string[]>
            {
                TemplateHeaders,
                new[]
                {
                    "Dell Latitude 7420", "IT-LAP-001", "IT Equipment", "", "Laptop", "",
                    "Dell", "Latitude 7420", "SN123456", "Finance team laptop", "2024-01-15", "1200.00",
                    "0", FinanceDefaults.DefaultCurrencyCode, "", "", "", "",
                    "New", "36", "0", "StraightLine", "2024-01-15", "1200.00", "false", "",
                    "", "", "InStore", "IT-LAP-001", "", "New", "", "", "", "false"
                }
            };

            return CsvExportHelper.ToUtf8Bytes(rows);
        }

        public AssetImportResultVm Import(Stream content, string fileName, string actorUserId)
        {
            var rows = SpreadsheetImportHelper.ReadRows(content, fileName);
            var rowMaps = SpreadsheetImportHelper.ToRowMaps(rows);
            ValidateDuplicateRowsInFile(rowMaps);
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
                try
                {
                    var model = MapRow(row, lookups, defaultProcesses);
                    ValidateNotDuplicateInDatabase(organizationId.Value, model.AssetTag, model.SerialNumber);
                    preparedRows.Add(new PreparedImportRow
                    {
                        RowNumber = rowNumber,
                        Model = model,
                        RawRow = row
                    });
                }
                catch (BusinessException ex)
                {
                    result.SkippedCount++;
                    result.Messages.Add("Row " + rowNumber + ": " + ex.Message);
                }
            }

            for (var batchStart = 0; batchStart < preparedRows.Count; batchStart += ImportBatchSize)
            {
                var batch = preparedRows.Skip(batchStart).Take(ImportBatchSize).ToList();
                try
                {
                    _unitOfWork.ExecuteInTransaction(() =>
                    {
                        foreach (var prepared in batch)
                        {
                            ValidateNotDuplicateInDatabase(organizationId.Value, prepared.Model.AssetTag, prepared.Model.SerialNumber);
                            var assetId = CreateAssetInTransaction(prepared.Model);
                            ApplyExtendedFieldsInTransaction(assetId, prepared.RawRow, actorUserId);
                            prepared.AssetId = assetId;
                        }
                    });

                    result.ImportedCount += batch.Count;
                }
                catch (BusinessException ex)
                {
                    ImportBatchIndividually(batch, actorUserId, organizationId.Value, result);
                    if (!string.IsNullOrWhiteSpace(ex.Message))
                    {
                        result.Messages.Add("Batch starting at row " + batch[0].RowNumber + ": " + ex.Message);
                    }
                }
                catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                {
                    ImportBatchIndividually(batch, actorUserId, organizationId.Value, result);
                    result.Messages.Add("Batch starting at row " + batch[0].RowNumber + ": duplicate asset tag or serial number detected.");
                }
                catch (Exception)
                {
                    ImportBatchIndividually(batch, actorUserId, organizationId.Value, result);
                    result.Messages.Add("Batch starting at row " + batch[0].RowNumber + ": unexpected error during batch import.");
                }
            }

            if (result.ImportedCount > 0)
            {
                _auditWriter.Write(
                    "Assets.Import",
                    nameof(Asset),
                    null,
                    null,
                    "imported=" + result.ImportedCount + ";skipped=" + result.SkippedCount + ";file=" + (fileName ?? string.Empty));
            }

            return result;
        }

        private static void ValidateDuplicateRowsInFile(IList<IDictionary<string, string>> rowMaps)
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var serials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rowNumber = 1;
            foreach (var row in rowMaps)
            {
                rowNumber++;
                var tag = GetValue(row, "AssetTag");
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    var normalizedTag = tag.Trim();
                    if (!tags.Add(normalizedTag))
                    {
                        throw new BusinessException("Row " + rowNumber + ": Duplicate AssetTag '" + normalizedTag + "' in import file.");
                    }
                }

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
                        ValidateNotDuplicateInDatabase(organizationId, prepared.Model.AssetTag, prepared.Model.SerialNumber);
                        var assetId = CreateAssetInTransaction(prepared.Model);
                        ApplyExtendedFieldsInTransaction(assetId, prepared.RawRow, actorUserId);
                    });
                    result.ImportedCount++;
                }
                catch (BusinessException ex)
                {
                    result.SkippedCount++;
                    result.Messages.Add("Row " + prepared.RowNumber + ": " + ex.Message);
                }
                catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                {
                    result.SkippedCount++;
                    result.Messages.Add("Row " + prepared.RowNumber + ": A duplicate asset tag or serial number already exists.");
                }
                catch (Exception)
                {
                    result.SkippedCount++;
                    result.Messages.Add("Row " + prepared.RowNumber + ": The row could not be imported due to an unexpected error.");
                }
            }
        }

        private void ValidateNotDuplicateInDatabase(int organizationId, string assetTag, string serialNumber)
        {
            if (!string.IsNullOrWhiteSpace(assetTag)
                && _operationsQueryRepository.ExistsActiveAssetTag(organizationId, assetTag))
            {
                throw new BusinessException("Asset tag '" + assetTag.Trim() + "' already exists.");
            }

            if (!string.IsNullOrWhiteSpace(serialNumber)
                && _operationsQueryRepository.ExistsActiveSerialNumber(organizationId, serialNumber))
            {
                throw new BusinessException("Serial number '" + serialNumber.Trim() + "' already exists.");
            }
        }

        private int CreateAssetInTransaction(AssetCreateVm model)
        {
            var entity = new Asset
            {
                AssetName = model.AssetName,
                AssetTag = model.AssetTag,
                CategoryId = model.CategoryId,
                AssetTypeId = model.AssetTypeId,
                Brand = model.Brand,
                Model = model.Model,
                SerialNumber = model.SerialNumber,
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
                SalvageValue = 0,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = model.PurchaseDate,
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

            AssetApprovalSettingsHelper.ApplyToAsset(entity, model.ApprovalProcesses);
            _unitOfWork.Repository<Asset>().Add(entity);
            _unitOfWork.SaveChanges();
            return entity.Id;
        }

        private void ApplyExtendedFieldsInTransaction(int assetId, IDictionary<string, string> row, string actorUserId)
        {
            var entity = _unitOfWork.Repository<Asset>().GetById(assetId);
            if (entity == null)
            {
                throw new BusinessException("Imported asset could not be loaded.");
            }

            var barcode = GetValue(row, "BarcodeOrQRCode");
            var specifications = GetValue(row, "Specifications");
            var condition = ParseOptionalEnum<AssetCondition>(row, "Condition");
            var custodianUserId = GetValue(row, "CustodianUserId");
            var impairmentNotes = GetValue(row, "ImpairmentNotes");
            var policyReference = GetValue(row, "PolicyReference");
            var isLeased = ParseOptionalBool(row, "IsLeased");

            if (!string.IsNullOrWhiteSpace(barcode))
            {
                entity.BarcodeOrQRCode = barcode;
            }

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

            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Asset>().Update(entity);

            if (!string.IsNullOrWhiteSpace(custodianUserId))
            {
                _assignmentService.AssignWithoutSave(new AssetAssignmentVm
                {
                    AssetId = assetId,
                    ToUserId = custodianUserId.Trim(),
                    ToDepartmentId = entity.DepartmentId,
                    AssignmentType = AssignmentType.Permanent.ToString(),
                    AssignedDate = DateTime.UtcNow,
                    HandedOverById = actorUserId,
                    ReceivedById = custodianUserId.Trim()
                });
            }
        }

        private AssetCreateVm MapRow(
            IDictionary<string, string> row,
            ImportLookups lookups,
            IList<ApprovalProcessSettingsVm> defaultProcesses)
        {
            var assetName = RequireValue(row, "AssetName");
            var assetTag = RequireValue(row, "AssetTag");
            var brand = RequireValue(row, "Brand");
            var modelName = RequireValue(row, "Model");
            var purchaseDate = ParseRequiredDate(row, "PurchaseDate");
            var acquisitionCost = ParseRequiredDecimal(row, "AcquisitionCost");

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

            return new AssetCreateVm
            {
                AssetName = assetName,
                AssetTag = assetTag,
                CategoryId = category.Id,
                AssetTypeId = assetType.Id,
                Brand = brand,
                Model = modelName,
                SerialNumber = GetValue(row, "SerialNumber"),
                Description = GetValue(row, "Description"),
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

            var categoryName = GetValue(row, "Category");
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                throw new BusinessException("Category or CategoryId is required.");
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
                throw new BusinessException("AssetType '" + assetTypeName + "' does not belong to category '" + GetValue(row, "Category") + "'.");
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

            Department byName;
            if (!lookups.DepartmentsByName.TryGetValue(NormalizeKey(departmentName), out byName))
            {
                throw new BusinessException("Department '" + departmentName + "' was not found.");
            }

            _departmentScope.EnsureCanAccessDepartment(byName);
            return byName;
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

            public Dictionary<int, Supplier> SuppliersById { get; set; }

            public Dictionary<string, Supplier> SuppliersByName { get; set; }
        }

        private static int? NormalizeOptionalId(int? value)
        {
            return value.HasValue && value.Value > 0 ? value : null;
        }
    }
}

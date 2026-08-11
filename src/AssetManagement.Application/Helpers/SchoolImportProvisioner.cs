using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Creates school departments, categories, and asset types from an import template before asset rows load.
    /// </summary>
    public class SchoolImportProvisioner
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IReferenceDataCache _referenceDataCache;

        public SchoolImportProvisioner(
            IUnitOfWork unitOfWork,
            IOrganizationScopeService organizationScope,
            IReferenceDataCache referenceDataCache)
        {
            _unitOfWork = unitOfWork;
            _organizationScope = organizationScope;
            _referenceDataCache = referenceDataCache;
        }

        public SchoolImportProvisionResult ProvisionFromRows(IList<IDictionary<string, string>> rows, Func<IDictionary<string, string>, string, string> getValue)
        {
            var result = new SchoolImportProvisionResult();
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                throw new BusinessException("Organization context is required for template provisioning.");
            }

            if (rows == null || rows.Count == 0)
            {
                return result;
            }

            var now = DateTime.UtcNow;
            var departments = _unitOfWork.Repository<Department>().GetAll()
                .Where(x => x.IsActive && x.OrganizationId == organizationId.Value)
                .ToList();
            var categories = _unitOfWork.Repository<AssetCategory>().GetAll()
                .Where(x => x.IsActive && x.OrganizationId == organizationId.Value)
                .ToList();
            var assetTypes = _unitOfWork.Repository<AssetType>().GetAll()
                .Where(x => x.IsActive && x.OrganizationId == organizationId.Value)
                .ToList();

            var adminDepartmentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var subDepartmentPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var classCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var supplierNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var categoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var typePairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var categoryName = ResolveCategoryName(row, getValue);
                if (!string.IsNullOrWhiteSpace(categoryName))
                {
                    categoryNames.Add(categoryName.Trim());
                }

                var assetTypeName = getValue(row, "AssetType");
                if (!string.IsNullOrWhiteSpace(categoryName) && !string.IsNullOrWhiteSpace(assetTypeName))
                {
                    typePairs.Add(NormalizeKey(categoryName) + "|" + NormalizeKey(assetTypeName));
                }

                var supplierName = getValue(row, "Supplier");
                if (!string.IsNullOrWhiteSpace(supplierName))
                {
                    supplierNames.Add(supplierName.Trim());
                }

                var departmentName = getValue(row, "Department");
                var classValue = getValue(row, "Class");
                if (SchoolClassCodeHelper.IsClassroomDepartment(departmentName)
                    && !string.IsNullOrWhiteSpace(classValue))
                {
                    var code = SchoolClassCodeHelper.BuildClassDepartmentCode(classValue);
                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        classCodes.Add(code);
                    }
                }
                else if (SchoolDepartmentCodeHelper.IsAdministrativeDepartmentName(departmentName))
                {
                    var normalizedAdminName = SchoolDepartmentCodeHelper.NormalizeAdminDepartmentName(departmentName);
                    adminDepartmentNames.Add(normalizedAdminName);
                    if (!string.IsNullOrWhiteSpace(classValue))
                    {
                        subDepartmentPairs.Add(normalizedAdminName + "|" + classValue.Trim());
                    }
                }
            }

            var adminNamesWithSubUnits = new HashSet<string>(
                subDepartmentPairs
                    .Select(pair => pair.Split('|')[0])
                    .Where(x => !string.IsNullOrWhiteSpace(x)),
                StringComparer.OrdinalIgnoreCase);

            foreach (var adminName in adminDepartmentNames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                EnsureAdminDepartment(
                    departments,
                    organizationId.Value,
                    now,
                    adminName,
                    !adminNamesWithSubUnits.Contains(adminName),
                    result);
            }

            _unitOfWork.SaveChanges();

            foreach (var pair in subDepartmentPairs.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var parts = pair.Split('|');
                if (parts.Length != 2)
                {
                    continue;
                }

                EnsureSubDepartment(
                    departments,
                    organizationId.Value,
                    now,
                    parts[0],
                    parts[1],
                    result);
            }

            foreach (var categoryName in categoryNames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                EnsureCategory(categories, organizationId.Value, now, categoryName, result);
            }

            if (result.CategoriesCreated > 0)
            {
                _unitOfWork.SaveChanges();
            }

            foreach (var pair in typePairs.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var parts = pair.Split('|');
                if (parts.Length != 2)
                {
                    continue;
                }

                var category = categories.FirstOrDefault(x =>
                    string.Equals(NormalizeKey(x.Name), parts[0], StringComparison.OrdinalIgnoreCase));
                if (category == null)
                {
                    continue;
                }

                var typeName = rows
                    .Select(row => getValue(row, "AssetType"))
                    .FirstOrDefault(name => string.Equals(NormalizeKey(name), parts[1], StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    continue;
                }

                EnsureAssetType(assetTypes, category, organizationId.Value, now, typeName.Trim(), result);
            }

            foreach (var classCode in classCodes.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                EnsureClassHierarchy(departments, organizationId.Value, now, classCode, result);
            }

            var suppliers = _unitOfWork.Repository<Supplier>().GetAll()
                .Where(x => x.IsActive && x.OrganizationId == organizationId.Value)
                .ToList();
            foreach (var supplierName in supplierNames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                EnsureSupplier(suppliers, organizationId.Value, now, supplierName, result);
            }

            _unitOfWork.SaveChanges();
            _referenceDataCache.InvalidateDepartments(organizationId.Value);
            return result;
        }

        private static string ResolveCategoryName(IDictionary<string, string> row, Func<IDictionary<string, string>, string, string> getValue)
        {
            var assetCategory = getValue(row, "AssetCategory");
            if (!string.IsNullOrWhiteSpace(assetCategory))
            {
                return assetCategory.Trim();
            }

            var legacyCategory = getValue(row, "Category");
            return string.IsNullOrWhiteSpace(legacyCategory) ? null : legacyCategory.Trim();
        }

        private void EnsureAdminDepartment(
            IList<Department> departments,
            int organizationId,
            DateTime now,
            string name,
            bool isRequisitionTarget,
            SchoolImportProvisionResult result)
        {
            var code = SchoolDepartmentCodeHelper.BuildAdminDepartmentCode(name);
            var existing = departments.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                if (!isRequisitionTarget && existing.IsRequisitionTarget)
                {
                    existing.IsRequisitionTarget = false;
                    existing.UpdatedAt = now;
                    _unitOfWork.Repository<Department>().Update(existing);
                }

                return;
            }

            var entity = new Department
            {
                Name = name,
                Code = code,
                Description = name + " department",
                OrganizationId = organizationId,
                DepartmentKind = DepartmentKind.Administrative,
                IsRequisitionTarget = isRequisitionTarget,
                CreatedAt = now,
                IsActive = true
            };
            _unitOfWork.Repository<Department>().Add(entity);
            departments.Add(entity);
            result.DepartmentsCreated++;
        }

        private void EnsureSubDepartment(
            IList<Department> departments,
            int organizationId,
            DateTime now,
            string parentName,
            string subUnitName,
            SchoolImportProvisionResult result)
        {
            var parentCode = SchoolDepartmentCodeHelper.BuildAdminDepartmentCode(parentName);
            var parent = departments.FirstOrDefault(x => string.Equals(x.Code, parentCode, StringComparison.OrdinalIgnoreCase));
            if (parent == null)
            {
                EnsureAdminDepartment(departments, organizationId, now, parentName, false, result);
                _unitOfWork.SaveChanges();
                parent = departments.FirstOrDefault(x => string.Equals(x.Code, parentCode, StringComparison.OrdinalIgnoreCase));
            }

            if (parent == null)
            {
                return;
            }

            if (parent.IsRequisitionTarget)
            {
                parent.IsRequisitionTarget = false;
                parent.UpdatedAt = now;
                _unitOfWork.Repository<Department>().Update(parent);
            }

            var subCode = SchoolDepartmentCodeHelper.BuildSubDepartmentCode(parent.Code, subUnitName);
            if (departments.Any(x => string.Equals(x.Code, subCode, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var entity = new Department
            {
                Name = subUnitName.Trim(),
                Code = subCode,
                Description = subUnitName.Trim() + " (" + parent.Name + ")",
                OrganizationId = organizationId,
                ParentDepartmentId = parent.Id,
                DepartmentKind = DepartmentKind.SubDepartment,
                IsRequisitionTarget = true,
                CreatedAt = now,
                IsActive = true
            };
            _unitOfWork.Repository<Department>().Add(entity);
            departments.Add(entity);
            result.DepartmentsCreated++;
        }

        private void EnsureCategory(
            IList<AssetCategory> categories,
            int organizationId,
            DateTime now,
            string name,
            SchoolImportProvisionResult result)
        {
            if (categories.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var entity = new AssetCategory
            {
                Name = name,
                OrganizationId = organizationId,
                CreatedAt = now,
                IsActive = true
            };
            _unitOfWork.Repository<AssetCategory>().Add(entity);
            categories.Add(entity);
            result.CategoriesCreated++;
        }

        private void EnsureAssetType(
            IList<AssetType> assetTypes,
            AssetCategory category,
            int organizationId,
            DateTime now,
            string name,
            SchoolImportProvisionResult result)
        {
            if (assetTypes.Any(x =>
                    x.AssetCategoryId == category.Id
                    && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var entity = new AssetType
            {
                Name = name,
                AssetCategoryId = category.Id,
                OrganizationId = organizationId,
                CreatedAt = now,
                IsActive = true
            };
            _unitOfWork.Repository<AssetType>().Add(entity);
            assetTypes.Add(entity);
            result.AssetTypesCreated++;
        }

        private void EnsureClassHierarchy(
            IList<Department> departments,
            int organizationId,
            DateTime now,
            string classCode,
            SchoolImportProvisionResult result)
        {
            if (departments.Any(x => string.Equals(x.Code, classCode, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var gradeDigits = classCode.Length >= 3 ? classCode.Substring(1, 2) : null;
            int grade;
            if (string.IsNullOrWhiteSpace(gradeDigits)
                || !int.TryParse(gradeDigits, out grade)
                || grade < 1
                || grade > SchoolClassCodeHelper.MaxGrade)
            {
                return;
            }

            var stream = classCode.Length > 3 ? classCode.Substring(3) : string.Empty;
            var gradeCode = SchoolClassCodeHelper.BuildGradeDepartmentCode(grade);
            var gradeDept = departments.FirstOrDefault(x =>
                string.Equals(x.Code, gradeCode, StringComparison.OrdinalIgnoreCase));
            if (gradeDept == null)
            {
                gradeDept = new Department
                {
                    Name = SchoolClassCodeHelper.BuildGradeDepartmentName(grade),
                    Code = gradeCode,
                    Description = "Grade " + grade + " container",
                    OrganizationId = organizationId,
                    DepartmentKind = DepartmentKind.Grade,
                    IsRequisitionTarget = false,
                    CreatedAt = now,
                    IsActive = true
                };
                _unitOfWork.Repository<Department>().Add(gradeDept);
                departments.Add(gradeDept);
                result.DepartmentsCreated++;
                _unitOfWork.SaveChanges();
            }

            var classDept = new Department
            {
                Name = SchoolClassCodeHelper.BuildClassDepartmentName(grade, stream),
                Code = classCode,
                Description = "Class " + grade + stream,
                OrganizationId = organizationId,
                ParentDepartmentId = gradeDept.Id,
                DepartmentKind = DepartmentKind.Class,
                IsRequisitionTarget = true,
                CreatedAt = now,
                IsActive = true
            };
            _unitOfWork.Repository<Department>().Add(classDept);
            departments.Add(classDept);
            result.DepartmentsCreated++;
        }

        private void EnsureSupplier(
            IList<Supplier> suppliers,
            int organizationId,
            DateTime now,
            string name,
            SchoolImportProvisionResult result)
        {
            if (suppliers.Any(x => string.Equals(x.SupplierName, name, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var entity = new Supplier
            {
                SupplierName = name,
                OrganizationId = organizationId,
                CreatedAt = now,
                IsActive = true
            };
            _unitOfWork.Repository<Supplier>().Add(entity);
            suppliers.Add(entity);
            result.SuppliersCreated++;
        }

        private static string NormalizeKey(string value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }
    }

    public class SchoolImportProvisionResult
    {
        public int DepartmentsCreated { get; set; }

        public int CategoriesCreated { get; set; }

        public int AssetTypesCreated { get; set; }

        public int SuppliersCreated { get; set; }
    }
}

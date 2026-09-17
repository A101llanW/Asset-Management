using System;
using System.Collections.Generic;
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
    public class DepartmentService : IDepartmentService
    {
        public const string SetupModeNormal = "Normal";
        public const string SetupModeSubDepartment = "SubDepartment";
        public const string SetupModeGradeStreams = "GradeWithStreams";
        public const string SetupModeBulkGrades = "BulkGrades";

        private readonly IUnitOfWork _unitOfWork;
        private readonly IDepartmentScopeService _departmentScope;
        private readonly IReferenceDataCache _referenceDataCache;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IAuditWriter _auditWriter;

        public DepartmentService(
            IUnitOfWork unitOfWork,
            IDepartmentScopeService departmentScope,
            IReferenceDataCache referenceDataCache,
            IOrganizationScopeService organizationScope,
            IAuditWriter auditWriter = null)
        {
            _unitOfWork = unitOfWork;
            _departmentScope = departmentScope;
            _referenceDataCache = referenceDataCache;
            _organizationScope = organizationScope;
            _auditWriter = auditWriter;
        }

        public IEnumerable<DepartmentVm> GetAll()
        {
            return MapDepartments(_departmentScope.ApplyDepartmentScope(_unitOfWork.Repository<Department>().Query())
                .OrderBy(x => x.Name));
        }

        public IEnumerable<DepartmentVm> GetRequisitionTargets()
        {
            var allDepartments = GetAll().Where(x => x.IsActive).ToList();
            var targets = allDepartments.Where(x => x.IsRequisitionTarget).ToList();
            var byId = allDepartments.ToDictionary(x => x.Id);
            foreach (var department in targets.Where(x => x.ParentDepartmentId.HasValue))
            {
                DepartmentVm parent;
                if (byId.TryGetValue(department.ParentDepartmentId.Value, out parent))
                {
                    department.ParentDepartmentName = parent.Name;
                }
            }

            return targets
                .OrderBy(x => x.ParentDepartmentName ?? x.Name)
                .ThenBy(x => x.ParentDepartmentId.HasValue ? 1 : 0)
                .ThenBy(x => x.Name);
        }

        public IEnumerable<DepartmentTreeSectionVm> GetTreeSections()
        {
            return GetTreeSections(null);
        }

        public IEnumerable<DepartmentTreeSectionVm> GetTreeSections(string domain)
        {
            var departments = GetAll().Where(x => x.IsActive).ToList();
            var byId = departments.ToDictionary(x => x.Id);
            foreach (var dept in departments.Where(x => x.ParentDepartmentId.HasValue))
            {
                DepartmentVm parent;
                if (byId.TryGetValue(dept.ParentDepartmentId.Value, out parent))
                {
                    parent.Children.Add(dept);
                }
            }

            foreach (var parent in byId.Values)
            {
                parent.Children = parent.Children.OrderBy(x => x.Code).ToList();
            }

            var includeClasses = string.IsNullOrWhiteSpace(domain)
                || DepartmentHierarchyRules.NormalizeDomain(domain) == DepartmentHierarchyRules.DomainClasses;
            var includeOrg = string.IsNullOrWhiteSpace(domain)
                || DepartmentHierarchyRules.NormalizeDomain(domain) == DepartmentHierarchyRules.DomainOrg;

            var sections = new List<DepartmentTreeSectionVm>();
            if (includeClasses)
            {
                sections.AddRange(BuildClassTreeSections(departments));
            }

            if (includeOrg)
            {
                sections.AddRange(BuildAdminTreeSections(departments));
            }

            return sections;
        }

        private static IEnumerable<DepartmentTreeSectionVm> BuildClassTreeSections(IList<DepartmentVm> departments)
        {
            var sections = new List<DepartmentTreeSectionVm>();
            foreach (var gradeParent in departments
                .Where(x => x.DepartmentKind == DepartmentKind.Grade)
                .OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase))
            {
                sections.Add(new DepartmentTreeSectionVm
                {
                    Title = gradeParent.Name,
                    Items = new List<DepartmentVm> { gradeParent }
                });
            }

            var ungrouped = departments
                .Where(x => !x.ParentDepartmentId.HasValue
                    && x.DepartmentKind == DepartmentKind.Class)
                .OrderBy(x => x.Name)
                .ToList();
            if (ungrouped.Any())
            {
                sections.Add(new DepartmentTreeSectionVm
                {
                    Title = "Other",
                    Items = ungrouped
                });
            }

            return sections;
        }

        private static IEnumerable<DepartmentTreeSectionVm> BuildAdminTreeSections(IList<DepartmentVm> departments)
        {
            var sections = new List<DepartmentTreeSectionVm>();
            var adminItems = departments
                .Where(x => x.DepartmentKind == DepartmentKind.Administrative && !x.ParentDepartmentId.HasValue)
                .OrderBy(x => x.Name)
                .ToList();
            if (adminItems.Any())
            {
                sections.Add(new DepartmentTreeSectionVm
                {
                    Title = "Administration",
                    Items = adminItems
                });
            }

            var ungrouped = departments
                .Where(x => !x.ParentDepartmentId.HasValue
                    && x.DepartmentKind == DepartmentKind.SubDepartment)
                .OrderBy(x => x.Name)
                .ToList();
            if (ungrouped.Any())
            {
                sections.Add(new DepartmentTreeSectionVm
                {
                    Title = "Other",
                    Items = ungrouped
                });
            }

            return sections;
        }

        public DepartmentVm GetById(int id)
        {
            var entity = _unitOfWork.Repository<Department>().GetById(id);
            if (entity == null)
            {
                return null;
            }

            var model = MapDepartment(entity);
            if (entity.ParentDepartmentId.HasValue)
            {
                var parent = _unitOfWork.Repository<Department>().GetById(entity.ParentDepartmentId.Value);
                if (parent != null)
                {
                    model.ParentDepartmentName = parent.Name;
                }
            }

            return model;
        }

        public int Create(DepartmentVm model)
        {
            var entity = BuildEntity(model);
            entity.CreatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Department>().Add(entity);
            _unitOfWork.SaveChanges();
            InvalidateDepartmentCache();
            WriteDepartmentAudit("Departments.Create", entity.Id.ToString(), null, entity.Name);
            return entity.Id;
        }

        public int CreateFromWizard(DepartmentCreateVm model)
        {
            if (model == null)
            {
                throw new BusinessException("Department details are required.");
            }

            var setupMode = (model.SetupMode ?? SetupModeNormal).Trim();
            switch (setupMode)
            {
                case SetupModeSubDepartment:
                    return CreateSubDepartment(model);
                case SetupModeGradeStreams:
                    return CreateGradeWithStreams(model);
                case SetupModeBulkGrades:
                    return CreateBulkGrades(model);
                default:
                    return CreateNormal(model);
            }
        }

        public void Update(DepartmentVm model)
        {
            var entity = _unitOfWork.Repository<Department>().GetById(model.Id);
            if (entity == null)
            {
                return;
            }

            DepartmentHierarchyRules.AssertKindUnchanged(entity.DepartmentKind, model.DepartmentKind);

            // Parent is not editable on the Edit form — preserve existing hierarchy.
            Department parent = null;
            if (entity.ParentDepartmentId.HasValue)
            {
                parent = _unitOfWork.Repository<Department>().GetById(entity.ParentDepartmentId.Value);
            }

            DepartmentHierarchyRules.AssertValidHierarchy(entity.DepartmentKind, parent);

            if (entity.DepartmentKind == DepartmentKind.Grade)
            {
                model.IsRequisitionTarget = false;
            }

            entity.Name = model.Name;
            entity.Code = model.Code;
            entity.Description = model.Description;
            entity.IsRequisitionTarget = model.IsRequisitionTarget;
            entity.IsActive = model.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Repository<Department>().Update(entity);
            _unitOfWork.SaveChanges();
            InvalidateDepartmentCache();
            WriteDepartmentAudit("Departments.Edit", entity.Id.ToString(), null, entity.Name);
        }

        private void WriteDepartmentAudit(string action, string entityId, string oldValues, string newValues)
        {
            _auditWriter?.Write(action, nameof(Department), entityId, oldValues, newValues);
        }

        private int CreateSubDepartment(DepartmentCreateVm model)
        {
            if (!model.ParentDepartmentId.HasValue || model.ParentDepartmentId.Value <= 0)
            {
                throw new BusinessException("Select the parent administrative department.");
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                throw new BusinessException("Name is required.");
            }

            var parent = _unitOfWork.Repository<Department>().GetById(model.ParentDepartmentId.Value);
            if (parent == null || !parent.IsActive)
            {
                throw new BusinessException("Parent department was not found.");
            }

            DepartmentHierarchyRules.AssertValidHierarchy(DepartmentKind.SubDepartment, parent);

            var now = DateTime.UtcNow;
            if (parent.IsRequisitionTarget)
            {
                parent.IsRequisitionTarget = false;
                parent.UpdatedAt = now;
                _unitOfWork.Repository<Department>().Update(parent);
            }

            var subCode = SchoolDepartmentCodeHelper.BuildSubDepartmentCode(parent.Code, model.Name.Trim());
            if (_unitOfWork.Repository<Department>().GetAll().Any(x =>
                    x.IsActive && string.Equals(x.Code, subCode, StringComparison.OrdinalIgnoreCase)))
            {
                throw new BusinessException("A sub-unit with code '" + subCode + "' already exists.");
            }

            var entity = new Department
            {
                Name = model.Name.Trim(),
                Code = subCode,
                Description = string.IsNullOrWhiteSpace(model.Description)
                    ? model.Name.Trim() + " (" + parent.Name + ")"
                    : model.Description.Trim(),
                ParentDepartmentId = parent.Id,
                DepartmentKind = DepartmentKind.SubDepartment,
                IsRequisitionTarget = true,
                IsActive = true,
                CreatedAt = now
            };
            ApplyOrganization(entity);
            _unitOfWork.Repository<Department>().Add(entity);
            _unitOfWork.SaveChanges();
            InvalidateDepartmentCache();
            WriteDepartmentAudit("Departments.Create", entity.Id.ToString(), null, entity.Name);
            return entity.Id;
        }

        private int CreateNormal(DepartmentCreateVm model)
        {
            ValidateRequiredNameAndCode(model);
            var entity = new Department
            {
                Name = model.Name.Trim(),
                Code = model.Code.Trim().ToUpperInvariant(),
                Description = model.Description,
                DepartmentKind = DepartmentKind.Administrative,
                IsRequisitionTarget = model.IsRequisitionTarget,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            ApplyOrganization(entity);
            _unitOfWork.Repository<Department>().Add(entity);
            _unitOfWork.SaveChanges();
            InvalidateDepartmentCache();
            WriteDepartmentAudit("Departments.Create", entity.Id.ToString(), null, entity.Name);
            return entity.Id;
        }

        private int CreateGradeWithStreams(DepartmentCreateVm model)
        {
            if (!model.GradeNumber.HasValue
                || model.GradeNumber.Value < SchoolClassCodeHelper.MinGrade
                || model.GradeNumber.Value > SchoolClassCodeHelper.MaxGrade)
            {
                throw new BusinessException(
                    "Grade must be between " + SchoolClassCodeHelper.MinGrade + " and " + SchoolClassCodeHelper.MaxGrade + ".");
            }

            var streamTokens = ParseStreams(model.SelectedStreams);
            if (streamTokens.Count == 0)
            {
                throw new BusinessException("Select at least one stream.");
            }

            var grade = model.GradeNumber.Value;
            var now = DateTime.UtcNow;
            var gradeEntity = EnsureGradeParent(grade, now);
            var firstClassId = 0;
            foreach (var token in streamTokens)
            {
                int streamGrade;
                string stream;
                if (!SchoolClassCodeHelper.TryResolveStreamEntry(grade, token, out streamGrade, out stream))
                {
                    throw new BusinessException("Stream '" + token + "' is not valid.");
                }

                var streamGradeEntity = streamGrade == grade
                    ? gradeEntity
                    : EnsureGradeParent(streamGrade, now);
                var classEntity = BuildClassEntity(streamGrade, stream, streamGradeEntity.Id, now);
                _unitOfWork.Repository<Department>().Add(classEntity);
                _unitOfWork.SaveChanges();
                WriteDepartmentAudit("Departments.Create", classEntity.Id.ToString(), null, classEntity.Name);
                if (firstClassId <= 0)
                {
                    firstClassId = classEntity.Id;
                }
            }

            InvalidateDepartmentCache();
            return firstClassId > 0 ? firstClassId : gradeEntity.Id;
        }

        private int CreateBulkGrades(DepartmentCreateVm model)
        {
            var from = model.BulkGradeFrom;
            var to = model.BulkGradeTo;
            if (from < SchoolClassCodeHelper.MinGrade
                || to > SchoolClassCodeHelper.MaxGrade
                || from > to)
            {
                throw new BusinessException(
                    "Bulk grade range must be between "
                    + SchoolClassCodeHelper.MinGrade
                    + " and "
                    + SchoolClassCodeHelper.MaxGrade
                    + ", with From less than or equal to To.");
            }

            var streamTokens = ParseStreams(model.BulkStreams);
            if (streamTokens.Count == 0)
            {
                throw new BusinessException("Select at least one stream.");
            }

            var now = DateTime.UtcNow;
            var firstId = 0;
            for (var grade = from; grade <= to; grade++)
            {
                var gradeEntity = EnsureGradeParent(grade, now);
                foreach (var token in streamTokens)
                {
                    int streamGrade;
                    string stream;
                    if (!SchoolClassCodeHelper.TryResolveStreamEntry(grade, token, out streamGrade, out stream))
                    {
                        continue;
                    }

                    if (streamGrade < from || streamGrade > to)
                    {
                        continue;
                    }

                    var streamGradeEntity = streamGrade == grade
                        ? gradeEntity
                        : EnsureGradeParent(streamGrade, now);
                    var code = SchoolClassCodeHelper.BuildClassDepartmentCode(streamGrade, stream);
                    if (_unitOfWork.Repository<Department>().GetAll().Any(x =>
                            x.IsActive && string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var classEntity = BuildClassEntity(streamGrade, stream, streamGradeEntity.Id, now);
                    _unitOfWork.Repository<Department>().Add(classEntity);
                    _unitOfWork.SaveChanges();
                    WriteDepartmentAudit("Departments.Create", classEntity.Id.ToString(), null, classEntity.Name);
                    if (firstId <= 0)
                    {
                        firstId = classEntity.Id;
                    }
                }
            }

            InvalidateDepartmentCache();
            return firstId;
        }

        private Department EnsureGradeParent(int grade, DateTime now)
        {
            var gradeCode = SchoolClassCodeHelper.BuildGradeDepartmentCode(grade);
            var existing = _unitOfWork.Repository<Department>().GetAll()
                .FirstOrDefault(x => x.IsActive && string.Equals(x.Code, gradeCode, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing;
            }

            var gradeEntity = new Department
            {
                Name = SchoolClassCodeHelper.BuildGradeDepartmentName(grade),
                Code = gradeCode,
                Description = "Grade " + grade + " container",
                DepartmentKind = DepartmentKind.Grade,
                IsRequisitionTarget = false,
                IsActive = true,
                CreatedAt = now
            };
            ApplyOrganization(gradeEntity);
            _unitOfWork.Repository<Department>().Add(gradeEntity);
            _unitOfWork.SaveChanges();
            WriteDepartmentAudit("Departments.Create", gradeEntity.Id.ToString(), null, gradeEntity.Name);
            return gradeEntity;
        }

        private Department BuildClassEntity(int grade, string stream, int parentId, DateTime now)
        {
            var entity = new Department
            {
                Name = SchoolClassCodeHelper.BuildClassDepartmentName(grade, stream),
                Code = SchoolClassCodeHelper.BuildClassDepartmentCode(grade, stream),
                Description = "Stream " + SchoolClassCodeHelper.BuildStreamLabel(grade, stream),
                ParentDepartmentId = parentId,
                DepartmentKind = DepartmentKind.Class,
                IsRequisitionTarget = true,
                IsActive = true,
                CreatedAt = now
            };
            ApplyOrganization(entity);
            return entity;
        }

        private static IList<string> ParseStreams(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<string>();
            }

            return raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().ToUpperInvariant())
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void ValidateRequiredNameAndCode(DepartmentCreateVm model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                throw new BusinessException("Name is required.");
            }

            if (string.IsNullOrWhiteSpace(model.Code))
            {
                throw new BusinessException("Code is required.");
            }
        }

        private void ApplyOrganization(Department entity)
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (organizationId.HasValue)
            {
                entity.OrganizationId = organizationId.Value;
            }
        }

        private void InvalidateDepartmentCache()
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (organizationId.HasValue)
            {
                _referenceDataCache.InvalidateDepartments(organizationId.Value);
            }
        }

        private static Department BuildEntity(DepartmentVm model)
        {
            return new Department
            {
                Name = model.Name,
                Code = model.Code,
                Description = model.Description,
                ParentDepartmentId = model.ParentDepartmentId,
                DepartmentKind = model.DepartmentKind,
                IsRequisitionTarget = model.IsRequisitionTarget,
                IsActive = model.IsActive
            };
        }

        private static IEnumerable<DepartmentVm> MapDepartments(IEnumerable<Department> entities)
        {
            return entities.Select(MapDepartment).ToList();
        }

        private static DepartmentVm MapDepartment(Department entity)
        {
            return new DepartmentVm
            {
                Id = entity.Id,
                Name = entity.Name,
                Code = entity.Code,
                Description = entity.Description,
                ParentDepartmentId = entity.ParentDepartmentId,
                DepartmentKind = entity.DepartmentKind,
                IsRequisitionTarget = entity.IsRequisitionTarget,
                IsActive = entity.IsActive
            };
        }
    }
}

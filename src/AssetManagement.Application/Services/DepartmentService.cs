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
        public const string SetupModeNormal = DepartmentSetupModes.Normal;
        public const string SetupModeSubDepartment = DepartmentSetupModes.SubDepartment;
        public const string SetupModeRoom = DepartmentSetupModes.Room;
        public const string SetupModeGradeStreams = DepartmentSetupModes.GradeStreams;
        public const string SetupModeBulkGrades = DepartmentSetupModes.BulkGrades;

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
            var mapped = MapDepartments(_departmentScope.ApplyDepartmentScope(_unitOfWork.Repository<Department>().Query())
                .OrderBy(x => x.Name));
            return DepartmentListHelper.DeduplicateById(mapped).OrderBy(x => x.Name);
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
            return GetTreeSections(GetAll().Where(x => x.IsActive));
        }

        public IEnumerable<DepartmentTreeSectionVm> GetTreeSections(IEnumerable<DepartmentVm> scopedDepartments)
        {
            var departments = (scopedDepartments ?? Enumerable.Empty<DepartmentVm>()).ToList();
            foreach (var department in departments)
            {
                department.Children = new List<DepartmentVm>();
            }

            DepartmentOrgHierarchyDisplay.AttachGradeClassChildren(departments);

            var sections = new List<DepartmentTreeSectionVm>();
            for (var grade = 1; grade <= SchoolClassCodeHelper.MaxGrade; grade++)
            {
                var gradeCode = SchoolClassCodeHelper.BuildGradeDepartmentCode(grade);
                var gradeParent = departments.FirstOrDefault(x =>
                    x.DepartmentKind == DepartmentKind.Grade
                    && string.Equals(x.Code, gradeCode, StringComparison.OrdinalIgnoreCase));
                if (gradeParent == null)
                {
                    continue;
                }

                sections.Add(new DepartmentTreeSectionVm
                {
                    Title = gradeParent.Name,
                    Items = new List<DepartmentVm> { gradeParent }
                });
            }

            var adminItems = DepartmentOrgHierarchyDisplay.BuildAdministrativeSectionItems(departments);
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
                    && x.DepartmentKind != DepartmentKind.Grade
                    && x.DepartmentKind != DepartmentKind.Administrative
                    && x.DepartmentKind != DepartmentKind.SubDepartment)
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

            return MapDepartment(entity);
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

            EnforceLockedParent(model);

            var setupMode = (model.SetupMode ?? SetupModeNormal).Trim();
            switch (setupMode)
            {
                case SetupModeSubDepartment:
                    return CreateSubDepartment(model);
                case SetupModeRoom:
                    return CreateRoom(model);
                case SetupModeGradeStreams:
                    return CreateGradeWithStreams(model);
                case SetupModeBulkGrades:
                    return CreateBulkGrades(model);
                default:
                    return CreateNormal(model);
            }
        }

        private static void EnforceLockedParent(DepartmentCreateVm model)
        {
            if (!model.LockedParentDepartmentId.HasValue || model.LockedParentDepartmentId.Value <= 0)
            {
                return;
            }

            if (!model.ParentDepartmentId.HasValue
                || model.ParentDepartmentId.Value != model.LockedParentDepartmentId.Value)
            {
                throw new BusinessException("Parent department cannot be changed for this create flow.");
            }
        }

        public void Update(DepartmentVm model)
        {
            var entity = _unitOfWork.Repository<Department>().GetById(model.Id);
            if (entity == null)
            {
                return;
            }

            var allDepartments = _unitOfWork.Repository<Department>().GetAll().ToList();
            DepartmentHierarchyRules.AssertValidHierarchy(
                model.Id,
                model.DepartmentKind,
                model.ParentDepartmentId,
                allDepartments);

            entity.Name = model.Name;
            entity.Code = model.Code;
            entity.Description = model.Description;
            entity.ParentDepartmentId = model.ParentDepartmentId;
            entity.DepartmentKind = model.DepartmentKind;
            entity.IsRequisitionTarget = model.IsRequisitionTarget;
            entity.IsActive = model.IsActive;
            DepartmentRequisitionApprovalSettingsHelper.ApplyToDepartment(entity, model);
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

            if (!DepartmentHierarchyRules.CanCreateSubDepartmentUnder(parent))
            {
                throw new BusinessException("Sub-departments can only be created under top-level administrative departments.");
            }

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
                throw new BusinessException("A sub-department with code '" + subCode + "' already exists.");
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

        private int CreateRoom(DepartmentCreateVm model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                throw new BusinessException("Name is required.");
            }

            var now = DateTime.UtcNow;
            if (!model.ParentDepartmentId.HasValue || model.ParentDepartmentId.Value <= 0)
            {
                ValidateRequiredNameAndCode(model);
                var independentCode = model.Code.Trim().ToUpperInvariant();
                if (_unitOfWork.Repository<Department>().GetAll().Any(x =>
                        x.IsActive && string.Equals(x.Code, independentCode, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new BusinessException("A room with code '" + independentCode + "' already exists.");
                }

                var independentEntity = new Department
                {
                    Name = model.Name.Trim(),
                    Code = independentCode,
                    Description = model.Description,
                    DepartmentKind = DepartmentKind.Room,
                    IsRequisitionTarget = model.IsRequisitionTarget,
                    IsActive = true,
                    CreatedAt = now
                };
                ApplyOrganization(independentEntity);
                _unitOfWork.Repository<Department>().Add(independentEntity);
                _unitOfWork.SaveChanges();
                InvalidateDepartmentCache();
                WriteDepartmentAudit("Departments.Create", independentEntity.Id.ToString(), null, independentEntity.Name);
                return independentEntity.Id;
            }

            var parent = _unitOfWork.Repository<Department>().GetById(model.ParentDepartmentId.Value);
            if (!DepartmentHierarchyRules.IsValidRoomParentDepartment(parent))
            {
                throw new BusinessException("Rooms with a parent must be created under an administrative department or a sub-department.");
            }

            var roomCode = SchoolDepartmentCodeHelper.BuildRoomCode(parent.Code, model.Name.Trim());
            if (_unitOfWork.Repository<Department>().GetAll().Any(x =>
                    x.IsActive && string.Equals(x.Code, roomCode, StringComparison.OrdinalIgnoreCase)))
            {
                throw new BusinessException("A room with code '" + roomCode + "' already exists.");
            }

            var entity = new Department
            {
                Name = model.Name.Trim(),
                Code = roomCode,
                Description = string.IsNullOrWhiteSpace(model.Description)
                    ? model.Name.Trim() + " (" + parent.Name + ")"
                    : model.Description.Trim(),
                ParentDepartmentId = parent.Id,
                DepartmentKind = DepartmentKind.Room,
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
                || model.GradeNumber.Value < 1
                || model.GradeNumber.Value > SchoolClassCodeHelper.MaxGrade)
            {
                throw new BusinessException("Grade must be between 1 and " + SchoolClassCodeHelper.MaxGrade + ".");
            }

            var streams = ParseStreams(model.SelectedStreams);
            if (streams.Count == 0)
            {
                throw new BusinessException("Select at least one class stream.");
            }

            var grade = model.GradeNumber.Value;
            var now = DateTime.UtcNow;
            var gradeEntity = EnsureGradeParent(grade, now);
            var firstClassId = 0;
            foreach (var stream in streams)
            {
                var classEntity = BuildClassEntity(grade, stream, gradeEntity.Id, now);
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
            var from = Math.Max(1, model.BulkGradeFrom);
            var to = Math.Min(SchoolClassCodeHelper.MaxGrade, model.BulkGradeTo);
            if (from > to)
            {
                throw new BusinessException("Bulk grade range is invalid.");
            }

            var streams = ParseStreams(model.BulkStreams);
            if (streams.Count == 0)
            {
                throw new BusinessException("Select at least one class stream.");
            }

            var now = DateTime.UtcNow;
            var firstId = 0;
            for (var grade = from; grade <= to; grade++)
            {
                var gradeEntity = EnsureGradeParent(grade, now);
                foreach (var stream in streams)
                {
                    var code = SchoolClassCodeHelper.BuildClassDepartmentCode(grade, stream);
                    if (_unitOfWork.Repository<Department>().GetAll().Any(x =>
                            x.IsActive && string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var classEntity = BuildClassEntity(grade, stream, gradeEntity.Id, now);
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
                Description = "Class " + grade + stream.Trim().ToUpperInvariant(),
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
                IsActive = entity.IsActive,
                UseCustomRequisitionApproval = entity.UseCustomRequisitionApproval
            };
        }
    }
}

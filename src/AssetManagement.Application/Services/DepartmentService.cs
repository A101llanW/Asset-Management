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
        public const string SetupModeRoom = "Room";
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
            // One org section (all Admin roots) + optional Independent rooms -- Index renders a single panel.
            var sections = new List<DepartmentTreeSectionVm>();
            var topLevel = departments
                .Where(x => x.DepartmentKind == DepartmentKind.Administrative && !x.ParentDepartmentId.HasValue)
                .OrderBy(x => x.Name)
                .ToList();
            if (topLevel.Any())
            {
                sections.Add(new DepartmentTreeSectionVm
                {
                    Title = "Organization",
                    Items = topLevel
                });
            }

            var ungrouped = departments
                .Where(x => !x.ParentDepartmentId.HasValue
                    && (x.DepartmentKind == DepartmentKind.SubDepartment || x.DepartmentKind == DepartmentKind.Room))
                .OrderBy(x => x.Name)
                .ToList();
            if (ungrouped.Any())
            {
                sections.Add(new DepartmentTreeSectionVm
                {
                    Title = "Independent rooms",
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

            model.EffectiveRequisitionFlowSummary = BuildEffectiveRequisitionFlowSummary(entity);
            model.CustomStages = ApprovalWorkflowSettingsHelper.CreateStageSettings(
                ApprovalWorkflowSettingsHelper.ParseStageRoleIds(entity.CustomStageRoleIds),
                ApprovalWorkflowSettingsHelper.ParseStageUserIds(entity.CustomStageUserIds),
                ensureBlankRowWhenEmpty: true);

            return model;
        }

        
        public IEnumerable<DepartmentVm> GetRoomRequisitionFlows()
        {
            // Product: EVERY org requisition leaf — Kind=Room plus SubDept/Admin IsRequisitionTarget leaves
            // (historical trees used SubDept as operational "rooms"; Kind=Room alone hid them).
            var rooms = _departmentScope.ApplyDepartmentScope(_unitOfWork.Repository<Department>().Query())
                .Where(x => x.DepartmentKind == DepartmentKind.Room
                    || (x.IsRequisitionTarget
                        && (x.DepartmentKind == DepartmentKind.SubDepartment
                            || x.DepartmentKind == DepartmentKind.Administrative)))
                .OrderBy(x => x.Name)
                .ToList();

            var parentIds = rooms
                .Where(x => x.ParentDepartmentId.HasValue)
                .Select(x => x.ParentDepartmentId.Value)
                .Distinct()
                .ToList();

            var parentsById = parentIds.Count == 0
                ? new Dictionary<int, Department>()
                : _unitOfWork.Repository<Department>().Query()
                    .Where(x => parentIds.Contains(x.Id))
                    .ToDictionary(x => x.Id);

            var result = new List<DepartmentVm>();
            foreach (var entity in rooms)
            {
                var model = MapDepartment(entity);
                if (entity.ParentDepartmentId.HasValue)
                {
                    Department parent;
                    if (parentsById.TryGetValue(entity.ParentDepartmentId.Value, out parent) && parent != null)
                    {
                        model.ParentDepartmentName = parent.Name;
                    }
                }

                model.EffectiveRequisitionFlowSummary = BuildEffectiveRequisitionFlowSummary(entity);
                result.Add(model);
            }

            return result
                .OrderBy(x => x.ParentDepartmentName ?? string.Empty)
                .ThenBy(x => x.Name)
                .ToList();
        }

        public IEnumerable<DepartmentVm> GetOrganizationalParentCandidates(int excludeDepartmentId)
        {
            var all = GetAll().Where(x => x.IsActive).ToList();
            return all.Where(x =>
                    (x.DepartmentKind == DepartmentKind.Administrative
                        || x.DepartmentKind == DepartmentKind.SubDepartment)
                    && x.Id != excludeDepartmentId
                    && !DepartmentHierarchyRules.WouldCreateCycle(excludeDepartmentId, x.Id, id =>
                    {
                        var match = all.FirstOrDefault(d => d.Id == id);
                        return match == null ? (int?)null : match.ParentDepartmentId;
                    }))
                .OrderBy(x => x.Name);
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

        public void Update(DepartmentVm model)
        {
            var entity = _unitOfWork.Repository<Department>().GetById(model.Id);
            if (entity == null)
            {
                return;
            }

            var requestedKind = model.DepartmentKind;
            var requestedParentId = model.ParentDepartmentId;

            if (DepartmentHierarchyRules.IsAcademic(entity.DepartmentKind))
            {
                requestedParentId = entity.ParentDepartmentId;
                requestedKind = entity.DepartmentKind;
            }
            else if (requestedParentId.HasValue && requestedParentId.Value > 0
                && entity.DepartmentKind == DepartmentKind.Administrative
                && entity.DepartmentKind != DepartmentKind.Room)
            {
                var hasChildren = _unitOfWork.Repository<Department>().GetAll()
                    .Any(x => x.IsActive && x.ParentDepartmentId == entity.Id);
                if (hasChildren)
                {
                    throw new BusinessException("This department has sub-units or rooms. Move or remove them before converting it to a room.");
                }

                DepartmentHierarchyRules.AssertCanConvertToRoom(entity);
                requestedKind = DepartmentKind.Room;
            }
            else
            {
                DepartmentHierarchyRules.AssertKindUnchanged(entity.DepartmentKind, requestedKind);
            }

            Department parent = null;
            if (requestedParentId.HasValue && requestedParentId.Value > 0)
            {
                if (DepartmentHierarchyRules.WouldCreateCycle(entity.Id, requestedParentId.Value, id =>
                {
                    var node = _unitOfWork.Repository<Department>().GetById(id);
                    return node == null ? (int?)null : node.ParentDepartmentId;
                }))
                {
                    throw new BusinessException("A department cannot sit under itself or one of its rooms.");
                }

                parent = _unitOfWork.Repository<Department>().GetById(requestedParentId.Value);
                if (parent == null || !parent.IsActive)
                {
                    throw new BusinessException("Parent department was not found.");
                }
            }

            entity.DepartmentKind = requestedKind;
            entity.ParentDepartmentId = (requestedParentId.HasValue && requestedParentId.Value > 0)
                ? requestedParentId
                : null;
            DepartmentHierarchyRules.AssertValidHierarchy(entity.DepartmentKind, parent);

            if (entity.DepartmentKind == DepartmentKind.Grade)
            {
                model.IsRequisitionTarget = false;
            }

            if (parent != null && parent.IsRequisitionTarget)
            {
                parent.IsRequisitionTarget = false;
                parent.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Repository<Department>().Update(parent);
            }

            entity.Name = model.Name;
            entity.Code = model.Code;
            entity.Description = model.Description;
            entity.IsRequisitionTarget = model.IsRequisitionTarget;
            if (!DepartmentHierarchyRules.IsAcademic(entity.DepartmentKind))
            {
                entity.RequisitionFlowMode = model.RequisitionFlowMode;
                entity.CustomStageRoleIds = model.RequisitionFlowMode == RequisitionFlowMode.Custom
                    ? model.CustomStageRoleIds
                    : null;
                entity.CustomStageUserIds = model.RequisitionFlowMode == RequisitionFlowMode.Custom
                    ? model.CustomStageUserIds
                    : null;
            }
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

        private int CreateRoom(DepartmentCreateVm model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                throw new BusinessException("Name is required.");
            }

            Department parent = null;
            if (model.ParentDepartmentId.HasValue && model.ParentDepartmentId.Value > 0)
            {
                parent = _unitOfWork.Repository<Department>().GetById(model.ParentDepartmentId.Value);
                if (parent == null || !parent.IsActive)
                {
                    throw new BusinessException("Parent department was not found.");
                }
            }

            DepartmentHierarchyRules.AssertValidHierarchy(DepartmentKind.Room, parent);

            var now = DateTime.UtcNow;
            if (parent != null && parent.IsRequisitionTarget)
            {
                parent.IsRequisitionTarget = false;
                parent.UpdatedAt = now;
                _unitOfWork.Repository<Department>().Update(parent);
            }

            var roomCode = parent != null
                ? SchoolDepartmentCodeHelper.BuildSubDepartmentCode(parent.Code, model.Name.Trim())
                : SchoolDepartmentCodeHelper.BuildSubDepartmentCode("ROOM", model.Name.Trim());
            if (_unitOfWork.Repository<Department>().GetAll().Any(x =>
                    x.IsActive && string.Equals(x.Code, roomCode, StringComparison.OrdinalIgnoreCase)))
            {
                throw new BusinessException("A room with code '" + roomCode + "' already exists.");
            }

            var defaultDescription = parent != null
                ? model.Name.Trim() + " (" + parent.Name + ")"
                : model.Name.Trim();
            var entity = new Department
            {
                Name = model.Name.Trim(),
                Code = roomCode,
                Description = string.IsNullOrWhiteSpace(model.Description)
                    ? defaultDescription
                    : model.Description.Trim(),
                ParentDepartmentId = parent != null ? (int?)parent.Id : null,
                DepartmentKind = DepartmentKind.Room,
                IsRequisitionTarget = true,
                RequisitionFlowMode = RequisitionFlowMode.InheritParent,
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
                RequisitionFlowMode = model.RequisitionFlowMode,
                CustomStageRoleIds = model.CustomStageRoleIds,
                CustomStageUserIds = model.CustomStageUserIds,
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
                RequisitionFlowMode = entity.RequisitionFlowMode,
                CustomStageRoleIds = entity.CustomStageRoleIds,
                CustomStageUserIds = entity.CustomStageUserIds,
                IsActive = entity.IsActive
            };
        }

        private string BuildEffectiveRequisitionFlowSummary(Department entity)
        {
            var orgDefault = ApprovalWorkflowHelper.GetProcessConfiguration(_unitOfWork, ApprovalProcessCodes.Purchase);
            var detailed = DepartmentRequisitionFlowResolver.ResolveDetailed(
                entity,
                id => _unitOfWork.Repository<Department>().GetById(id),
                orgDefault);
            var config = detailed.Configuration ?? orgDefault;
            var roles = config.StageRoleIds ?? new List<int>();
            var roleLookup = _unitOfWork.Repository<Role>().GetAll()
                .Where(x => x != null && x.Id > 0)
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First().Name);
            var stageSummary = roles.Count == 0
                ? "No approvers configured"
                : ApprovalWorkflowSettingsHelper.BuildStageSummary(roles, roleLookup);

            if (entity.RequisitionFlowMode == RequisitionFlowMode.Custom)
            {
                return roles.Count == 0
                    ? "Custom (auto-approve)"
                    : "Custom: " + stageSummary;
            }

            return "Inherit (" + detailed.SourceLabel + "): " + stageSummary;
        }
    }
}

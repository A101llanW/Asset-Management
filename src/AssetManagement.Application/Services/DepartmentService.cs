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
            var entities = _departmentScope.ApplyDepartmentScope(_unitOfWork.Repository<Department>().Query())
                .OrderBy(x => x.Name)
                .ToList();
            var mapped = EnrichMappedDepartments(entities);
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

            return EnrichMappedDepartments(new[] { entity }).FirstOrDefault();
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
            var repository = _unitOfWork.Repository<Department>();
            var entity = repository.GetById(model.Id);
            if (entity == null)
            {
                return;
            }

            var allDepartments = repository.GetAll().ToList();
            DepartmentHierarchyRules.AssertValidHierarchy(
                model.Id,
                model.DepartmentKind,
                model.ParentDepartmentId,
                allDepartments);

            // Kind/Parent change on a unit with ACTIVE children: every child the change would make invalid
            // must get a valid new parent in the same save (server re-validates the whole affected subtree).
            var childMoves = DepartmentKindChangeGuard.NormalizeMoves(model.ChildMoves);
            DepartmentKindChangeGuard.AssertChildrenRemainValid(
                model.Id,
                string.IsNullOrWhiteSpace(model.Name) ? entity.Name : model.Name,
                model.DepartmentKind,
                model.ParentDepartmentId,
                childMoves,
                allDepartments);

            var flowConfigurable = DepartmentHierarchyRules.CanConfigureRequisitionFlow(model.DepartmentKind);
            if (flowConfigurable)
            {
                AssertInheritSourceAvailable(model, allDepartments);
            }

            var oldKind = entity.DepartmentKind;
            var oldParentId = entity.ParentDepartmentId;
            entity.Name = model.Name;
            entity.Code = model.Code;
            entity.Description = model.Description;
            entity.ParentDepartmentId = model.ParentDepartmentId;
            entity.DepartmentKind = model.DepartmentKind;
            entity.IsRequisitionTarget = model.IsRequisitionTarget;
            entity.IsActive = model.IsActive;
            // Optional PIC only — never cascade-reassign assets when teacher/custodian changes.
            if (model.DepartmentKind == DepartmentKind.Class)
            {
                entity.ClassTeacherUserId = string.IsNullOrWhiteSpace(model.ClassTeacherUserId)
                    ? null
                    : model.ClassTeacherUserId.Trim();
                entity.RoomCustodianUserId = null;
            }
            else if (model.DepartmentKind == DepartmentKind.Room)
            {
                entity.RoomCustodianUserId = string.IsNullOrWhiteSpace(model.RoomCustodianUserId)
                    ? null
                    : model.RoomCustodianUserId.Trim();
                entity.ClassTeacherUserId = null;
            }
            else
            {
                entity.ClassTeacherUserId = null;
                entity.RoomCustodianUserId = null;
            }
            if (flowConfigurable)
            {
                DepartmentRequisitionApprovalSettingsHelper.ApplyToDepartment(entity, model);
            }
            else
            {
                entity.RequisitionInheritFrom = null;
            }
            var now = DateTime.UtcNow;
            entity.UpdatedAt = now;

            var movedChildren = new List<KeyValuePair<Department, int?>>();
            _unitOfWork.ExecuteInTransaction(() =>
            {
                repository.Update(entity);
                foreach (var move in childMoves)
                {
                    var child = repository.GetById(move.Key);
                    if (child == null)
                    {
                        throw new BusinessException("Child department #" + move.Key + " was not found.");
                    }

                    var previousParentId = child.ParentDepartmentId;
                    child.ParentDepartmentId = move.Value;
                    child.UpdatedAt = now;
                    repository.Update(child);
                    movedChildren.Add(new KeyValuePair<Department, int?>(child, previousParentId));
                }

                _unitOfWork.SaveChanges();
            });
            InvalidateDepartmentCache();
            WriteDepartmentAudit(
                "Departments.Edit",
                entity.Id.ToString(),
                oldKind != entity.DepartmentKind || oldParentId != entity.ParentDepartmentId
                    ? "Kind=" + oldKind + ";ParentId=" + (oldParentId.HasValue ? oldParentId.Value.ToString() : "-")
                    : null,
                entity.Name);
            foreach (var moved in movedChildren)
            {
                WriteDepartmentAudit(
                    "Departments.MoveChild",
                    moved.Key.Id.ToString(),
                    "ParentId=" + (moved.Value.HasValue ? moved.Value.Value.ToString() : "-"),
                    moved.Key.Name + ";ParentId=" + (moved.Key.ParentDepartmentId.HasValue ? moved.Key.ParentDepartmentId.Value.ToString() : "-")
                        + ";Reason=Kind change on " + entity.Name);
            }
        }

        public DepartmentKindChangeImpactVm GetKindChangeImpact(int id, DepartmentKind newKind, int? newParentId)
        {
            var result = new DepartmentKindChangeImpactVm
            {
                DepartmentId = id,
                NewKindLabel = DepartmentHierarchyRules.DisplayLabel(newKind)
            };
            var entity = _unitOfWork.Repository<Department>().GetById(id);
            if (entity == null)
            {
                return result;
            }

            var allDepartments = _unitOfWork.Repository<Department>().GetAll().ToList();
            foreach (var impact in DepartmentKindChangeGuard.Analyze(id, newKind, newParentId, allDepartments))
            {
                var item = new DepartmentKindChangeImpactItemVm
                {
                    ChildId = impact.Child.Id,
                    ChildName = impact.Child.Name,
                    ChildCode = impact.Child.Code,
                    ChildKindLabel = DepartmentHierarchyRules.DisplayLabel(impact.Child.DepartmentKind),
                    Reason = impact.Reason
                };
                if (impact.AllowNoParent)
                {
                    item.Candidates.Add(new DepartmentParentOptionVm { Id = 0, Label = "No parent (independent)" });
                }

                foreach (var candidate in impact.CandidateParents)
                {
                    item.Candidates.Add(new DepartmentParentOptionVm
                    {
                        Id = candidate.Id,
                        Label = DepartmentHierarchyRules.DisplayLabel(candidate.DepartmentKind) + ": "
                            + DepartmentLabelHelper.FormatCodeName(candidate.Code, candidate.Name)
                    });
                }

                result.Items.Add(item);
            }

            return result;
        }

        public IList<RequisitionInheritOptionVm> GetRequisitionInheritOptions(int departmentId)
        {
            var entity = _unitOfWork.Repository<Department>().GetById(departmentId);
            if (entity == null || !DepartmentHierarchyRules.CanConfigureRequisitionFlow(entity.DepartmentKind))
            {
                return new List<RequisitionInheritOptionVm>();
            }

            var byId = _unitOfWork.Repository<Department>().GetAll()
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First());
            Func<int, Department> getById = id =>
            {
                Department found;
                return byId.TryGetValue(id, out found) ? found : null;
            };

            return DepartmentRequisitionFlowResolver.GetInheritOptions(entity, getById)
                .Select(x => new RequisitionInheritOptionVm
                {
                    Source = x.Source,
                    AncestorId = x.AncestorId,
                    AncestorName = x.AncestorName,
                    Label = (x.Source == RequisitionInheritSource.SubDepartment ? "Sub-department: " : "Department: ")
                        + DepartmentLabelHelper.FormatCodeName(x.AncestorCode, x.AncestorName)
                })
                .ToList();
        }

        private static void AssertInheritSourceAvailable(DepartmentVm model, IList<Department> allDepartments)
        {
            var choice = DepartmentRequisitionFlowResolver.NormalizeInheritChoice(model.RequisitionInheritFrom);
            if (!choice.HasValue
                || model.UseCustomRequisitionApproval
                || model.RequisitionFlowMode == RequisitionFlowMode.Custom
                || model.RequisitionFlowMode == RequisitionFlowMode.AutoApprove)
            {
                return;
            }

            var probe = new Department
            {
                Id = model.Id,
                ParentDepartmentId = model.ParentDepartmentId,
                DepartmentKind = model.DepartmentKind
            };
            var byId = allDepartments.GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First());
            Func<int, Department> getById = id =>
            {
                if (id == probe.Id)
                {
                    return probe;
                }

                Department found;
                return byId.TryGetValue(id, out found) ? found : null;
            };

            if (DepartmentRequisitionFlowResolver.FindInheritAncestor(probe, choice.Value, getById) == null)
            {
                throw new BusinessException(
                    "Inherit flow from '" + (choice.Value == RequisitionInheritSource.SubDepartment ? "Sub-department" : "Department")
                    + "' is not available: this unit has no such ancestor under the selected parent. "
                    + "Choose 'Nearest parent (default)', or save the new parent first and then pick the inherit source.");
            }
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
                    RoomCustodianUserId = string.IsNullOrWhiteSpace(model.RoomCustodianUserId) ? null : model.RoomCustodianUserId.Trim(),
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
                RoomCustodianUserId = string.IsNullOrWhiteSpace(model.RoomCustodianUserId) ? null : model.RoomCustodianUserId.Trim(),
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
            var mode = entity.RequisitionFlowMode;
            if (mode != RequisitionFlowMode.AutoApprove
                && (entity.UseCustomRequisitionApproval || mode == RequisitionFlowMode.Custom))
            {
                mode = RequisitionFlowMode.Custom;
            }

            return new DepartmentVm
            {
                Id = entity.Id,
                Name = entity.Name,
                Code = entity.Code,
                Description = entity.Description,
                ParentDepartmentId = entity.ParentDepartmentId,
                DepartmentKind = entity.DepartmentKind,
                IsRequisitionTarget = entity.IsRequisitionTarget,
                RequisitionFlowMode = mode,
                RequisitionInheritFrom = mode == RequisitionFlowMode.InheritParent
                    ? DepartmentRequisitionFlowResolver.NormalizeInheritChoice(entity.RequisitionInheritFrom)
                    : null,
                IsActive = entity.IsActive,
                UseCustomRequisitionApproval = entity.UseCustomRequisitionApproval
                    || mode == RequisitionFlowMode.Custom,
                ClassTeacherUserId = entity.ClassTeacherUserId,
                RoomCustodianUserId = entity.RoomCustodianUserId
            };
        }

        private IList<DepartmentVm> EnrichMappedDepartments(IEnumerable<Department> entities)
        {
            var list = (entities ?? Enumerable.Empty<Department>()).Where(x => x != null).ToList();
            if (list.Count == 0)
            {
                return new List<DepartmentVm>();
            }

            var walkById = _unitOfWork.Repository<Department>().GetAll()
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First());
            Func<int, Department> getById = id =>
            {
                Department found;
                return walkById.TryGetValue(id, out found) ? found : null;
            };

            var orgDefault = ApprovalWorkflowHelper.GetProcessConfiguration(_unitOfWork, ApprovalProcessCodes.Purchase);
            var roleLookup = _unitOfWork.Repository<Role>().GetAll()
                .Where(x => x != null && x.Id > 0)
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First().Name);

            var result = new List<DepartmentVm>(list.Count);
            foreach (var entity in list)
            {
                var model = MapDepartment(entity);
                ApplyEffectiveFlowFields(model, entity, getById, orgDefault, roleLookup);
                result.Add(model);
            }

            return result;
        }

        private static void ApplyEffectiveFlowFields(
            DepartmentVm model,
            Department entity,
            Func<int, Department> getById,
            ApprovalProcessConfiguration orgDefault,
            IDictionary<int, string> roleLookup)
        {
            if (model == null || entity == null)
            {
                return;
            }

            if (!DepartmentHierarchyRules.ShowsRequisitionFlowColumns(entity.DepartmentKind))
            {
                model.EffectiveRequisitionFlowSource = "-";
                model.EffectiveRequisitionFlowSummary = "-";
                return;
            }

            var detailed = DepartmentRequisitionFlowResolver.ResolveDetailed(entity, getById, orgDefault);
            model.EffectiveRequisitionFlowSource = detailed.SourceLabel;
            var config = detailed.Configuration ?? orgDefault;
            var roles = config.StageRoleIds ?? new List<int>();
            var stageSummary = roles.Count == 0
                ? "No approvers configured"
                : ApprovalWorkflowSettingsHelper.BuildStageSummary(roles, roleLookup);

            if (model.RequisitionFlowMode == RequisitionFlowMode.Custom
                || model.RequisitionFlowMode == RequisitionFlowMode.AutoApprove)
            {
                model.EffectiveRequisitionFlowSummary = roles.Count == 0
                    ? "Custom (auto-approve)"
                    : "Custom: " + stageSummary;
            }
            else
            {
                // Show the effective source, e.g. "Inherit - from Sub: X | ..." / "Inherit - from Dept: Y | ...".
                model.EffectiveRequisitionFlowSummary = detailed.InheritSourceDisplay + " | " + stageSummary;
            }
        }
    }
}

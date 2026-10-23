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
        public const string SetupModeGrade = DepartmentSetupModes.Grade;
        public const string SetupModeClass = DepartmentSetupModes.Class;

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

            // Section order: Prep first, then Senior school. Inside a section: its grades (by sort number, not by a
            // hard-coded G01..G99 list, so Prep G00 and any non-G## grade code are shown), Administration, Other.
            var adminItems = DepartmentOrgHierarchyDisplay.BuildAdministrativeSectionItems(departments);
            var ungrouped = departments
                .Where(x => !x.ParentDepartmentId.HasValue
                    && x.DepartmentKind != DepartmentKind.Grade
                    && x.DepartmentKind != DepartmentKind.Administrative
                    && x.DepartmentKind != DepartmentKind.SubDepartment)
                .OrderBy(x => x.Name)
                .ToList();

            var sections = new List<DepartmentTreeSectionVm>();
            foreach (var section in new[] { DepartmentSection.Prep, DepartmentSection.SeniorSchool })
            {
                var current = section;
                var grades = departments
                    .Where(x => x.DepartmentKind == DepartmentKind.Grade
                        && DepartmentSectionHelper.Normalize(x.Section) == current)
                    .OrderBy(x => DepartmentSectionHelper.EffectiveSortNumber(x.SortOrder, x.Code))
                    .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.Id)
                    .ToList();
                foreach (var gradeParent in grades)
                {
                    sections.Add(new DepartmentTreeSectionVm
                    {
                        Title = gradeParent.Name,
                        SectionGroup = current,
                        Items = new List<DepartmentVm> { gradeParent }
                    });
                }

                var sectionAdmins = adminItems
                    .Select(x => ProjectAdministrativeToSection(x, current))
                    .Where(x => x != null)
                    .ToList();
                if (sectionAdmins.Any())
                {
                    sections.Add(new DepartmentTreeSectionVm
                    {
                        Title = "Administration",
                        SectionGroup = current,
                        Items = sectionAdmins
                    });
                }

                var sectionOther = ungrouped
                    .Where(x => DepartmentSectionHelper.Normalize(x.Section) == current)
                    .ToList();
                if (sectionOther.Any())
                {
                    sections.Add(new DepartmentTreeSectionVm
                    {
                        Title = "Other",
                        SectionGroup = current,
                        Items = sectionOther
                    });
                }
            }

            return sections;
        }

        /// <summary>
        /// The administrative department as it appears inside one Section: its own children/rooms that belong to that Section.
        /// Returns the same object when nothing is filtered out (the usual all-Senior case), a trimmed copy when a
        /// department holds rooms of both sections (e.g. Prep rooms under a Senior administrative department),
        /// and null when neither the department nor anything under it is in the Section.
        /// </summary>
        private static DepartmentVm ProjectAdministrativeToSection(DepartmentVm admin, DepartmentSection current)
        {
            var adminIn = DepartmentSectionHelper.Normalize(admin.Section) == current;
            var kept = new List<DepartmentVm>();
            var changed = false;
            foreach (var child in admin.Children ?? new List<DepartmentVm>())
            {
                var item = child;
                var childIn = DepartmentSectionHelper.Normalize(child.Section) == current;
                if (child.DepartmentKind == DepartmentKind.SubDepartment)
                {
                    var rooms = (child.Children ?? new List<DepartmentVm>())
                        .Where(r => DepartmentSectionHelper.Normalize(r.Section) == current)
                        .ToList();
                    if (!childIn && rooms.Count == 0)
                    {
                        changed = true;
                        continue;
                    }

                    if (rooms.Count != (child.Children == null ? 0 : child.Children.Count))
                    {
                        item = child.ShallowCopy();
                        item.Children = rooms;
                        changed = true;
                    }
                }
                else if (!childIn)
                {
                    changed = true;
                    continue;
                }

                kept.Add(item);
            }

            if (!adminIn && kept.Count == 0)
            {
                return null;
            }

            if (!changed)
            {
                return admin;
            }

            var copy = admin.ShallowCopy();
            copy.Children = kept;
            return copy;
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
            var createOrganizationId = _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId();
            if (createOrganizationId.HasValue && model != null)
            {
                NewReferenceGuard().EnsureDepartmentInOrganization(
                    createOrganizationId.Value, model.ParentDepartmentId, "Parent unit", "Departments.Create", nameof(Department), null);
            }

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
                case SetupModeGrade:
                    return CreateGrade(model);
                case SetupModeClass:
                    return CreateClass(model);
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

            // Defence in depth for QA3-1: GetById is org-scoped only; a scoped user must not update another unit.
            _departmentScope.EnsureCanAccessDepartment(entity);

            // A class acts as a room and is immovable: it cannot be re-parented (only its assets move, to a room).
            var requestedParentId = model.ParentDepartmentId.HasValue && model.ParentDepartmentId.Value > 0 ? model.ParentDepartmentId : null;
            var currentParentId = entity.ParentDepartmentId.HasValue && entity.ParentDepartmentId.Value > 0 ? entity.ParentDepartmentId : null;
            if (entity.DepartmentKind == DepartmentKind.Class && requestedParentId != currentParentId)
            {
                throw new BusinessException("A class acts as a room and cannot be moved or re-parented. Move its assets to a room instead.");
            }

            var allDepartments = repository.GetAll().ToList();
            if (model.ChildMoves != null)
            {
                foreach (var move in model.ChildMoves)
                {
                    var child = move == null ? null : allDepartments.FirstOrDefault(x => x.Id == move.ChildDepartmentId);
                    if (child != null && child.DepartmentKind == DepartmentKind.Class && move.NewParentDepartmentId.HasValue)
                    {
                        throw new BusinessException("A class acts as a room and cannot be moved or re-parented. Move its assets to a room instead.");
                    }
                }
            }

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

            var departmentOrganizationId = entity.OrganizationId
                ?? (_organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId());
            if (departmentOrganizationId.HasValue)
            {
                GuardDepartmentReferences(departmentOrganizationId.Value, model, flowConfigurable, entity.Id.ToString());
            }

            var oldKind = entity.DepartmentKind;
            var oldParentId = entity.ParentDepartmentId;
            var oldSection = entity.Section;
            entity.Name = model.Name;
            entity.Code = model.Code;
            entity.Description = model.Description;
            entity.ParentDepartmentId = model.ParentDepartmentId;
            entity.DepartmentKind = model.DepartmentKind;
            entity.IsRequisitionTarget = model.IsRequisitionTarget;
            entity.IsActive = model.IsActive;
            // Section: a null (field not posted) leaves the stored value alone, so a form that omits it can never
            // silently turn a Prep unit back into Senior school.
            if (model.Section.HasValue)
            {
                entity.Section = model.Section.Value;
            }

            if (model.DepartmentKind == DepartmentKind.Grade)
            {
                entity.SortOrder = model.SortOrder;
            }

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
            var sectionFollowers = new List<Department>();
            _unitOfWork.ExecuteInTransaction(() =>
            {
                repository.Update(entity);
                if (entity.DepartmentKind == DepartmentKind.Grade && entity.Section != oldSection)
                {
                    // A grade's classes are its streams: the ones still on the grade's old section follow it.
                    foreach (var classRow in allDepartments.Where(x =>
                        x.ParentDepartmentId == entity.Id
                        && x.DepartmentKind == DepartmentKind.Class
                        && x.Section == oldSection))
                    {
                        var follower = repository.GetById(classRow.Id);
                        if (follower == null)
                        {
                            continue;
                        }

                        follower.Section = entity.Section;
                        follower.UpdatedAt = now;
                        repository.Update(follower);
                        sectionFollowers.Add(follower);
                    }
                }
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
            var auditOld = new List<string>();
            if (oldKind != entity.DepartmentKind || oldParentId != entity.ParentDepartmentId)
            {
                auditOld.Add("Kind=" + oldKind + ";ParentId=" + (oldParentId.HasValue ? oldParentId.Value.ToString() : "-"));
            }

            if (oldSection != entity.Section)
            {
                auditOld.Add("Section=" + oldSection);
            }

            WriteDepartmentAudit(
                "Departments.Edit",
                entity.Id.ToString(),
                auditOld.Count > 0 ? string.Join(";", auditOld) : null,
                oldSection != entity.Section ? entity.Name + ";Section=" + entity.Section : entity.Name);
            foreach (var follower in sectionFollowers)
            {
                WriteDepartmentAudit(
                    "Departments.Edit",
                    follower.Id.ToString(),
                    "Section=" + oldSection,
                    follower.Name + ";Section=" + follower.Section + ";Reason=Section change on grade " + entity.Name);
            }

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

        /// <summary>
        /// Effective flow and PO raiser this unit would get when set to Inherit, for each "Inherit flow from" choice
        /// (nearest parent + each available ancestor). Resolved with the same resolver as approval, on an in-memory
        /// copy of the unit (nothing is saved).
        /// </summary>
        public IList<RequisitionInheritPreviewVm> GetRequisitionInheritPreviews(int departmentId)
        {
            var result = new List<RequisitionInheritPreviewVm>();
            var entity = _unitOfWork.Repository<Department>().GetById(departmentId);
            if (entity == null || !DepartmentHierarchyRules.CanConfigureRequisitionFlow(entity.DepartmentKind))
            {
                return result;
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
            var personLabels = BuildPurchaseOrderRaiserPersonLabels(walkById.Values);

            var choices = new List<RequisitionInheritSource?> { null };
            choices.AddRange(DepartmentRequisitionFlowResolver.GetInheritOptions(entity, getById)
                .Select(x => (RequisitionInheritSource?)x.Source));
            foreach (var choice in choices.Distinct())
            {
                var probe = new Department();
                foreach (var property in typeof(Department).GetProperties())
                {
                    if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                    {
                        property.SetValue(probe, property.GetValue(entity, null), null);
                    }
                }

                probe.RequisitionFlowMode = RequisitionFlowMode.InheritParent;
                probe.UseCustomRequisitionApproval = false;
                probe.RequisitionInheritFrom = choice;
                var probeId = entity.Id;
                Func<int, Department> probeGetById = id => id == probeId ? probe : getById(id);

                var model = MapDepartment(probe);
                ApplyEffectiveFlowFields(model, probe, probeGetById, orgDefault, roleLookup);
                ApplyEffectivePurchaseOrderRaiser(model, probe, probeGetById, orgDefault, roleLookup, personLabels);
                result.Add(new RequisitionInheritPreviewVm
                {
                    Choice = choice.HasValue ? choice.Value.ToString() : string.Empty,
                    FlowSummary = model.EffectiveRequisitionFlowSummary,
                    PurchaseOrderRaiserSummary = model.EffectivePurchaseOrderRaiserSummary
                });
            }

            return result;
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

        private OrgReferenceValidator NewReferenceGuard()
        {
            return new OrgReferenceValidator(_unitOfWork, _auditWriter, _referenceDataCache);
        }

        /// <summary>
        /// Same-org guard for every id a department save stores: parent unit, class teacher / room custodian,
        /// Custom requisition stages (roles + persons) and the PO raiser role/person. The controller validates the
        /// same things for the form; this is the server-side backstop for every caller.
        /// </summary>
        private void GuardDepartmentReferences(int organizationId, DepartmentVm model, bool flowConfigurable, string entityId)
        {
            if (model == null)
            {
                return;
            }

            var guard = NewReferenceGuard();
            guard.EnsureDepartmentInOrganization(organizationId, model.ParentDepartmentId, "Parent unit", "Departments.Edit", nameof(Department), entityId);
            if (model.DepartmentKind == DepartmentKind.Class)
            {
                guard.EnsureUserInOrganization(organizationId, model.ClassTeacherUserId, "Class teacher", "Departments.Edit", nameof(Department), entityId);
            }
            else if (model.DepartmentKind == DepartmentKind.Room)
            {
                guard.EnsureUserInOrganization(organizationId, model.RoomCustodianUserId, "Room custodian", "Departments.Edit", nameof(Department), entityId);
            }

            var custom = flowConfigurable
                && model.RequisitionFlowMode != RequisitionFlowMode.AutoApprove
                && (model.UseCustomRequisitionApproval || model.RequisitionFlowMode == RequisitionFlowMode.Custom);
            if (!custom)
            {
                return;
            }

            guard.EnsureProcessesInOrganization(organizationId, model.RequisitionApprovalProcesses, "Departments.Edit", nameof(Department), entityId);
            guard.EnsureRoleInOrganization(organizationId, model.PurchaseOrderRaiserRoleId, "Purchase order raised by", "Departments.Edit", nameof(Department), entityId);
            if (model.PurchaseOrderRaiserRoleId.HasValue && model.PurchaseOrderRaiserRoleId.Value > 0)
            {
                guard.EnsureUserInOrganization(organizationId, model.PurchaseOrderRaiserUserId, "Purchase order raised by", "Departments.Edit", nameof(Department), entityId);
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
                Section = model.Section ?? parent.Section,
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

            var roomOrganizationId = _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId();
            if (roomOrganizationId.HasValue)
            {
                NewReferenceGuard().EnsureUserInOrganization(
                    roomOrganizationId.Value, model.RoomCustodianUserId, "Room custodian", "Departments.CreateRoom", nameof(Department), null);
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
                    Section = DepartmentSectionHelper.Normalize(model.Section),
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

            // QA3-2: the generated code is capped at 20 (BuildRoomCode) and made unique within the organization.
            // IX_Department_Org_Code_Unique covers inactive rows too, so check every code in the org, not just active.
            var roomName = model.Name.Trim();
            var orgDepartments = _unitOfWork.Repository<Department>().GetAll().ToList();
            if (orgDepartments.Any(x =>
                    x.IsActive
                    && x.ParentDepartmentId == parent.Id
                    && x.DepartmentKind == DepartmentKind.Room
                    && string.Equals((x.Name ?? string.Empty).Trim(), roomName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new BusinessException("A room named '" + roomName + "' already exists under " + parent.Name + ".");
            }

            var roomCode = SchoolDepartmentCodeHelper.MakeUniqueCode(
                SchoolDepartmentCodeHelper.BuildRoomCode(parent.Code, roomName),
                code => orgDepartments.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)));

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
                Section = model.Section ?? parent.Section,
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
                Section = DepartmentSectionHelper.Normalize(model.Section),
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
            var gradeEntity = EnsureGradeParent(grade, now, DepartmentSectionHelper.Normalize(model.Section));
            var firstClassId = 0;
            foreach (var stream in streams)
            {
                var classEntity = BuildClassEntity(grade, stream, gradeEntity.Id, now, gradeEntity.Section);
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

        /// <summary>Create ONE grade: any code (Prep = G00 is fine), a Section and an optional sort number.</summary>
        private int CreateGrade(DepartmentCreateVm model)
        {
            ValidateRequiredNameAndCode(model);
            var name = model.Name.Trim();
            var code = model.Code.Trim().ToUpperInvariant();
            if (code.Length > SchoolDepartmentCodeHelper.MaxDepartmentCodeLength)
            {
                throw new BusinessException("Code must be " + SchoolDepartmentCodeHelper.MaxDepartmentCodeLength + " characters or fewer.");
            }

            if (model.SortOrder.HasValue && (model.SortOrder.Value < 0 || model.SortOrder.Value > 999))
            {
                throw new BusinessException("Sort number must be between 0 and 999.");
            }

            // IX_Department_Org_Code_Unique covers inactive rows too, so check every code in the organization.
            var existing = _unitOfWork.Repository<Department>().GetAll().ToList();
            if (existing.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)))
            {
                throw new BusinessException("A department with code '" + code + "' already exists.");
            }

            if (existing.Any(x => x.IsActive
                && x.DepartmentKind == DepartmentKind.Grade
                && string.Equals((x.Name ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new BusinessException("A grade named '" + name + "' already exists.");
            }

            var entity = new Department
            {
                Name = name,
                Code = code,
                Description = string.IsNullOrWhiteSpace(model.Description) ? name + " container" : model.Description.Trim(),
                DepartmentKind = DepartmentKind.Grade,
                IsRequisitionTarget = false,
                Section = DepartmentSectionHelper.Normalize(model.Section),
                SortOrder = model.SortOrder,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            ApplyOrganization(entity);
            _unitOfWork.Repository<Department>().Add(entity);
            _unitOfWork.SaveChanges();
            InvalidateDepartmentCache();
            WriteDepartmentAudit("Departments.Create", entity.Id.ToString(), null, entity.Name + ";Section=" + entity.Section);
            return entity.Id;
        }

        /// <summary>Create ONE class (stream) under an existing active grade; it takes the grade's Section unless one is chosen.</summary>
        private int CreateClass(DepartmentCreateVm model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                throw new BusinessException("Name is required.");
            }

            if (!model.ParentDepartmentId.HasValue || model.ParentDepartmentId.Value <= 0)
            {
                throw new BusinessException("Select the grade for this class.");
            }

            var classOrganizationId = _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId();
            if (classOrganizationId.HasValue)
            {
                var guard = NewReferenceGuard();
                guard.EnsureDepartmentInOrganization(
                    classOrganizationId.Value, model.ParentDepartmentId, "Grade", "Departments.CreateClass", nameof(Department), null);
                guard.EnsureUserInOrganization(
                    classOrganizationId.Value, model.ClassTeacherUserId, "Class teacher", "Departments.CreateClass", nameof(Department), null);
            }

            var grade = _unitOfWork.Repository<Department>().GetById(model.ParentDepartmentId.Value);
            if (grade == null || !grade.IsActive || grade.DepartmentKind != DepartmentKind.Grade)
            {
                throw new BusinessException("Select an active grade for this class.");
            }

            var name = model.Name.Trim();
            var existing = _unitOfWork.Repository<Department>().GetAll().ToList();
            if (existing.Any(x => x.IsActive
                && x.ParentDepartmentId == grade.Id
                && x.DepartmentKind == DepartmentKind.Class
                && string.Equals((x.Name ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new BusinessException("A class named '" + name + "' already exists under " + grade.Name + ".");
            }

            string code;
            if (string.IsNullOrWhiteSpace(model.Code))
            {
                code = SchoolDepartmentCodeHelper.MakeUniqueCode(
                    BuildSingleClassCode(grade.Code, name),
                    candidate => existing.Any(x => string.Equals(x.Code, candidate, StringComparison.OrdinalIgnoreCase)));
            }
            else
            {
                code = model.Code.Trim().ToUpperInvariant();
                if (code.Length > SchoolDepartmentCodeHelper.MaxDepartmentCodeLength)
                {
                    throw new BusinessException("Code must be " + SchoolDepartmentCodeHelper.MaxDepartmentCodeLength + " characters or fewer.");
                }

                if (existing.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new BusinessException("A department with code '" + code + "' already exists.");
                }
            }

            var entity = new Department
            {
                Name = name,
                Code = code,
                Description = string.IsNullOrWhiteSpace(model.Description) ? name + " (" + grade.Name + ")" : model.Description.Trim(),
                ParentDepartmentId = grade.Id,
                DepartmentKind = DepartmentKind.Class,
                IsRequisitionTarget = true,
                Section = model.Section ?? grade.Section,
                ClassTeacherUserId = string.IsNullOrWhiteSpace(model.ClassTeacherUserId) ? null : model.ClassTeacherUserId.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            ApplyOrganization(entity);
            _unitOfWork.Repository<Department>().Add(entity);
            _unitOfWork.SaveChanges();
            InvalidateDepartmentCache();
            WriteDepartmentAudit("Departments.Create", entity.Id.ToString(), null, entity.Name + ";Section=" + entity.Section);
            return entity.Id;
        }

        /// <summary>Grade code + the letters/digits of the class name, e.g. G03 + "C" = G03C. A leading grade number in the name is not repeated (G03 + "3C" = G03C).</summary>
        public static string BuildSingleClassCode(string gradeCode, string className)
        {
            var stream = new string((className ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());
            var number = DepartmentSectionHelper.ParseGradeNumber(gradeCode);
            if (number.HasValue && number.Value > 0)
            {
                var prefix = number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (stream.Length > prefix.Length && stream.StartsWith(prefix, StringComparison.Ordinal))
                {
                    stream = stream.Substring(prefix.Length);
                }
            }

            var code = ((gradeCode ?? string.Empty).Trim().ToUpperInvariant() + stream);
            return code.Length > SchoolDepartmentCodeHelper.MaxDepartmentCodeLength
                ? code.Substring(0, SchoolDepartmentCodeHelper.MaxDepartmentCodeLength)
                : code;
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
                var gradeEntity = EnsureGradeParent(grade, now, DepartmentSectionHelper.Normalize(model.Section));
                foreach (var stream in streams)
                {
                    var code = SchoolClassCodeHelper.BuildClassDepartmentCode(grade, stream);
                    if (_unitOfWork.Repository<Department>().GetAll().Any(x =>
                            x.IsActive && string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var classEntity = BuildClassEntity(grade, stream, gradeEntity.Id, now, gradeEntity.Section);
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

        private Department EnsureGradeParent(int grade, DateTime now, DepartmentSection section)
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
                Section = section,
                IsActive = true,
                CreatedAt = now
            };
            ApplyOrganization(gradeEntity);
            _unitOfWork.Repository<Department>().Add(gradeEntity);
            _unitOfWork.SaveChanges();
            WriteDepartmentAudit("Departments.Create", gradeEntity.Id.ToString(), null, gradeEntity.Name);
            return gradeEntity;
        }

        private Department BuildClassEntity(int grade, string stream, int parentId, DateTime now, DepartmentSection section)
        {
            var entity = new Department
            {
                Name = SchoolClassCodeHelper.BuildClassDepartmentName(grade, stream),
                Code = SchoolClassCodeHelper.BuildClassDepartmentCode(grade, stream),
                Description = "Class " + grade + stream.Trim().ToUpperInvariant(),
                ParentDepartmentId = parentId,
                DepartmentKind = DepartmentKind.Class,
                IsRequisitionTarget = true,
                Section = section,
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
                IsActive = model.IsActive,
                Section = DepartmentSectionHelper.Normalize(model.Section),
                SortOrder = model.SortOrder
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
                RoomCustodianUserId = entity.RoomCustodianUserId,
                PurchaseOrderRaiserRoleId = entity.PurchaseOrderRaiserRoleId,
                PurchaseOrderRaiserUserId = entity.PurchaseOrderRaiserUserId,
                Section = entity.Section,
                SortOrder = entity.SortOrder
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

            var personLabels = BuildPurchaseOrderRaiserPersonLabels(walkById.Values);
            var result = new List<DepartmentVm>(list.Count);
            foreach (var entity in list)
            {
                var model = MapDepartment(entity);
                ApplyEffectiveFlowFields(model, entity, getById, orgDefault, roleLookup);
                ApplyEffectivePurchaseOrderRaiser(model, entity, getById, orgDefault, roleLookup, personLabels);
                result.Add(model);
            }

            return result;
        }

        /// <summary>Org-scoped labels for the (few) PO-raiser persons referenced by departments; one query, only when any is set.</summary>
        private IDictionary<string, string> BuildPurchaseOrderRaiserPersonLabels(IEnumerable<Department> departments)
        {
            var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var ids = (departments ?? Enumerable.Empty<Department>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.PurchaseOrderRaiserUserId))
                .Select(x => x.PurchaseOrderRaiserUserId.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var organizationId = _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId();
            if (ids.Count == 0 || !organizationId.HasValue || _referenceDataCache == null)
            {
                return labels;
            }

            foreach (var user in _referenceDataCache.GetUsersByIds(organizationId.Value, ids) ?? new List<UserVm>())
            {
                if (user != null && !string.IsNullOrWhiteSpace(user.Id) && !labels.ContainsKey(user.Id))
                {
                    labels[user.Id] = StageApproverRules.BuildPersonLabel(user.Email, user.FirstName, user.LastName, user.Id);
                }
            }

            return labels;
        }

        private static void ApplyEffectivePurchaseOrderRaiser(
            DepartmentVm model,
            Department entity,
            Func<int, Department> getById,
            ApprovalProcessConfiguration orgDefault,
            IDictionary<int, string> roleLookup,
            IDictionary<string, string> personLabels)
        {
            if (model == null || entity == null || !DepartmentHierarchyRules.ShowsRequisitionFlowColumns(entity.DepartmentKind))
            {
                return;
            }

            var config = DepartmentRequisitionFlowResolver.Resolve(entity, getById, orgDefault);
            if (!PurchaseOrderRaiserRules.IsConfigured(config))
            {
                model.EffectivePurchaseOrderRaiserSummary = "PO: any user with Purchases permission";
                return;
            }

            var label = PurchaseOrderRaiserRules.BuildLabel(config, roleLookup, id =>
            {
                string found;
                return personLabels != null && personLabels.TryGetValue(id, out found) ? found : null;
            });
            var inherited = config.PurchaseOrderRaiserSourceDepartmentId.HasValue
                && config.PurchaseOrderRaiserSourceDepartmentId.Value != entity.Id;
            model.EffectivePurchaseOrderRaiserSummary = inherited
                ? "PO: " + label + " (inherited from " + config.PurchaseOrderRaiserSourceDepartmentName + ")"
                : "PO: " + label;
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

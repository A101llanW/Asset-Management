using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.DTOs;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Plans migration of active Class departments (under Grade parents) to Room departments under the admin tree.
    /// </summary>
    public static class ClassToRoomConversionHelper
    {
        public const string GradeContainerNameSuffix = " homerooms";

        public const string FlatContainerTempKey = "flat-homerooms";

        public sealed class NewDepartmentSpec
        {
            public string TempKey { get; set; }

            public string Name { get; set; }

            public string Code { get; set; }

            public string Description { get; set; }

            public int ParentDepartmentId { get; set; }

            public DepartmentKind DepartmentKind { get; set; }

            public bool IsRequisitionTarget { get; set; }
        }

        public sealed class DepartmentMutationSpec
        {
            public int DepartmentId { get; set; }

            public DepartmentKind NewKind { get; set; }

            /// <summary>Set when parent already exists.</summary>
            public int? NewParentDepartmentId { get; set; }

            /// <summary>Set when parent will be created in the same plan.</summary>
            public string NewParentContainerTempKey { get; set; }

            public string RoomCustodianUserId { get; set; }
        }

        public sealed class ConversionPlan
        {
            public IList<NewDepartmentSpec> NewDepartments { get; set; } = new List<NewDepartmentSpec>();

            public IList<DepartmentMutationSpec> Mutations { get; set; } = new List<DepartmentMutationSpec>();

            public IList<string> Warnings { get; set; } = new List<string>();

            public IList<string> Details { get; set; } = new List<string>();
        }

        public static ConversionPlan BuildPlan(
            IEnumerable<Department> allInOrganization,
            int topLevelAdministrativeDepartmentId,
            bool groupRoomsByGrade)
        {
            var plan = new ConversionPlan();
            var list = (allInOrganization ?? Enumerable.Empty<Department>())
                .Where(x => x != null)
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList();
            var byId = list.ToDictionary(x => x.Id);

            Department admin;
            if (!byId.TryGetValue(topLevelAdministrativeDepartmentId, out admin)
                || !admin.IsActive
                || !DepartmentHierarchyRules.IsTopLevelAdministrative(admin))
            {
                throw new BusinessException(
                    "Choose a top-level administrative department (no parent) to host converted homeroom rooms.");
            }

            var classes = list
                .Where(x => x.IsActive && x.DepartmentKind == DepartmentKind.Class)
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Id)
                .ToList();

            if (classes.Count == 0)
            {
                plan.Details.Add("No active class departments to convert.");
                return plan;
            }

            var containerExistingIdByGrade = new Dictionary<int, int>();
            var containerTempKeyByGrade = new Dictionary<int, string>();

            if (groupRoomsByGrade)
            {
                foreach (var classDept in classes)
                {
                    Department grade;
                    if (!TryGetGradeParent(classDept, byId, out grade))
                    {
                        plan.Warnings.Add(
                            "Skipped '" + (classDept.Name ?? ("#" + classDept.Id))
                            + "': parent is not an active grade department.");
                        continue;
                    }

                    if (containerExistingIdByGrade.ContainsKey(grade.Id)
                        || containerTempKeyByGrade.ContainsKey(grade.Id))
                    {
                        continue;
                    }

                    var existing = FindExistingGradeContainer(list, admin.Id, grade);
                    if (existing != null)
                    {
                        containerExistingIdByGrade[grade.Id] = existing.Id;
                        plan.Details.Add(
                            "Reusing sub-unit '" + existing.Name + "' for grade '" + grade.Name + "'.");
                    }
                    else
                    {
                        var tempKey = "grade-" + grade.Id;
                        var containerName = (grade.Name ?? "Grade").Trim() + GradeContainerNameSuffix;
                        plan.NewDepartments.Add(new NewDepartmentSpec
                        {
                            TempKey = tempKey,
                            Name = containerName,
                            Code = BuildGradeContainerCode(admin, grade, list),
                            Description = "Homeroom rooms converted from class streams under " + grade.Name,
                            ParentDepartmentId = admin.Id,
                            DepartmentKind = DepartmentKind.SubDepartment,
                            IsRequisitionTarget = false
                        });
                        containerTempKeyByGrade[grade.Id] = tempKey;
                        plan.Details.Add(
                            "Will create sub-unit '" + containerName + "' for grade '" + grade.Name + "'.");
                    }
                }
            }
            else
            {
                var flat = FindExistingFlatContainer(list, admin.Id);
                if (flat != null)
                {
                    containerExistingIdByGrade[-1] = flat.Id;
                    plan.Details.Add("Reusing flat container sub-unit '" + flat.Name + "'.");
                }
                else
                {
                    plan.NewDepartments.Add(new NewDepartmentSpec
                    {
                        TempKey = FlatContainerTempKey,
                        Name = "Homerooms",
                        Code = BuildFlatContainerCode(admin, list),
                        Description = "Rooms converted from class streams",
                        ParentDepartmentId = admin.Id,
                        DepartmentKind = DepartmentKind.SubDepartment,
                        IsRequisitionTarget = false
                    });
                    containerTempKeyByGrade[-1] = FlatContainerTempKey;
                    plan.Details.Add("Will create flat sub-unit 'Homerooms' under '" + admin.Name + "'.");
                }
            }

            foreach (var classDept in classes)
            {
                Department grade;
                if (!TryGetGradeParent(classDept, byId, out grade))
                {
                    continue;
                }

                if (list.Any(x => x.IsActive && x.ParentDepartmentId == classDept.Id))
                {
                    plan.Warnings.Add(
                        "Skipped '" + classDept.Name + "': class has active child departments; move children first.");
                    continue;
                }

                var mutation = new DepartmentMutationSpec
                {
                    DepartmentId = classDept.Id,
                    NewKind = DepartmentKind.Room,
                    RoomCustodianUserId = string.IsNullOrWhiteSpace(classDept.ClassTeacherUserId)
                        ? null
                        : classDept.ClassTeacherUserId.Trim()
                };

                if (groupRoomsByGrade)
                {
                    int existingParentId;
                    if (containerExistingIdByGrade.TryGetValue(grade.Id, out existingParentId))
                    {
                        mutation.NewParentDepartmentId = existingParentId;
                    }
                    else
                    {
                        mutation.NewParentContainerTempKey = containerTempKeyByGrade[grade.Id];
                    }
                }
                else
                {
                    int existingParentId;
                    if (containerExistingIdByGrade.TryGetValue(-1, out existingParentId))
                    {
                        mutation.NewParentDepartmentId = existingParentId;
                    }
                    else
                    {
                        mutation.NewParentContainerTempKey = FlatContainerTempKey;
                    }
                }

                plan.Mutations.Add(mutation);
                plan.Details.Add("Will convert class '" + classDept.Name + "' to room.");
            }

            return plan;
        }

        public static void ValidateMutation(
            DepartmentMutationSpec mutation,
            IDictionary<int, Department> byId,
            int resolvedParentDepartmentId)
        {
            if (mutation == null)
            {
                throw new BusinessException("Conversion mutation is required.");
            }

            Department entity;
            if (!byId.TryGetValue(mutation.DepartmentId, out entity) || !entity.IsActive)
            {
                throw new BusinessException("Class department #" + mutation.DepartmentId + " was not found.");
            }

            var simulated = new Department
            {
                Id = entity.Id,
                OrganizationId = entity.OrganizationId,
                Name = entity.Name,
                Code = entity.Code,
                DepartmentKind = mutation.NewKind,
                ParentDepartmentId = resolvedParentDepartmentId,
                IsActive = true,
                RoomCustodianUserId = mutation.RoomCustodianUserId,
                ClassTeacherUserId = null
            };

            DepartmentHierarchyRules.AssertValidHierarchy(
                simulated.Id,
                simulated.DepartmentKind,
                simulated.ParentDepartmentId,
                byId.Values);
        }

        private static bool TryGetGradeParent(
            Department classDept,
            IDictionary<int, Department> byId,
            out Department grade)
        {
            grade = null;
            if (classDept == null || !classDept.ParentDepartmentId.HasValue)
            {
                return false;
            }

            Department parent;
            if (!byId.TryGetValue(classDept.ParentDepartmentId.Value, out parent)
                || !parent.IsActive
                || parent.DepartmentKind != DepartmentKind.Grade)
            {
                return false;
            }

            grade = parent;
            return true;
        }

        private static Department FindExistingGradeContainer(
            IList<Department> list,
            int adminId,
            Department grade)
        {
            var expectedName = (grade.Name ?? "Grade").Trim() + GradeContainerNameSuffix;
            return list.FirstOrDefault(x =>
                x.IsActive
                && x.DepartmentKind == DepartmentKind.SubDepartment
                && x.ParentDepartmentId == adminId
                && string.Equals(x.Name, expectedName, StringComparison.OrdinalIgnoreCase));
        }

        private static Department FindExistingFlatContainer(IList<Department> list, int adminId)
        {
            return list.FirstOrDefault(x =>
                x.IsActive
                && x.DepartmentKind == DepartmentKind.SubDepartment
                && x.ParentDepartmentId == adminId
                && string.Equals(x.Name, "Homerooms", StringComparison.OrdinalIgnoreCase));
        }

        private static string BuildGradeContainerCode(Department admin, Department grade, IList<Department> list)
        {
            var baseCode = SchoolDepartmentCodeHelper.BuildSubDepartmentCode(admin.Code, grade.Name + "-HR");
            return EnsureUniqueCode(baseCode, list);
        }

        private static string BuildFlatContainerCode(Department admin, IList<Department> list)
        {
            var baseCode = SchoolDepartmentCodeHelper.BuildSubDepartmentCode(admin.Code, "Homerooms");
            return EnsureUniqueCode(baseCode, list);
        }

        private static string EnsureUniqueCode(string baseCode, IList<Department> list)
        {
            var code = (baseCode ?? string.Empty).Trim();
            if (code.Length == 0)
            {
                code = "HR";
            }

            if (!list.Any(x => x.IsActive && string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)))
            {
                return code;
            }

            for (var i = 2; i < 100; i++)
            {
                var candidate = TruncateCode(code + i);
                if (!list.Any(x => x.IsActive && string.Equals(x.Code, candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    return candidate;
                }
            }

            throw new BusinessException("Could not generate a unique department code for the homeroom container.");
        }

        private static string TruncateCode(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length <= SchoolDepartmentCodeHelper.MaxDepartmentCodeLength)
            {
                return code;
            }

            return code.Substring(0, SchoolDepartmentCodeHelper.MaxDepartmentCodeLength);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services
{
    public static class DepartmentRequisitionApprovalSettingsHelper
    {
        public static IList<ApprovalProcessSettingsVm> BuildForDepartment(
            Department department,
            IUnitOfWork unitOfWork,
            IEnumerable<RoleVm> roles)
        {
            var settings = LoadSettingsDictionary(unitOfWork, department == null ? null : department.OrganizationId);
            var roleLookup = BuildRoleLookup(roles);
            var defaultRoleId = OrganizationApprovalDefaults.ResolveDefaultApproverRoleId(
                ApprovalProcessCodes.Purchase,
                roles);

            if (department == null || !department.UseCustomRequisitionApproval)
            {
                return new List<ApprovalProcessSettingsVm>
                {
                    BuildOrgDefaultProcess(settings, roleLookup, defaultRoleId)
                };
            }

            var configuredStages = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(department.RequisitionApprovalStageRoleIds);
            var configuredUsers = ApprovalWorkflowSettingsHelper.ParseStageUserIds(department.RequisitionApprovalStageUserIds);
            if (configuredStages.Count == 0 && defaultRoleId.HasValue)
            {
                configuredStages.Add(defaultRoleId.Value);
            }

            return new List<ApprovalProcessSettingsVm>
            {
                ToProcessVm(
                    true,
                    configuredStages,
                    configuredUsers,
                    roleLookup,
                    null)
            };
        }

        public static void ApplyToDepartment(Department entity, DepartmentVm model)
        {
            if (entity == null || model == null)
            {
                return;
            }

            entity.UseCustomRequisitionApproval = model.UseCustomRequisitionApproval;
            if (!model.UseCustomRequisitionApproval)
            {
                return;
            }

            var process = (model.RequisitionApprovalProcesses ?? new List<ApprovalProcessSettingsVm>())
                .FirstOrDefault(x => x != null
                    && string.Equals(x.ProcessCode, ApprovalProcessCodes.Purchase, StringComparison.OrdinalIgnoreCase))
                ?? (model.RequisitionApprovalProcesses ?? new List<ApprovalProcessSettingsVm>()).FirstOrDefault();

            if (process == null)
            {
                entity.RequisitionApprovalStageRoleIds = null;
                entity.RequisitionApprovalStageUserIds = null;
                return;
            }

            process.RequiresApproval = true;
            entity.RequisitionApprovalStageRoleIds = ApprovalWorkflowSettingsHelper.SerializeStageRoleIds(process.GetStageRoleIds());
            entity.RequisitionApprovalStageUserIds = ApprovalWorkflowSettingsHelper.SerializeStageUserIds(process.GetStageUserIds());
        }

        public static void ValidateCustomApproval(DepartmentVm model, Action<string, string> addModelError)
        {
            if (model == null || !model.UseCustomRequisitionApproval)
            {
                return;
            }

            var processes = model.RequisitionApprovalProcesses ?? new List<ApprovalProcessSettingsVm>();
            if (processes.Count == 0)
            {
                processes = new List<ApprovalProcessSettingsVm>
                {
                    new ApprovalProcessSettingsVm
                    {
                        ProcessCode = ApprovalProcessCodes.Purchase,
                        DisplayName = ApprovalProcessCodes.GetDisplayName(ApprovalProcessCodes.Purchase),
                        RequiresApproval = true,
                        Stages = ApprovalWorkflowSettingsHelper.CreateStageSettings(new int[0], false)
                    }
                };
            }

            foreach (var process in processes)
            {
                if (process != null)
                {
                    process.RequiresApproval = true;
                }
            }

            ApprovalWorkflowSettingsHelper.ValidateApprovalProcessSettings(processes, addModelError);
        }

        private static ApprovalProcessSettingsVm BuildOrgDefaultProcess(
            IDictionary<string, SystemSetting> settings,
            IDictionary<int, string> roleLookup,
            int? defaultRoleId)
        {
            var requiresApproval = ApprovalWorkflowSettingsHelper.GetBool(
                settings,
                ApprovalProcessCodes.GetEnabledSettingKey(ApprovalProcessCodes.Purchase),
                ApprovalWorkflowSettingsHelper.GetBool(
                    settings,
                    ApprovalProcessCodes.GetLegacyRequireSettingKey(ApprovalProcessCodes.Purchase),
                    false));
            var configuredStages = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(
                ApprovalWorkflowSettingsHelper.GetString(settings, ApprovalProcessCodes.GetStageRoleIdsSettingKey(ApprovalProcessCodes.Purchase)));

            if (configuredStages.Count == 0 && defaultRoleId.HasValue && requiresApproval)
            {
                configuredStages.Add(defaultRoleId.Value);
            }

            return ToProcessVm(requiresApproval, configuredStages, new List<string>(), roleLookup, null);
        }

        private static ApprovalProcessSettingsVm ToProcessVm(
            bool requiresApproval,
            IList<int> configuredStages,
            IList<string> configuredUsers,
            IDictionary<int, string> roleLookup,
            IDictionary<string, string> userLookup)
        {
            var vm = new ApprovalProcessSettingsVm
            {
                ProcessCode = ApprovalProcessCodes.Purchase,
                DisplayName = ApprovalProcessCodes.GetDisplayName(ApprovalProcessCodes.Purchase),
                RequiresApproval = requiresApproval,
                Stages = ApprovalWorkflowSettingsHelper.CreateStageSettings(
                    configuredStages,
                    configuredUsers,
                    requiresApproval && configuredStages.Count == 0)
            };
            vm.StageSummary = ApprovalWorkflowSettingsHelper.BuildAssetStageSummary(
                vm.GetStageRoleIds(),
                vm.GetStageUserIds(),
                roleLookup,
                userLookup ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            return vm;
        }

        private static IDictionary<string, SystemSetting> LoadSettingsDictionary(IUnitOfWork unitOfWork, int? organizationId)
        {
            var settings = unitOfWork.Repository<SystemSetting>().GetAll();
            if (organizationId.HasValue)
            {
                settings = settings.Where(x => x.OrganizationId == organizationId.Value);
            }

            return ApprovalWorkflowSettingsHelper.ToDictionary(settings);
        }

        private static IDictionary<int, string> BuildRoleLookup(IEnumerable<RoleVm> roles)
        {
            var lookup = new Dictionary<int, string>();
            foreach (var role in roles ?? Enumerable.Empty<RoleVm>())
            {
                if (role == null || role.Id <= 0 || lookup.ContainsKey(role.Id))
                {
                    continue;
                }

                lookup[role.Id] = role.Name;
            }

            return lookup;
        }
    }
}

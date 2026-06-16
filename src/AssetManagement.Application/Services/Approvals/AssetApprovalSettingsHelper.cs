using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services
{
    public static class AssetApprovalSettingsHelper
    {
        public static readonly string[] ProcessCodes = { ApprovalProcessCodes.Transfer, ApprovalProcessCodes.Disposal };

        public static IList<ApprovalProcessSettingsVm> BuildDefaultProcesses(IUnitOfWork unitOfWork, IEnumerable<RoleVm> roles, int? organizationId = null)
        {
            var settings = LoadSettingsDictionary(unitOfWork, organizationId);
            var roleLookup = BuildRoleLookup(roles);
            return ProcessCodes
                .Select(code => BuildProcessVm(code, settings, roleLookup, GetDefaultApproverRoleId(code, roles)))
                .ToList();
        }

        public static IList<ApprovalProcessSettingsVm> BuildFromAsset(
            Asset asset,
            IUnitOfWork unitOfWork,
            IEnumerable<RoleVm> roles,
            IDictionary<string, string> userLookup = null)
        {
            if (asset == null)
            {
                return BuildDefaultProcesses(unitOfWork, roles);
            }

            var settings = LoadSettingsDictionary(unitOfWork, asset.OrganizationId);
            var roleLookup = BuildRoleLookup(roles);
            return ProcessCodes
                .Select(code => BuildAssetProcessVm(asset, code, settings, roleLookup, userLookup, GetDefaultApproverRoleId(code, roles)))
                .ToList();
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

        public static void ApplyToAsset(Asset asset, IList<ApprovalProcessSettingsVm> processes)
        {
            if (asset == null)
            {
                return;
            }

            foreach (var process in processes ?? new List<ApprovalProcessSettingsVm>())
            {
                if (process == null)
                {
                    continue;
                }

                var stageIds = ApprovalWorkflowSettingsHelper.SerializeStageRoleIds(process.GetStageRoleIds());
                var stageUserIds = ApprovalWorkflowSettingsHelper.SerializeStageUserIds(process.GetStageUserIds());
                if (string.Equals(process.ProcessCode, ApprovalProcessCodes.Transfer, StringComparison.OrdinalIgnoreCase))
                {
                    asset.RequireTransferApproval = process.RequiresApproval;
                    asset.TransferApprovalStageRoleIds = stageIds;
                    asset.TransferApprovalStageUserIds = stageUserIds;
                }
                else if (string.Equals(process.ProcessCode, ApprovalProcessCodes.Disposal, StringComparison.OrdinalIgnoreCase))
                {
                    asset.RequireDisposalApproval = process.RequiresApproval;
                    asset.DisposalApprovalStageRoleIds = stageIds;
                    asset.DisposalApprovalStageUserIds = stageUserIds;
                }
            }
        }

        public static void ValidateApprovalProcesses(IList<ApprovalProcessSettingsVm> processes, Action<string, string> addModelError)
        {
            ApprovalWorkflowSettingsHelper.ValidateAssetApprovalProcessSettings(processes, addModelError);
        }

        private static ApprovalProcessSettingsVm BuildAssetProcessVm(
            Asset asset,
            string processCode,
            IDictionary<string, SystemSetting> settings,
            IDictionary<int, string> roleLookup,
            IDictionary<string, string> userLookup,
            int? defaultRoleId)
        {
            var requiresApproval = string.Equals(processCode, ApprovalProcessCodes.Transfer, StringComparison.OrdinalIgnoreCase)
                ? asset.RequireTransferApproval
                : asset.RequireDisposalApproval;
            var configuredStages = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(
                string.Equals(processCode, ApprovalProcessCodes.Transfer, StringComparison.OrdinalIgnoreCase)
                    ? asset.TransferApprovalStageRoleIds
                    : asset.DisposalApprovalStageRoleIds);
            var configuredUsers = ApprovalWorkflowSettingsHelper.ParseStageUserIds(
                string.Equals(processCode, ApprovalProcessCodes.Transfer, StringComparison.OrdinalIgnoreCase)
                    ? asset.TransferApprovalStageUserIds
                    : asset.DisposalApprovalStageUserIds);

            if (configuredStages.Count == 0)
            {
                configuredStages = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(
                    ApprovalWorkflowSettingsHelper.GetString(settings, ApprovalProcessCodes.GetStageRoleIdsSettingKey(processCode)));
                configuredUsers = new List<string>();
            }

            if (configuredStages.Count == 0 && defaultRoleId.HasValue && requiresApproval)
            {
                configuredStages.Add(defaultRoleId.Value);
                configuredUsers = new List<string>();
            }

            return ToProcessVm(processCode, requiresApproval, configuredStages, configuredUsers, roleLookup, userLookup);
        }

        private static ApprovalProcessSettingsVm BuildProcessVm(
            string processCode,
            IDictionary<string, SystemSetting> settings,
            IDictionary<int, string> roleLookup,
            int? defaultRoleId)
        {
            var requiresApproval = ApprovalWorkflowSettingsHelper.GetBool(
                settings,
                ApprovalProcessCodes.GetEnabledSettingKey(processCode),
                ApprovalWorkflowSettingsHelper.GetBool(settings, ApprovalProcessCodes.GetLegacyRequireSettingKey(processCode), false));
            var configuredStages = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(
                ApprovalWorkflowSettingsHelper.GetString(settings, ApprovalProcessCodes.GetStageRoleIdsSettingKey(processCode)));

            if (configuredStages.Count == 0 && defaultRoleId.HasValue && requiresApproval)
            {
                configuredStages.Add(defaultRoleId.Value);
            }

            return ToProcessVm(processCode, requiresApproval, configuredStages, new List<string>(), roleLookup, null);
        }

        private static ApprovalProcessSettingsVm ToProcessVm(
            string processCode,
            bool requiresApproval,
            IList<int> configuredStages,
            IList<string> configuredUsers,
            IDictionary<int, string> roleLookup,
            IDictionary<string, string> userLookup)
        {
            var vm = new ApprovalProcessSettingsVm
            {
                ProcessCode = processCode,
                DisplayName = ApprovalProcessCodes.GetDisplayName(processCode),
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

        private static int? GetDefaultApproverRoleId(string processCode, IEnumerable<RoleVm> roles)
        {
            string roleName;
            switch (processCode)
            {
                case ApprovalProcessCodes.Transfer:
                case ApprovalProcessCodes.Disposal:
                    roleName = "Asset Manager";
                    break;
                default:
                    roleName = "Asset Manager";
                    break;
            }

            return roles == null
                ? null
                : roles.FirstOrDefault(x => string.Equals(x.Name, roleName, StringComparison.OrdinalIgnoreCase))?.Id;
        }
    }
}

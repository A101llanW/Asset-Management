using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Helpers;
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

        /// <summary>
        /// Writes the posted/default stages onto the asset CSV columns. Every role/person is checked against the
        /// asset's organisation first: stages whose role belongs to another company are removed (with their
        /// person) and foreign persons are cleared, so a raw foreign id can never be serialized onto an asset.
        /// Interactive callers reject foreign ids before this point (OrgReferenceValidator); the return value
        /// lists anything dropped here (normally empty) so bulk/import callers can warn and log.
        /// </summary>
        public static IDictionary<string, StageGuardResult> ApplyToAsset(
            Asset asset,
            IList<ApprovalProcessSettingsVm> processes,
            Func<int, bool> roleInOrganization,
            Func<string, bool> userInOrganization)
        {
            if (asset == null)
            {
                return new Dictionary<string, StageGuardResult>();
            }

            if (roleInOrganization == null)
            {
                throw new ArgumentNullException("roleInOrganization", "Approval stage roles must be validated against the asset's organisation before they are stored.");
            }

            var dropped = OrgReferenceGuard.DropForeignStages(processes, roleInOrganization, userInOrganization);
            ApplyValidatedToAsset(asset, processes);
            return dropped;
        }

        private static void ApplyValidatedToAsset(Asset asset, IList<ApprovalProcessSettingsVm> processes)
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
            ApprovalWorkflowSettingsHelper.ValidateApprovalProcessSettings(processes, addModelError);
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

            // Stored ids that are not roles of this organisation (e.g. another company's role ids copied onto the
            // asset) cannot be shown or re-posted by the form: hide them so the form falls back to this org's
            // defaults, and AssetService.Update drops/logs them on save.
            if (configuredStages.Count > 0 && roleLookup != null && roleLookup.Count > 0)
            {
                var shown = OrgReferenceGuard.GuardStageCsv(
                    ApprovalWorkflowSettingsHelper.SerializeStageRoleIds(configuredStages.Select(x => (int?)x)),
                    ApprovalWorkflowSettingsHelper.SerializeStageUserIds(configuredUsers),
                    roleLookup.ContainsKey,
                    null);
                if (shown.HasDrops)
                {
                    configuredStages = new List<int>(shown.RoleIds);
                    configuredUsers = new List<string>(shown.UserIds);
                }
            }

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
            return OrganizationApprovalDefaults.ResolveDefaultApproverRoleId(processCode, roles);
        }
    }
}

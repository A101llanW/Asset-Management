using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.ViewModels
{
    public static class ApprovalProcessCodes
    {
        public const string Transfer = "Transfer";
        public const string Disposal = "Disposal";
        public const string Purchase = "Purchase";
        public const string AssetRequest = "AssetRequest";

        public static readonly string[] Ordered = { Transfer, Disposal, Purchase };

        public static string GetDisplayName(string processCode)
        {
            switch (processCode)
            {
                case Transfer:
                    return "Asset Transfer";
                case Disposal:
                    return "Asset Disposal";
                case Purchase:
                    return "Requisition";
                case AssetRequest:
                    return "Asset Request";
                default:
                    return processCode;
            }
        }

        public static string GetProcessGuide(string processCode)
        {
            switch (processCode)
            {
                case Transfer:
                    return "Asset transfers must be approved by the configured role(s) before the move is completed.";
                case Disposal:
                    return "Disposal requests must be approved before assets are written off.";
                case Purchase:
                    return "Department heads submit requisitions; the configured role(s) approve before procurement records a purchase order. Typical setup: one stage — Procurement Officer (not Finance).";
                default:
                    return null;
            }
        }

        public static bool IsRequisitionProcessName(string processName)
        {
            return string.Equals(processName, GetDisplayName(Purchase), StringComparison.OrdinalIgnoreCase)
                || string.Equals(processName, "Purchase Request", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetEnabledSettingKey(string processCode)
        {
            return "Approval.Process." + processCode + ".Enabled";
        }

        public static string GetStageRoleIdsSettingKey(string processCode)
        {
            return "Approval.Process." + processCode + ".StageRoleIds";
        }

        public static string GetStageUserIdsSettingKey(string processCode)
        {
            return "Approval.Process." + processCode + ".StageUserIds";
        }

        public static string GetLegacyRequireSettingKey(string processCode)
        {
            switch (processCode)
            {
                case Transfer:
                    return "Approval.RequireTransferApproval";
                case Disposal:
                    return "Approval.RequireDisposalApproval";
                case Purchase:
                    return "Approval.RequirePurchaseApproval";
                default:
                    return null;
            }
        }
    }

    public class ApprovalProcessConfiguration
    {
        public string ProcessCode { get; set; }

        public string DisplayName { get; set; }

        public bool RequiresApproval { get; set; }

        public IList<int> StageRoleIds { get; set; } = new List<int>();

        public IList<string> StageUserIds { get; set; } = new List<string>();

        public bool UsesApproval
        {
            get { return RequiresApproval && StageRoleIds != null && StageRoleIds.Count > 0; }
        }
    }

    public class ApprovalProcessSettingsVm
    {
        public string ProcessCode { get; set; }

        public string DisplayName { get; set; }

        [Display(Name = "Requires Approval")]
        public bool RequiresApproval { get; set; }

        public IList<ApprovalStageSettingsVm> Stages { get; set; } = new List<ApprovalStageSettingsVm>();

        public string StageSummary { get; set; }

        public IList<int?> GetStageRoleIds()
        {
            return (Stages ?? new List<ApprovalStageSettingsVm>())
                .Select(x => x == null ? null : x.RoleId)
                .ToList();
        }

        public IList<string> GetStageUserIds()
        {
            return (Stages ?? new List<ApprovalStageSettingsVm>())
                .Select(x => x == null ? null : x.UserId)
                .ToList();
        }
    }

    public class ApprovalStageSettingsVm
    {
        [Display(Name = "Approver Role")]
        public int? RoleId { get; set; }

        [Display(Name = "Approver")]
        public string UserId { get; set; }
    }

    public class TransferSubmissionResultVm
    {
        public int TransferId { get; set; }

        public bool RequiresApproval { get; set; }
    }

    public class TransferApprovalDecisionVm
    {
        [Required]
        public int TransferId { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }
    }

    public class ApprovalDecisionHistoryVm
    {
        public int StageNumber { get; set; }

        public string RoleName { get; set; }

        public string ApproverName { get; set; }

        public string Decision { get; set; }

        public string Notes { get; set; }

        public string DecisionDateText { get; set; }
    }

    public class PendingTransferApprovalVm
    {
        public int Id { get; set; }

        public string RequestedByName { get; set; }

        public string RequestedDateText { get; set; }

        public string FromEntity { get; set; }

        public string ToEntity { get; set; }

        public string Reason { get; set; }

        public int CurrentApprovalStage { get; set; }

        public int? CurrentStageRoleId { get; set; }

        public string CurrentStageRoleName { get; set; }

        public string CurrentStageUserId { get; set; }

        public string CurrentStageUserName { get; set; }

        public bool CanCurrentUserApprove { get; set; }

        public IEnumerable<ApprovalDecisionHistoryVm> ApprovalHistory { get; set; } = Enumerable.Empty<ApprovalDecisionHistoryVm>();
    }

    public class PendingDisposalApprovalVm
    {
        public int DisposalRecordId { get; set; }

        public string RequestedByName { get; set; }

        public string RequestedDateText { get; set; }

        public string DisposalMethod { get; set; }

        public string DisposalReason { get; set; }

        public string Notes { get; set; }

        public int CurrentApprovalStage { get; set; }

        public int? CurrentStageRoleId { get; set; }

        public string CurrentStageRoleName { get; set; }

        public string CurrentStageUserId { get; set; }

        public string CurrentStageUserName { get; set; }

        public bool CanCurrentUserApprove { get; set; }

        public IEnumerable<ApprovalDecisionHistoryVm> ApprovalHistory { get; set; } = Enumerable.Empty<ApprovalDecisionHistoryVm>();
    }

    public static class ApprovalWorkflowSettingsHelper
    {
        public static IDictionary<string, SystemSetting> ToDictionary(IEnumerable<SystemSetting> settings)
        {
            var dictionary = new Dictionary<string, SystemSetting>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var setting in settings ?? Enumerable.Empty<SystemSetting>())
            {
                if (setting == null || string.IsNullOrWhiteSpace(setting.SettingKey) || dictionary.ContainsKey(setting.SettingKey))
                {
                    continue;
                }

                dictionary.Add(setting.SettingKey, setting);
            }

            return dictionary;
        }

        public static bool GetBool(IDictionary<string, SystemSetting> settings, string key, bool fallback)
        {
            SystemSetting setting;
            bool parsed;
            if (settings != null && !string.IsNullOrWhiteSpace(key) && settings.TryGetValue(key, out setting) && bool.TryParse(setting.SettingValue, out parsed))
            {
                return parsed;
            }

            return fallback;
        }

        public static string GetString(IDictionary<string, SystemSetting> settings, string key, string fallback = null)
        {
            SystemSetting setting;
            if (settings != null && !string.IsNullOrWhiteSpace(key) && settings.TryGetValue(key, out setting) && !string.IsNullOrWhiteSpace(setting.SettingValue))
            {
                return setting.SettingValue;
            }

            return fallback;
        }

        public static string GetDefaultCurrencyCode(IEnumerable<SystemSetting> settings)
        {
            var currency = GetString(ToDictionary(settings), FinanceDefaults.DefaultCurrencySettingKey, FinanceDefaults.DefaultCurrencyCode);
            return string.IsNullOrWhiteSpace(currency)
                ? FinanceDefaults.DefaultCurrencyCode
                : currency.Trim().ToUpperInvariant();
        }

        public static IList<int> ParseStageRoleIds(string value)
        {
            return (value ?? string.Empty)
                .Split(',')
                .Select(x =>
                {
                    int parsed;
                    return int.TryParse(x.Trim(), out parsed) ? parsed : 0;
                })
                .Where(x => x > 0)
                .ToList();
        }

        public static string SerializeStageRoleIds(IEnumerable<int?> roleIds)
        {
            return string.Join(",", (roleIds ?? Enumerable.Empty<int?>())
                .Where(x => x.HasValue && x.Value > 0)
                .Select(x => x.Value.ToString()));
        }

        public static IList<string> ParseStageUserIds(string value)
        {
            return (value ?? string.Empty)
                .Split(',')
                .Select(x => string.IsNullOrWhiteSpace(x) ? null : x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }

        public static string SerializeStageUserIds(IEnumerable<string> userIds)
        {
            return string.Join(",", (userIds ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()));
        }

        public static string BuildStageSummary(IEnumerable<int?> roleIds, IDictionary<int, string> roleLookup)
        {
            var names = (roleIds ?? Enumerable.Empty<int?>())
                .Where(x => x.HasValue && x.Value > 0)
                .Select(x => roleLookup != null && roleLookup.ContainsKey(x.Value) ? roleLookup[x.Value] : "Role #" + x.Value)
                .ToList();

            return names.Count == 0
                ? "No approval stages configured."
                : string.Join(" -> ", names);
        }

        public static string BuildStageSummary(IEnumerable<int> roleIds, IDictionary<int, string> roleLookup)
        {
            return BuildStageSummary(roleIds.Select(x => (int?)x), roleLookup);
        }

        public static int? TryGetCurrentStageRoleId(string stageRoleIds, int currentStage)
        {
            var stages = ParseStageRoleIds(stageRoleIds);
            var stageIndex = currentStage <= 0 ? 0 : currentStage - 1;
            if (stageIndex < 0 || stageIndex >= stages.Count)
            {
                return null;
            }

            return stages[stageIndex];
        }

        public static string TryGetCurrentStageUserId(string stageUserIds, int currentStage)
        {
            var stages = ParseStageUserIds(stageUserIds);
            var stageIndex = currentStage <= 0 ? 0 : currentStage - 1;
            if (stageIndex < 0 || stageIndex >= stages.Count)
            {
                return null;
            }

            return stages[stageIndex];
        }

        public static string BuildAssetStageSummary(
            IEnumerable<int?> roleIds,
            IEnumerable<string> userIds,
            IDictionary<int, string> roleLookup,
            IDictionary<string, string> userLookup)
        {
            var roleList = (roleIds ?? Enumerable.Empty<int?>()).ToList();
            var userList = (userIds ?? Enumerable.Empty<string>()).ToList();
            var names = new List<string>();

            for (var i = 0; i < roleList.Count; i++)
            {
                var roleId = roleList[i];
                if (!roleId.HasValue || roleId.Value <= 0)
                {
                    continue;
                }

                var roleName = ResolveRoleName(roleLookup, roleId);
                var userId = i < userList.Count ? userList[i] : null;
                string userName;
                if (!string.IsNullOrWhiteSpace(userId)
                    && userLookup != null
                    && userLookup.TryGetValue(userId, out userName)
                    && !string.IsNullOrWhiteSpace(userName))
                {
                    names.Add(roleName + " — " + userName);
                }
                else
                {
                    names.Add(roleName);
                }
            }

            return names.Count == 0
                ? "No approval stages configured."
                : string.Join(" -> ", names);
        }

        public static string ResolveRoleName(IDictionary<int, string> roleLookup, int? roleId, string emptyLabel = "No stage assigned")
        {
            if (!roleId.HasValue)
            {
                return emptyLabel;
            }

            return roleLookup != null && roleLookup.ContainsKey(roleId.Value)
                ? roleLookup[roleId.Value]
                : "Role #" + roleId.Value;
        }

        public static string ResolveRoleName(IDictionary<int, string> roleLookup, int roleId)
        {
            return ResolveRoleName(roleLookup, (int?)roleId);
        }

        public static IList<string> GetProcessesUsingRole(int roleId, IDictionary<string, SystemSetting> settings)
        {
            var processes = new List<string>();
            foreach (var processCode in ApprovalProcessCodes.Ordered)
            {
                var stageRoleIds = ParseStageRoleIds(
                    GetString(settings, ApprovalProcessCodes.GetStageRoleIdsSettingKey(processCode)));
                if (stageRoleIds.Contains(roleId))
                {
                    processes.Add(ApprovalProcessCodes.GetDisplayName(processCode));
                }
            }

            return processes;
        }

        public static string GetTypicalRoleWorkflowNote(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return null;
            }

            switch (roleName.Trim())
            {
                case "Company Admin":
                    return "Configures approval matrix, roles, and org defaults.";
                case "Department Head":
                    return "Submits requisitions and in-store asset requests for their department.";
                case "Procurement Officer":
                    return "Typical requisition approver; manages suppliers, catalog, and purchase orders.";
                case "Asset Manager":
                    return "Receives goods and approves in-store asset requests; may approve transfers/disposals when configured.";
                case "Finance Officer":
                    return "Financial reporting and depreciation; not the default requisition approver.";
                case "Staff":
                    return "Assignee-only profile — register employees without this login role.";
                case "Auditor":
                    return "Read-only access to reports and audit data.";
                default:
                    return null;
            }
        }

        public const int MaxApprovalStages = 10;

        public static IList<ApprovalStageSettingsVm> CreateStageSettings(IEnumerable<int> roleIds, bool ensureBlankRowWhenEmpty)
        {
            return CreateStageSettings(roleIds, null, ensureBlankRowWhenEmpty);
        }

        public static IList<ApprovalStageSettingsVm> CreateStageSettings(
            IEnumerable<int> roleIds,
            IEnumerable<string> userIds,
            bool ensureBlankRowWhenEmpty)
        {
            var roleList = (roleIds ?? Enumerable.Empty<int>()).Where(x => x > 0).ToList();
            var userList = (userIds ?? Enumerable.Empty<string>()).ToList();
            var stages = new List<ApprovalStageSettingsVm>();

            for (var i = 0; i < roleList.Count; i++)
            {
                stages.Add(new ApprovalStageSettingsVm
                {
                    RoleId = roleList[i],
                    UserId = i < userList.Count ? userList[i] : null
                });
            }

            if (stages.Count == 0 && ensureBlankRowWhenEmpty)
            {
                stages.Add(new ApprovalStageSettingsVm());
            }

            return stages;
        }

        public static ApprovalProcessSettingsVm CreateProcessSettingsVm(
            string processCode,
            bool requiresApproval,
            IEnumerable<int> roleIds,
            IDictionary<int, string> roleLookup,
            bool ensureBlankRowWhenEmpty)
        {
            return CreateProcessSettingsVm(
                processCode,
                requiresApproval,
                roleIds,
                null,
                roleLookup,
                null,
                ensureBlankRowWhenEmpty);
        }

        public static ApprovalProcessSettingsVm CreateProcessSettingsVm(
            string processCode,
            bool requiresApproval,
            IEnumerable<int> roleIds,
            IEnumerable<string> userIds,
            IDictionary<int, string> roleLookup,
            IDictionary<string, string> userLookup,
            bool ensureBlankRowWhenEmpty)
        {
            var configuredStages = (roleIds ?? Enumerable.Empty<int>()).Where(x => x > 0).ToList();
            var configuredUsers = (userIds ?? Enumerable.Empty<string>()).ToList();
            var vm = new ApprovalProcessSettingsVm
            {
                ProcessCode = processCode,
                DisplayName = ApprovalProcessCodes.GetDisplayName(processCode),
                RequiresApproval = requiresApproval,
                Stages = CreateStageSettings(
                    configuredStages,
                    configuredUsers,
                    ensureBlankRowWhenEmpty && configuredStages.Count == 0)
            };
            vm.StageSummary = userLookup != null && configuredUsers.Any(x => !string.IsNullOrWhiteSpace(x))
                ? BuildAssetStageSummary(configuredStages.Select(x => (int?)x), configuredUsers, roleLookup, userLookup)
                : BuildStageSummary(configuredStages, roleLookup);
            return vm;
        }

        public static void ValidateApprovalProcessSettings(IList<ApprovalProcessSettingsVm> processes, Action<string, string> addModelError)
        {
            if (addModelError == null)
            {
                throw new ArgumentNullException("addModelError");
            }

            var items = processes ?? new List<ApprovalProcessSettingsVm>();
            for (var i = 0; i < items.Count; i++)
            {
                var process = items[i];
                if (process == null)
                {
                    continue;
                }

                if (!process.RequiresApproval)
                {
                    continue;
                }

                var stages = process.GetStageRoleIds();
                var compactStages = stages.Where(x => x.HasValue && x.Value > 0).Select(x => x.Value).ToList();
                var errorKey = "ApprovalProcesses[" + i + "].Stages[0].RoleId";

                if (compactStages.Count == 0)
                {
                    addModelError(errorKey, "Add at least one approval stage for " + process.DisplayName + ".");
                    continue;
                }

                if (stages.Any(x => !x.HasValue || x.Value <= 0))
                {
                    addModelError(errorKey, "Each approval stage for " + process.DisplayName + " must have an approver role selected.");
                }

                if (compactStages.Count > MaxApprovalStages)
                {
                    addModelError(errorKey, process.DisplayName + " cannot have more than " + MaxApprovalStages + " approval stages.");
                }

                if (compactStages.Count != compactStages.Distinct().Count())
                {
                    addModelError(errorKey, "Each approval stage for " + process.DisplayName + " must use a different role.");
                }
            }
        }

        public static void ValidateAssetApprovalProcessSettings(IList<ApprovalProcessSettingsVm> processes, Action<string, string> addModelError)
        {
            if (addModelError == null)
            {
                throw new ArgumentNullException("addModelError");
            }

            var items = processes ?? new List<ApprovalProcessSettingsVm>();
            for (var i = 0; i < items.Count; i++)
            {
                var process = items[i];
                if (process == null || !process.RequiresApproval)
                {
                    continue;
                }

                var stages = process.Stages ?? new List<ApprovalStageSettingsVm>();
                var compactStages = stages
                    .Where(x => x != null && x.RoleId.HasValue && x.RoleId.Value > 0 && !string.IsNullOrWhiteSpace(x.UserId))
                    .ToList();
                var errorKey = "ApprovalProcesses[" + i + "].Stages[0].UserId";

                if (compactStages.Count == 0)
                {
                    addModelError(errorKey, "Add at least one approver for " + process.DisplayName + ".");
                    continue;
                }

                if (stages.Any(x => x == null
                    || !x.RoleId.HasValue
                    || x.RoleId.Value <= 0
                    || string.IsNullOrWhiteSpace(x.UserId)))
                {
                    addModelError(errorKey, "Each approval stage for " + process.DisplayName + " must have a title and specific approver selected.");
                }

                if (compactStages.Count > MaxApprovalStages)
                {
                    addModelError(errorKey, process.DisplayName + " cannot have more than " + MaxApprovalStages + " approval stages.");
                }

                if (compactStages.Count != compactStages.Select(x => x.UserId.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count())
                {
                    addModelError(errorKey, "Each approval stage for " + process.DisplayName + " must use a different approver.");
                }
            }
        }
    }
}

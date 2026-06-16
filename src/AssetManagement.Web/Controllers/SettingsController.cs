using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Settings.Manage")]
    public class SettingsController : BaseController
    {
        private const string AttachmentRootPathKey = "Attachment.RootPath";
        private const string WarrantyThresholdKey = "Notification.WarrantyThresholdDays";
        private const string InsuranceThresholdKey = "Notification.InsuranceThresholdDays";
        private const string MaintenanceThresholdKey = "Notification.MaintenanceThresholdDays";
        private const string DefaultCurrencyKey = FinanceDefaults.DefaultCurrencySettingKey;

        public ActionResult Index()
        {
            var model = BuildViewModel();
            PopulateRoleOptions();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(SettingsVm model)
        {
            PopulateRoleOptions();
            ValidateApprovalProcesses(model);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var repository = UnitOfWork.Repository<SystemSetting>();
            var settings = ApprovalWorkflowSettingsHelper.ToDictionary(repository.GetAll());

            UpsertSetting(repository, settings, AttachmentRootPathKey, model.AttachmentRootPath, "Filesystem root path used for uploaded attachments.");
            UpsertSetting(repository, settings, WarrantyThresholdKey, model.WarrantyThresholdDays.ToString(), "Days before warranty expiry to trigger alerts.");
            UpsertSetting(repository, settings, InsuranceThresholdKey, model.InsuranceThresholdDays.ToString(), "Days before insurance expiry to trigger alerts.");
            UpsertSetting(repository, settings, MaintenanceThresholdKey, model.MaintenanceThresholdDays.ToString(), "Days before maintenance due date to trigger alerts.");
            UpsertSetting(repository, settings, DefaultCurrencyKey, model.DefaultCurrency.ToUpperInvariant(), "Default finance currency code.");

            foreach (var process in model.ApprovalProcesses ?? new List<ApprovalProcessSettingsVm>())
            {
                var stageIds = process.GetStageRoleIds();
                UpsertSetting(repository, settings, ApprovalProcessCodes.GetEnabledSettingKey(process.ProcessCode), process.RequiresApproval.ToString(), "Whether " + process.DisplayName + " requires staged approval.");
                UpsertSetting(repository, settings, ApprovalProcessCodes.GetStageRoleIdsSettingKey(process.ProcessCode), ApprovalWorkflowSettingsHelper.SerializeStageRoleIds(stageIds), "Ordered role ids for " + process.DisplayName + " approval stages.");

                var legacyKey = ApprovalProcessCodes.GetLegacyRequireSettingKey(process.ProcessCode);
                if (!string.IsNullOrWhiteSpace(legacyKey))
                {
                    UpsertSetting(repository, settings, legacyKey, process.RequiresApproval.ToString(), "Legacy approval toggle for " + process.DisplayName + ".");
                }
            }

            UnitOfWork.SaveChanges();
            TempData["Message"] = "Settings saved successfully.";
            TempData["Guidance"] = "Approval stage changes apply to new requests going forward. Existing pending requests keep the approval path they were submitted with.";
            return RedirectToAction("Index");
        }

        private SettingsVm BuildViewModel()
        {
            var repository = UnitOfWork.Repository<SystemSetting>();
            var settings = ApprovalWorkflowSettingsHelper.ToDictionary(repository.GetAll());
            var roles = BuildRoleService().GetRoles().ToList();
            var roleLookup = roles.ToDictionary(x => x.Id, x => x.Name);

            return new SettingsVm
            {
                AttachmentRootPath = ApprovalWorkflowSettingsHelper.GetString(settings, AttachmentRootPathKey, "~/App_Data/Attachments"),
                WarrantyThresholdDays = GetInt(settings, WarrantyThresholdKey, 30),
                InsuranceThresholdDays = GetInt(settings, InsuranceThresholdKey, 30),
                MaintenanceThresholdDays = GetInt(settings, MaintenanceThresholdKey, 14),
                DefaultCurrency = ApprovalWorkflowSettingsHelper.GetString(settings, DefaultCurrencyKey, FinanceDefaults.DefaultCurrencyCode),
                RequireTransferApproval = ApprovalWorkflowSettingsHelper.GetBool(settings, ApprovalProcessCodes.GetLegacyRequireSettingKey(ApprovalProcessCodes.Transfer), false),
                RequireDisposalApproval = ApprovalWorkflowSettingsHelper.GetBool(settings, ApprovalProcessCodes.GetLegacyRequireSettingKey(ApprovalProcessCodes.Disposal), false),
                ApprovalProcesses = ApprovalProcessCodes.Ordered.Select(code => BuildApprovalProcessVm(code, settings, roleLookup, GetDefaultApproverRoleIdForProcess(code, roles))).ToList()
            };
        }

        private static int? GetDefaultApproverRoleIdForProcess(string processCode, IList<RoleVm> roles)
        {
            string roleName;
            switch (processCode)
            {
                case ApprovalProcessCodes.Transfer:
                case ApprovalProcessCodes.Disposal:
                    roleName = "Asset Manager";
                    break;
                case ApprovalProcessCodes.Purchase:
                    roleName = "Procurement Officer";
                    break;
                default:
                    roleName = "Asset Manager";
                    break;
            }

            return roles?.FirstOrDefault(x => string.Equals(x.Name, roleName, StringComparison.OrdinalIgnoreCase))?.Id;
        }

        private ApprovalProcessSettingsVm BuildApprovalProcessVm(string processCode, IDictionary<string, SystemSetting> settings, IDictionary<int, string> roleLookup, int? defaultRoleId)
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

            return ApprovalWorkflowSettingsHelper.CreateProcessSettingsVm(
                processCode,
                requiresApproval,
                configuredStages,
                roleLookup,
                requiresApproval);
        }

        private void PopulateRoleOptions()
        {
            ViewBag.RoleOptions = BuildRoleOptionList();
        }

        private void ValidateApprovalProcesses(SettingsVm model)
        {
            ApprovalWorkflowSettingsHelper.ValidateApprovalProcessSettings(
                model?.ApprovalProcesses,
                (key, message) => ModelState.AddModelError(key, message));
        }

        private static int GetInt(IDictionary<string, SystemSetting> settings, string key, int fallback)
        {
            SystemSetting setting;
            int parsed;
            if (settings.TryGetValue(key, out setting) && int.TryParse(setting.SettingValue, out parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static void UpsertSetting(IRepository<SystemSetting> repository, IDictionary<string, SystemSetting> settings, string key, string value, string description)
        {
            SystemSetting setting;
            if (settings.TryGetValue(key, out setting))
            {
                setting.SettingValue = value;
                setting.Description = description;
                repository.Update(setting);
                return;
            }

            var newSetting = new SystemSetting
            {
                SettingKey = key,
                SettingValue = value,
                Description = description
            };
            repository.Add(newSetting);
            settings[key] = newSetting;
        }
    }
}

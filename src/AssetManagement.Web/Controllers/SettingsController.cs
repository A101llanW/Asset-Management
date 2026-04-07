using System;
using System.Collections.Generic;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using System.Web.Mvc;
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
        private const string DefaultCurrencyKey = "Finance.DefaultCurrency";
        private const string DefaultUsefulLifeMonthsKey = "Finance.DefaultUsefulLifeMonths";
        private const string RequireTransferApprovalKey = "Approval.RequireTransferApproval";
        private const string RequireDisposalApprovalKey = "Approval.RequireDisposalApproval";

        public ActionResult Index()
        {
            var model = BuildViewModel();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(SettingsVm model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var repository = UnitOfWork.Repository<SystemSetting>();
            var settings = ToDictionary(repository.GetAll());

            UpsertSetting(repository, settings, AttachmentRootPathKey, model.AttachmentRootPath, "Filesystem root path used for uploaded attachments.");
            UpsertSetting(repository, settings, WarrantyThresholdKey, model.WarrantyThresholdDays.ToString(), "Days before warranty expiry to trigger alerts.");
            UpsertSetting(repository, settings, InsuranceThresholdKey, model.InsuranceThresholdDays.ToString(), "Days before insurance expiry to trigger alerts.");
            UpsertSetting(repository, settings, MaintenanceThresholdKey, model.MaintenanceThresholdDays.ToString(), "Days before maintenance due date to trigger alerts.");
            UpsertSetting(repository, settings, DefaultCurrencyKey, model.DefaultCurrency.ToUpperInvariant(), "Default finance currency code.");
            UpsertSetting(repository, settings, DefaultUsefulLifeMonthsKey, model.DefaultUsefulLifeMonths.ToString(), "Default useful life in months for depreciation.");
            UpsertSetting(repository, settings, RequireTransferApprovalKey, model.RequireTransferApproval.ToString(), "Require approval before transfers can be completed.");
            UpsertSetting(repository, settings, RequireDisposalApprovalKey, model.RequireDisposalApproval.ToString(), "Require approval before disposals can be completed.");

            UnitOfWork.SaveChanges();

            TempData["Message"] = "Settings saved successfully.";
            return RedirectToAction("Index");
        }

        private SettingsVm BuildViewModel()
        {
            var repository = UnitOfWork.Repository<SystemSetting>();
            var settings = ToDictionary(repository.GetAll());

            var model = new SettingsVm
            {
                AttachmentRootPath = GetString(settings, AttachmentRootPathKey, "~/App_Data/Attachments"),
                WarrantyThresholdDays = GetInt(settings, WarrantyThresholdKey, 30),
                InsuranceThresholdDays = GetInt(settings, InsuranceThresholdKey, 30),
                MaintenanceThresholdDays = GetInt(settings, MaintenanceThresholdKey, 14),
                DefaultCurrency = GetString(settings, DefaultCurrencyKey, "USD"),
                DefaultUsefulLifeMonths = GetInt(settings, DefaultUsefulLifeMonthsKey, 36),
                RequireTransferApproval = GetBool(settings, RequireTransferApprovalKey, true),
                RequireDisposalApproval = GetBool(settings, RequireDisposalApprovalKey, true)
            };

            return model;
        }

        private static Dictionary<string, SystemSetting> ToDictionary(IEnumerable<SystemSetting> settings)
        {
            var dictionary = new Dictionary<string, SystemSetting>(StringComparer.OrdinalIgnoreCase);

            foreach (var setting in settings)
            {
                if (setting == null || string.IsNullOrWhiteSpace(setting.SettingKey))
                {
                    continue;
                }

                if (!dictionary.ContainsKey(setting.SettingKey))
                {
                    dictionary.Add(setting.SettingKey, setting);
                }
            }

            return dictionary;
        }

        private static string GetString(IDictionary<string, SystemSetting> settings, string key, string fallback)
        {
            SystemSetting setting;
            if (settings.TryGetValue(key, out setting) && !string.IsNullOrWhiteSpace(setting.SettingValue))
            {
                return setting.SettingValue;
            }

            return fallback;
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

        private static bool GetBool(IDictionary<string, SystemSetting> settings, string key, bool fallback)
        {
            SystemSetting setting;
            bool parsed;
            if (settings.TryGetValue(key, out setting) && bool.TryParse(setting.SettingValue, out parsed))
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

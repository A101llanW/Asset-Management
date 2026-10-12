using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Settings.Manage")]
    public partial class SettingsController : BaseController
    {
        private const string AttachmentRootPathKey = "Attachment.RootPath";
        private const string WarrantyThresholdKey = "Notification.WarrantyThresholdDays";
        private const string InsuranceThresholdKey = "Notification.InsuranceThresholdDays";
        private const string MaintenanceThresholdKey = "Notification.MaintenanceThresholdDays";
        private const string DefaultCurrencyKey = FinanceDefaults.DefaultCurrencySettingKey;

        public ActionResult Index()
        {
            var model = BuildViewModel();
            ViewBag.SmtpRouteSummary = BuildSmtpRouteSummary(ResolveCurrentOrganizationId());
            PopulateApprovalMatrixOptions();
            PopulateRequisitionPolicies(null);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(SettingsVm model, [Bind(Prefix = "RequisitionPolicies")] RequisitionPolicySettingsVm requisitionPolicies = null)
        {
            PopulateApprovalMatrixOptions();
            PopulateRequisitionPolicies(Request.Form["RequisitionPolicies.ClassificationMode"] == null ? null : requisitionPolicies);
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
            CapLabelQrMagnification(model.LabelPrinter, ExternalUrlHelper.GetConfiguredBaseUrl());
            UpsertLabelPrinterSettings(repository, settings, model.LabelPrinter, includeLayoutDesign: false);

            foreach (var process in model.ApprovalProcesses ?? new List<ApprovalProcessSettingsVm>())
            {
                var stageIds = process.GetStageRoleIds();
                var stageUserIds = process.GetStageUserIds();
                UpsertSetting(repository, settings, ApprovalProcessCodes.GetEnabledSettingKey(process.ProcessCode), process.RequiresApproval.ToString(), "Whether " + process.DisplayName + " requires staged approval.");
                UpsertSetting(repository, settings, ApprovalProcessCodes.GetStageRoleIdsSettingKey(process.ProcessCode), ApprovalWorkflowSettingsHelper.SerializeStageRoleIds(stageIds), "Ordered role ids for " + process.DisplayName + " approval stages.");
                UpsertSetting(repository, settings, ApprovalProcessCodes.GetStageUserIdsSettingKey(process.ProcessCode), ApprovalWorkflowSettingsHelper.SerializeStageUserIds(stageUserIds), "Ordered approver user ids for " + process.DisplayName + " approval stages.");

                var legacyKey = ApprovalProcessCodes.GetLegacyRequireSettingKey(process.ProcessCode);
                if (!string.IsNullOrWhiteSpace(legacyKey))
                {
                    UpsertSetting(repository, settings, legacyKey, process.RequiresApproval.ToString(), "Legacy approval toggle for " + process.DisplayName + ".");
                }
            }

            UnitOfWork.SaveChanges();
            if (Request.Form["RequisitionPolicies.ClassificationMode"] != null)
            {
                SaveRequisitionPolicies(requisitionPolicies);
            }

            TempData["Message"] = "Settings saved successfully.";
            TempData["Guidance"] = "Approval stage changes apply to new requisitions and other requests going forward. Existing pending items keep the approval path they were submitted with.";
            return RedirectToAction("Index");
        }

        public ActionResult LabelDesigner()
        {
            var repository = UnitOfWork.Repository<SystemSetting>();
            var settings = ApprovalWorkflowSettingsHelper.ToDictionary(repository.GetAll());
            var labelPrinter = LabelPrinterSettingsHelper.FromDictionary(settings);
            // rel1009i: normalise on load - a stored magnification larger than the label allows is shown capped.
            CapLabelQrMagnification(labelPrinter, ExternalUrlHelper.GetConfiguredBaseUrl());
            var design = LabelPrinterSettingsHelper.GetLayoutDesign(labelPrinter)
                ?? LabelLayoutDesignHelper.CreateDefault(labelPrinter.WidthMm, labelPrinter.HeightMm);

            var model = new LabelDesignerVm
            {
                LabelPrinter = labelPrinter,
                LayoutDesignJson = LabelLayoutDesignHelper.Serialize(design, labelPrinter.WidthMm, labelPrinter.HeightMm, labelPrinter.QrMagnification),
                LayoutTemplatesJson = LabelLayoutTemplateHelper.SerializeForClient(LabelLayoutTemplateHelper.ListTemplates(settings)),
                ExternalBaseUrl = ExternalUrlHelper.GetConfiguredBaseUrl()
            };

            SetLabelQrViewBag(model.LabelPrinter, model.ExternalBaseUrl);
            return View(model);
        }

        /// <summary>rel1009i: caps the QR magnification so the QR fits the label (worst-case scan URL for this site).</summary>
        private void CapLabelQrMagnification(LabelPrinterSettingsVm labelPrinter, string externalBaseUrl)
        {
            if (labelPrinter == null)
            {
                return;
            }

            var payloadBytes = AssetQrUrlHelper.WorstCaseQrPayloadBytes(Request, Url, externalBaseUrl);
            labelPrinter.QrMagnification = LabelQrSizing.CapMagnification(labelPrinter.QrMagnification, labelPrinter.WidthMm, labelPrinter.HeightMm, payloadBytes);
        }

        private void SetLabelQrViewBag(LabelPrinterSettingsVm labelPrinter, string externalBaseUrl)
        {
            var payloadBytes = AssetQrUrlHelper.WorstCaseQrPayloadBytes(Request, Url, externalBaseUrl);
            ViewBag.QrPayloadBytes = payloadBytes;
            ViewBag.QrMaxMagnification = labelPrinter == null
                ? LabelQrSizing.MaxMagnification
                : LabelQrSizing.MaxMagnificationFor(labelPrinter.WidthMm, labelPrinter.HeightMm, payloadBytes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult SaveLabelLayoutTemplate(string name, string layoutDesignJson, int widthMm, int heightMm, decimal qrMagnification, string templateId = null)
        {
            try
            {
                var repository = UnitOfWork.Repository<SystemSetting>();
                var settings = ApprovalWorkflowSettingsHelper.ToDictionary(repository.GetAll());
                var collection = LabelLayoutTemplateHelper.Deserialize(settings);
                qrMagnification = LabelQrSizing.CapMagnification(
                    qrMagnification, widthMm, heightMm, AssetQrUrlHelper.WorstCaseQrPayloadBytes(Request, Url, ExternalUrlHelper.GetConfiguredBaseUrl()));
                var template = LabelLayoutTemplateHelper.SaveTemplate(
                    collection,
                    name,
                    layoutDesignJson,
                    widthMm,
                    heightMm,
                    qrMagnification,
                    templateId);

                UpsertSetting(
                    repository,
                    settings,
                    LabelLayoutTemplateHelper.TemplatesKey,
                    LabelLayoutTemplateHelper.SerializeCollection(collection),
                    "Saved label layout canvas templates.");

                UnitOfWork.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = "Template \"" + template.Name + "\" saved.",
                    template = template,
                    templates = LabelLayoutTemplateHelper.ListTemplates(settings)
                });
            }
            catch (ArgumentException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult DeleteLabelLayoutTemplate(string templateId)
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                return Json(new { success = false, message = "Template id is required." });
            }

            var repository = UnitOfWork.Repository<SystemSetting>();
            var settings = ApprovalWorkflowSettingsHelper.ToDictionary(repository.GetAll());
            var collection = LabelLayoutTemplateHelper.Deserialize(settings);
            if (!LabelLayoutTemplateHelper.DeleteTemplate(collection, templateId))
            {
                return Json(new { success = false, message = "Template not found." });
            }

            UpsertSetting(
                repository,
                settings,
                LabelLayoutTemplateHelper.TemplatesKey,
                LabelLayoutTemplateHelper.SerializeCollection(collection),
                "Saved label layout canvas templates.");

            UnitOfWork.SaveChanges();

            return Json(new
            {
                success = true,
                message = "Template deleted.",
                templates = LabelLayoutTemplateHelper.ListTemplates(settings)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LabelDesigner(LabelDesignerVm model)
        {
            if (model == null)
            {
                model = new LabelDesignerVm();
            }

            if (model.LabelPrinter == null)
            {
                model.LabelPrinter = new LabelPrinterSettingsVm();
            }

            if (!ModelState.IsValid)
            {
                model.ExternalBaseUrl = ExternalUrlHelper.GetConfiguredBaseUrl();
                SetLabelQrViewBag(model.LabelPrinter, model.ExternalBaseUrl);
                return View(model);
            }

            // rel1009i: normalise on save (server side) - cap the magnification before the layout is re-clamped.
            CapLabelQrMagnification(model.LabelPrinter, ExternalUrlHelper.GetConfiguredBaseUrl());
            var repository = UnitOfWork.Repository<SystemSetting>();
            var settings = ApprovalWorkflowSettingsHelper.ToDictionary(repository.GetAll());
            var design = LabelLayoutDesignHelper.Deserialize(
                model.LayoutDesignJson,
                model.LabelPrinter.WidthMm,
                model.LabelPrinter.HeightMm,
                model.LabelPrinter.QrMagnification);

            model.LabelPrinter.LayoutPreset = LabelPrinterSettingsHelper.LayoutCustom;
            model.LabelPrinter.LayoutDesignJson = LabelLayoutDesignHelper.Serialize(
                design,
                model.LabelPrinter.WidthMm,
                model.LabelPrinter.HeightMm,
                model.LabelPrinter.QrMagnification);

            UpsertLabelPrinterSettings(repository, settings, model.LabelPrinter, includeLayoutDesign: true);
            UnitOfWork.SaveChanges();

            TempData["Message"] = "Label layout saved successfully.";
            return RedirectToAction("LabelDesigner");
        }

        private SettingsVm BuildViewModel()
        {
            var repository = UnitOfWork.Repository<SystemSetting>();
            var settings = ApprovalWorkflowSettingsHelper.ToDictionary(repository.GetAll());
            var roles = BuildRoleService().GetRoles().ToList();
            var roleLookup = roles.ToDictionary(x => x.Id, x => x.Name);
            var orgId = ResolveCurrentOrganizationId();
            var users = orgId.HasValue
                ? BuildReferenceDataCache().GetUsersForDropdown(orgId.Value)
                : GetActiveUsers();
            var userLookup = ApproverPickerHelper.BuildUserNameLookup(users);

            return new SettingsVm
            {
                AttachmentRootPath = ApprovalWorkflowSettingsHelper.GetString(settings, AttachmentRootPathKey, "~/App_Data/Attachments"),
                WarrantyThresholdDays = GetInt(settings, WarrantyThresholdKey, 30),
                InsuranceThresholdDays = GetInt(settings, InsuranceThresholdKey, 30),
                MaintenanceThresholdDays = GetInt(settings, MaintenanceThresholdKey, 14),
                DefaultCurrency = ApprovalWorkflowSettingsHelper.GetString(settings, DefaultCurrencyKey, FinanceDefaults.DefaultCurrencyCode),
                RequireTransferApproval = ApprovalWorkflowSettingsHelper.GetBool(settings, ApprovalProcessCodes.GetLegacyRequireSettingKey(ApprovalProcessCodes.Transfer), false),
                RequireDisposalApproval = ApprovalWorkflowSettingsHelper.GetBool(settings, ApprovalProcessCodes.GetLegacyRequireSettingKey(ApprovalProcessCodes.Disposal), false),
                ApprovalProcesses = ApprovalProcessCodes.Ordered.Select(code => BuildApprovalProcessVm(code, settings, roleLookup, userLookup, GetDefaultApproverRoleIdForProcess(code, roles))).ToList(),
                LabelPrinter = LabelPrinterSettingsHelper.FromDictionary(settings)
            };
        }

        private static int? GetDefaultApproverRoleIdForProcess(string processCode, IList<RoleVm> roles)
        {
            return OrganizationApprovalDefaults.ResolveDefaultApproverRoleId(processCode, roles);
        }

        private ApprovalProcessSettingsVm BuildApprovalProcessVm(
            string processCode,
            IDictionary<string, SystemSetting> settings,
            IDictionary<int, string> roleLookup,
            IDictionary<string, string> userLookup,
            int? defaultRoleId)
        {
            var requiresApproval = ApprovalWorkflowSettingsHelper.GetBool(
                settings,
                ApprovalProcessCodes.GetEnabledSettingKey(processCode),
                ApprovalWorkflowSettingsHelper.GetBool(settings, ApprovalProcessCodes.GetLegacyRequireSettingKey(processCode), false));
            var configuredStages = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(
                ApprovalWorkflowSettingsHelper.GetString(settings, ApprovalProcessCodes.GetStageRoleIdsSettingKey(processCode)));
            var configuredUsers = ApprovalWorkflowSettingsHelper.ParseStageUserIds(
                ApprovalWorkflowSettingsHelper.GetString(settings, ApprovalProcessCodes.GetStageUserIdsSettingKey(processCode)));

            if (configuredStages.Count == 0 && defaultRoleId.HasValue && requiresApproval)
            {
                configuredStages.Add(defaultRoleId.Value);
            }

            return ApprovalWorkflowSettingsHelper.CreateProcessSettingsVm(
                processCode,
                requiresApproval,
                configuredStages,
                configuredUsers,
                roleLookup,
                userLookup,
                requiresApproval);
        }

        private void PopulateApprovalMatrixOptions()
        {
            PopulateRoleOptions();
            // Requisition row: optional per-stage "Approver (person)" (same component as Departments Edit).
            PopulateStagePersonOptions();
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
            ValidateStagePeople(model);
        }

        /// <summary>
        /// Org Approval Matrix: the person picker is offered on the Requisition row only. Persons there must be
        /// active, in this org and hold the stage role (cleared when the role changed); other rows stay role-only.
        /// </summary>
        private void ValidateStagePeople(SettingsVm model)
        {
            var organizationId = ResolveCurrentOrganizationId();
            if (model == null || model.ApprovalProcesses == null || !organizationId.HasValue)
            {
                return;
            }

            var settings = ApprovalWorkflowSettingsHelper.ToDictionary(UnitOfWork.Repository<SystemSetting>().GetAll());
            var roleIds = GetOrganizationRoleIdSet(organizationId.Value);
            Func<string, ApproverUserRecord> findUser = null;

            // Same-org guard for every process row (Transfer/Disposal rows were role-unchecked before): a stage role
            // that is not one of this organisation's roles is rejected and logged before any setting is written.
            var foreignStages = OrgReferenceGuard.FindForeignStages(model.ApprovalProcesses, roleIds.Contains, null);
            if (foreignStages.Count > 0)
            {
                new OrgReferenceValidator(UnitOfWork, AuditWriter, BuildReferenceDataCache()).Report(
                    organizationId.Value,
                    "Settings.ApprovalMatrix",
                    "SystemSetting",
                    null,
                    "Approval.*.StageRoleIds",
                    string.Join(" | ", foreignStages.ToArray()),
                    true);
                // The Requisition row gets its role error from StageApproverRules.ValidateStages below.
                foreach (var message in OrgReferenceGuard.FindForeignStages(
                    model.ApprovalProcesses.Where(x => x != null
                        && !string.Equals(x.ProcessCode, ApprovalProcessCodes.Purchase, StringComparison.OrdinalIgnoreCase)),
                    roleIds.Contains,
                    null))
                {
                    ModelState.AddModelError(string.Empty, message);
                }
            }

            foreach (var process in model.ApprovalProcesses.Where(x => x != null && x.Stages != null))
            {
                if (!string.Equals(process.ProcessCode, ApprovalProcessCodes.Purchase, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var stage in process.Stages.Where(x => x != null))
                    {
                        stage.UserId = null;
                    }

                    continue;
                }

                if (findUser == null)
                {
                    findUser = BuildOrgApproverUserFinder(organizationId.Value);
                }

                var storedRoleIds = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(
                    ApprovalWorkflowSettingsHelper.GetString(settings, ApprovalProcessCodes.GetStageRoleIdsSettingKey(process.ProcessCode)));
                StageApproverRules.ValidateStages(
                    process.Stages,
                    storedRoleIds,
                    organizationId.Value,
                    roleIds,
                    findUser,
                    (key, message) => ModelState.AddModelError(string.Empty, process.DisplayName + " " + message),
                    "ApprovalProcesses.Stages");
            }
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

        private static void UpsertLabelPrinterSettings(
            IRepository<SystemSetting> repository,
            IDictionary<string, SystemSetting> settings,
            LabelPrinterSettingsVm labelPrinter,
            bool includeLayoutDesign)
        {
            if (labelPrinter == null)
            {
                return;
            }

            UpsertSetting(repository, settings, LabelPrinterSettingsHelper.EnabledKey, labelPrinter.Enabled.ToString(), "Enable Zebra ZD421 label printing.");
            UpsertSetting(repository, settings, LabelPrinterSettingsHelper.ModelKey, labelPrinter.Model ?? LabelPrinterSettingsHelper.DefaultModel, "Configured Zebra printer model reference.");
            UpsertSetting(repository, settings, LabelPrinterSettingsHelper.ModeKey, labelPrinter.Mode ?? LabelPrinterSettingsHelper.ModeZebraBrowserPrint, "Label print mode: ZebraBrowserPrint or Browser.");
            UpsertSetting(repository, settings, LabelPrinterSettingsHelper.DeviceNameKey, labelPrinter.DeviceName ?? string.Empty, "Optional Windows printer name for Zebra Browser Print.");
            UpsertSetting(repository, settings, LabelPrinterSettingsHelper.WidthMmKey, labelPrinter.WidthMm.ToString(), "Label width in millimeters.");
            UpsertSetting(repository, settings, LabelPrinterSettingsHelper.HeightMmKey, labelPrinter.HeightMm.ToString(), "Label height in millimeters.");
            UpsertSetting(repository, settings, LabelPrinterSettingsHelper.QrMagnificationKey, labelPrinter.QrMagnification.ToString(System.Globalization.CultureInfo.InvariantCulture), "ZPL QR magnification factor (0.5-10).");

            if (includeLayoutDesign)
            {
                UpsertSetting(repository, settings, LabelPrinterSettingsHelper.LayoutPresetKey, LabelPrinterSettingsHelper.LayoutCustom, "Label layout preset.");
                UpsertSetting(repository, settings, LabelPrinterSettingsHelper.LayoutDesignKey, labelPrinter.LayoutDesignJson ?? string.Empty, "Custom label layout design JSON.");
            }
            else
            {
                UpsertSetting(repository, settings, LabelPrinterSettingsHelper.LayoutPresetKey, labelPrinter.LayoutPreset ?? LabelPrinterSettingsHelper.LayoutQrWithMeta, "Label layout preset.");
            }
        }
    }
}

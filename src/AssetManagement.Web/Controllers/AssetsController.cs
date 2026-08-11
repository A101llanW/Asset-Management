using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Application.Services;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Application.Security;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Assets.View")]
    [ModuleAccess(ModulePermissionCatalog.Assets)]
    public class AssetsController : BaseController
    {
        private readonly IAssetService _assetService;
        private readonly IAssetBulkService _assetBulkService;
        private readonly IAssetImportService _assetImportService;
        private readonly IAuthorizationService _authorizationService;

        public AssetsController()
        {
            _assetService = BuildAssetService();
            _assetBulkService = DependencyResolver.Current.GetService<IAssetBulkService>();
            _assetImportService = DependencyResolver.Current.GetService<IAssetImportService>();
            _authorizationService = BuildAuthorizationService();
        }

        public ActionResult Index(AssetFilterVm filter, string sort = "tag", string direction = "asc", int page = 1, int pageSize = 10, string view = "grouped")
        {
            filter = ListRoleDefaultsHelper.ApplyAssetListDefaults(
                filter,
                GetCurrentUserProfile(),
                CanApproveAssetRequests(),
                IsCurrentUserSuperAdmin());
            if (filter == null)
            {
                filter = new AssetFilterVm();
            }

            filter.ListViewMode = string.Equals(view, "grouped", StringComparison.OrdinalIgnoreCase) ? "grouped" : "flat";
            ViewBag.ListViewMode = filter.ListViewMode;
            ViewBag.Departments = BuildAssetFilterDepartmentSelectList(filter?.DepartmentId);
            ViewBag.Statuses = new SelectList(System.Enum.GetValues(typeof(AssetStatus)).Cast<AssetStatus>().Select(x => new { Value = x, Text = x.ToString() }), "Value", "Text", filter?.Status);
            ViewBag.CanBulkEdit = HtmlHasPermission("Assets.Edit");
            ViewBag.CanRelocateAsset = HtmlHasPermission("Assets.Edit") || HtmlHasPermission("Assets.Transfer");
            ViewBag.ClassDepartments = BuildClassDepartmentSelectList();
            ViewBag.Filter = filter;
            SetListSortViewBag(sort, direction);

            if (string.Equals(filter.ListViewMode, "grouped", StringComparison.OrdinalIgnoreCase))
            {
                var groupedPage = _assetService.GetAssetGroupListPage(filter, sort, direction, page, pageSize);
                ViewBag.GroupedPage = groupedPage;
                return View(ToAssetListPage(new AssetListPageVm
                {
                    Items = new List<AssetListVm>(),
                    TotalCount = groupedPage.TotalCount,
                    Search = groupedPage.Search,
                    Sort = groupedPage.Sort,
                    Direction = groupedPage.Direction,
                    Page = groupedPage.Page,
                    PageSize = groupedPage.PageSize
                }));
            }

            var pageModel = _assetService.GetAssetListPage(filter, sort, direction, page, pageSize);
            EnrichAssetListCustodianNames(pageModel.Items);
            return View(ToAssetListPage(pageModel));
        }

        [PermissionAuthorize("Assets.View")]
        public JsonResult GroupMembers(
            AssetFilterVm filter,
            string assetName,
            int? assetSubTypeId,
            int? groupDepartmentId,
            AssetStatus groupStatus,
            int skip = 0,
            int take = 10)
        {
            filter = ListRoleDefaultsHelper.ApplyAssetListDefaults(
                filter,
                GetCurrentUserProfile(),
                CanApproveAssetRequests(),
                IsCurrentUserSuperAdmin());
            if (filter == null)
            {
                filter = new AssetFilterVm();
            }

            if (string.IsNullOrWhiteSpace(assetName))
            {
                return Json(new { items = new object[0], totalCount = 0, skip = 0, take = take, hasMore = false, remainingCount = 0 }, JsonRequestBehavior.AllowGet);
            }

            var canRelocateAsset = HtmlHasPermission("Assets.Edit") || HtmlHasPermission("Assets.Transfer");
            var pageModel = _assetService.GetAssetGroupMembers(filter, assetName, assetSubTypeId, groupDepartmentId, groupStatus, skip, take);
            EnrichAssetListCustodianNames(pageModel.Items);

            var items = pageModel.Items.Select(x => new
            {
                id = x.Id,
                assetTag = x.AssetTag,
                assetName = x.AssetName,
                brandModel = DisplayText.FormatBrandModel(x.Brand, x.Model),
                custodianName = string.IsNullOrWhiteSpace(x.CurrentCustodianName) ? DisplayText.Unassigned : x.CurrentCustodianName,
                acquisitionCost = x.AcquisitionCost,
                acquisitionCostDisplay = CurrencyFormatter.Format(x.AcquisitionCost),
                detailsUrl = Url.Action("Details", new { id = x.Id }),
                canMove = canRelocateAsset
                    && string.IsNullOrWhiteSpace(x.CurrentCustodianId)
                    && (x.CurrentStatus == AssetStatus.InStore
                        || x.CurrentStatus == AssetStatus.Received
                        || x.CurrentStatus == AssetStatus.Returned)
            }).ToList();

            var loadedCount = pageModel.Skip + pageModel.Items.Count;
            return Json(new
            {
                items = items,
                totalCount = pageModel.TotalCount,
                skip = pageModel.Skip,
                take = pageModel.Take,
                hasMore = loadedCount < pageModel.TotalCount,
                remainingCount = Math.Max(0, pageModel.TotalCount - loadedCount)
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Edit")]
        public ActionResult Bulk(AssetBulkActionRequestVm request)
        {
            if (request == null)
            {
                TempData["Error"] = "Bulk action request is required.";
                return RedirectToAction("Index");
            }

            request.PermissionCodes = BuildBulkPermissionCodes();
            try
            {
                var result = _assetBulkService.Execute(request, User.GetUserId());
                TempData["Message"] = "Bulk action completed: " + result.ProcessedCount + " updated, " + result.SkippedCount + " skipped.";
                if (result.Messages != null && result.Messages.Count > 0)
                {
                    TempData["Guidance"] = string.Join(" ", result.Messages.Take(5));
                }
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AnyPermissionAuthorize("Assets.Edit", "Assets.Transfer")]
        public ActionResult RelocateToClass(
            int assetId,
            int targetDepartmentId,
            AssetFilterVm filter,
            string sort = "tag",
            string direction = "asc",
            int page = 1,
            int pageSize = 10,
            string view = "grouped")
        {
            try
            {
                _assetService.RelocateToClassDepartment(assetId, targetDepartmentId, User.GetUserId());
                TempData["Message"] = "Asset moved to the selected class.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            filter = filter ?? new AssetFilterVm();
            return RedirectToAction("Index", new
            {
                Search = filter.Search,
                DepartmentId = filter.DepartmentId,
                Status = filter.Status,
                sort,
                direction,
                page,
                pageSize,
                view
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AnyPermissionAuthorize("Assets.Edit", "Assets.Transfer")]
        public ActionResult RelocateGroupToClass(
            string assetName,
            int? assetSubTypeId,
            int? groupDepartmentId,
            AssetStatus groupStatus,
            int targetDepartmentId,
            AssetFilterVm filter,
            string sort = "tag",
            string direction = "asc",
            int page = 1,
            int pageSize = 10,
            string view = "grouped")
        {
            try
            {
                var result = _assetService.RelocateGroupToClassDepartment(
                    assetName,
                    assetSubTypeId,
                    groupDepartmentId,
                    groupStatus,
                    targetDepartmentId,
                    User.GetUserId());
                TempData["Message"] = "Group move completed: " + result.ProcessedCount + " moved, " + result.SkippedCount + " skipped.";
                if (result.Messages != null && result.Messages.Count > 0)
                {
                    TempData["Guidance"] = string.Join(" ", result.Messages.Take(5));
                }
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            filter = filter ?? new AssetFilterVm();
            return RedirectToAction("Index", new
            {
                Search = filter.Search,
                DepartmentId = filter.DepartmentId,
                Status = filter.Status,
                sort,
                direction,
                page,
                pageSize,
                view
            });
        }

        [PermissionAuthorize("Assets.View")]
        public ActionResult MyAssets(AssetFilterVm filter, string sort = "tag", string direction = "asc", int page = 1, int pageSize = 10)
        {
            if (filter == null)
            {
                filter = new AssetFilterVm();
            }

            filter.CustodianUserId = User.GetUserId();
            var pageModel = _assetService.GetAssetListPage(filter, sort, direction, page, pageSize);
            EnrichAssetListCustodianNames(pageModel.Items);
            ViewBag.ShowCustodianSelfService = true;
            ViewBag.ListTitle = "My Assets";
            ViewBag.ListSubtitle = "Assets currently assigned to you.";
            SetListSortViewBag(sort, direction);
            return View("CustodyList", ToAssetListPage(pageModel));
        }

        [PermissionAuthorize("Assets.View")]
        public ActionResult DepartmentAssets(AssetFilterVm filter, string sort = "tag", string direction = "asc", int page = 1, int pageSize = 10)
        {
            if (filter == null)
            {
                filter = new AssetFilterVm();
            }

            var user = BuildUserService().GetById(User.GetUserId());
            if (user == null || !user.DepartmentId.HasValue)
            {
                TempData["Error"] = "Your user profile has no department. Contact an administrator.";
                return RedirectToAction("Index");
            }

            filter.DepartmentId = user.DepartmentId;
            var pageModel = _assetService.GetAssetListPage(filter, sort, direction, page, pageSize);
            EnrichAssetListCustodianNames(pageModel.Items);
            ViewBag.ListTitle = "Department Assets";
            ViewBag.ListSubtitle = "Assets registered to your department.";
            SetListSortViewBag(sort, direction);
            return View("CustodyList", ToAssetListPage(pageModel));
        }

        [PermissionAuthorize("Assets.View")]
        public ActionResult Details(int id)
        {
            var model = _assetService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            var currentRoleId = GetCurrentUserRoleId();
            var isSuperAdmin = IsCurrentUserSuperAdmin();
            var currentUserId = User.GetUserId();
            foreach (var pendingTransfer in model.PendingTransfers)
            {
                pendingTransfer.CanCurrentUserApprove = ApprovalWorkflowHelper.CanUserActOnStage(
                    pendingTransfer.RequestedByName,
                    currentUserId,
                    isSuperAdmin,
                    currentRoleId,
                    pendingTransfer.CurrentStageRoleId,
                    pendingTransfer.CurrentStageUserId);
            }

            if (model.PendingDisposal != null)
            {
                model.PendingDisposal.CanCurrentUserApprove = ApprovalWorkflowHelper.CanUserActOnStage(
                    model.PendingDisposal.RequestedByName,
                    currentUserId,
                    isSuperAdmin,
                    currentRoleId,
                    model.PendingDisposal.CurrentStageRoleId,
                    model.PendingDisposal.CurrentStageUserId);
            }

            var entity = UnitOfWork.Repository<Asset>().GetById(id);
            ViewBag.TransferApprovalSummary = BuildAssetApprovalProcessSummary(entity, ApprovalProcessCodes.Transfer);
            ViewBag.DisposalApprovalSummary = BuildAssetApprovalProcessSummary(entity, ApprovalProcessCodes.Disposal);
            ViewBag.AssetLabelPrint = AssetLabelPrintHelper.CreateModel(Request, Url, model, GetExternalBaseUrl());
            ViewBag.AssetId = id;
            ViewBag.AssetAuditLogs = BuildAuditLogService()
                .GetLogs(new AuditLogFilterVm { RelatedAssetId = id, BusinessEventsOnly = true })
                .OrderByDescending(x => x.Timestamp)
                .Take(30)
                .ToList();
            ViewBag.DisposalBlockedReason = BuildDisposalBlockedReason(model);
            EnrichAssetDetails(model);
            model.Documents = BuildAssetDocumentService().GetByAsset(id).ToList();
            model.DocumentRows = BuildAssetDocumentRequirementService().GetStatusRowsByAsset(id).ToList();
            ViewBag.PendingDocumentRequirementId = Request.QueryString["uploadRequirement"];

            return View(model);
        }

        [PermissionAuthorize("Assets.View")]
        public ActionResult PrintLabel(int id)
        {
            var model = AssetLabelPrintHelper.CreateModel(_assetService, Request, Url, id, GetExternalBaseUrl());
            if (model == null)
            {
                return HttpNotFound();
            }

            return View(model);
        }

        [PermissionAuthorize("Assets.View")]
        public ActionResult LabelZpl(int id)
        {
            var model = AssetLabelPrintHelper.CreateModel(_assetService, Request, Url, id, GetExternalBaseUrl());
            if (model == null)
            {
                return HttpNotFound();
            }

            var settings = GetLabelPrinterSettings();
            var zpl = ZplLabelBuilder.Build(ToZplLabelData(model), settings);
            return Content(zpl, "text/plain");
        }

        [PermissionAuthorize("Assets.View")]
        public JsonResult LabelPrintConfig(int id)
        {
            var model = AssetLabelPrintHelper.CreateModel(_assetService, Request, Url, id, GetExternalBaseUrl());
            if (model == null)
            {
                return Json(new { error = "Asset not found." }, JsonRequestBehavior.AllowGet);
            }

            var settings = GetLabelPrinterSettings();
            return Json(new
            {
                enabled = settings.Enabled,
                mode = settings.Mode,
                deviceName = settings.DeviceName,
                zplUrl = Url.Action("LabelZpl", new { id = model.AssetId }),
                labelWidthMm = settings.WidthMm,
                labelHeightMm = settings.HeightMm,
                assetId = model.AssetId
            }, JsonRequestBehavior.AllowGet);
        }

        private string GetExternalBaseUrl()
        {
            var platformSettings = DependencyResolver.Current.GetService<IPlatformSettingsService>();
            return platformSettings == null ? null : platformSettings.GetExternalBaseUrl();
        }

        private LabelPrinterSettingsVm GetLabelPrinterSettings()
        {
            return LabelPrinterSettingsHelper.FromDictionary(
                ApprovalWorkflowSettingsHelper.ToDictionary(UnitOfWork.Repository<SystemSetting>().GetAll()));
        }

        private static ZplLabelData ToZplLabelData(AssetManagement.Web.ViewModels.AssetLabelPrintVm model)
        {
            return new ZplLabelData
            {
                AssetTag = model.AssetTag,
                AssetName = model.AssetName,
                DepartmentName = model.DepartmentName,
                SerialNumber = model.SerialNumber,
                ScanUrl = model.ScanUrl
            };
        }

        [PermissionAuthorize("Assets.Create")]
        public ActionResult Create()
        {
            var model = new AssetCreateVm
            {
                CurrentStatus = AssetManagement.Domain.Enums.AssetStatus.InStore,
                PurchaseDate = System.DateTime.Today,
                DepreciationStartDate = System.DateTime.Today,
                ApprovalProcesses = AssetApprovalSettingsHelper.BuildDefaultProcesses(UnitOfWork, GetRolesForOrganization(), ResolveCurrentOrganizationId()).ToList()
            };

            ApplyAssetFormDefaults(model);
            PopulateLookups(model);
            PopulateDepreciationContext(model);
            PopulateAssetApprovalFormOptions();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Create")]
        public ActionResult Create([Bind(Prefix = "")] AssetCreateVm viewModel)
        {
            if (viewModel == null)
            {
                viewModel = new AssetCreateVm
                {
                    CurrentStatus = AssetManagement.Domain.Enums.AssetStatus.InStore
                };
            }

            if (viewModel.CurrentStatus == 0)
            {
                viewModel.CurrentStatus = AssetManagement.Domain.Enums.AssetStatus.InStore;
            }

            AssetTaxInputHelper.ApplyTaxInput(viewModel);
            ApplyAssetFormDefaults(viewModel);
            ClearOptionalAssetFieldErrors(viewModel);
            PopulateLookups(viewModel);
            PopulateDepreciationContext(viewModel);
            PopulateAssetApprovalFormOptions();
            AssetApprovalSettingsHelper.ValidateApprovalProcesses(viewModel.ApprovalProcesses, (key, message) => ModelState.AddModelError(key, message));
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
                viewModel.CanManageDepreciationSettings = CanManageDepreciationSettings();
                var assetId = _assetService.Create(viewModel);
                TempData["Message"] = "Asset created successfully.";
                TempData["Guidance"] = "Next step: review the asset details, then assign it, transfer it, or add maintenance and insurance information.";
                return RedirectToAction("Details", new { id = assetId });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                PopulateAssetApprovalFormOptions();
                return View(viewModel);
            }
        }

        [PermissionAuthorize("Assets.Create")]
        public ActionResult Import()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Create")]
        public ActionResult Import(System.Web.HttpPostedFileBase importFile)
        {
            if (importFile == null || importFile.ContentLength == 0)
            {
                TempData["Error"] = "Select an Excel or CSV file to import.";
                return RedirectToAction("Import");
            }

            try
            {
                using (var stream = importFile.InputStream)
                {
                    var result = _assetImportService.Import(stream, importFile.FileName, User.GetUserId());
                    TempData["Message"] = "Import completed: " + result.ImportedCount + " assets created, " + result.SkippedCount + " skipped.";
                    if (result.Messages != null && result.Messages.Count > 0)
                    {
                        TempData["Guidance"] = string.Join(" ", result.Messages.Take(10));
                    }
                }
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Import");
            }

            return RedirectToAction("Index");
        }

        [PermissionAuthorize("Assets.Create")]
        public ActionResult DownloadImportTemplate()
        {
            return File(
                _assetImportService.GetImportTemplate(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "asset-import-template.xlsx");
        }

        [PermissionAuthorize("Assets.Edit")]
        public ActionResult Edit(int id)
        {
            var item = _assetService.GetById(id);
            var entity = UnitOfWork.Repository<Asset>().GetById(id);
            if (item == null)
            {
                return HttpNotFound();
            }

            var model = new AssetEditVm
            {
                Id = item.Id,
                AssetName = item.AssetName,
                AssetTag = item.AssetTag,
                SerialNumber = item.SerialNumber,
                Brand = item.Brand,
                Model = item.Model,
                CategoryId = entity.CategoryId,
                AssetTypeId = entity.AssetTypeId,
                AssetSubTypeId = entity.AssetSubTypeId,
                AssetSubTypeName = entity.AssetSubTypeId.HasValue
                    ? ResolveAssetSubTypeName(entity.AssetSubTypeId.Value)
                    : null,
                DepartmentId = entity.DepartmentId,
                SupplierId = entity.SupplierId,
                PurchaseDate = entity.PurchaseDate,
                AcquisitionCost = item.AcquisitionCost,
                Currency = entity.Currency,
                CurrentStatus = item.CurrentStatus,
                UsefulLifeMonths = entity.UsefulLifeMonths,
                SalvageValue = entity.SalvageValue,
                TaxAmount = entity.TaxAmount,
                ConditionOnReceipt = entity.ConditionOnReceipt,
                DepreciationMethod = entity.DepreciationMethod,
                DepreciationStartDate = entity.DepreciationStartDate,
                UseCustomDepreciationLife = entity.DepreciationLifeMonths.HasValue,
                DepreciationLifeMonths = entity.DepreciationLifeMonths,
                UseCustomDepreciationRate = entity.DepreciationRatePercent.HasValue,
                DepreciationRatePercent = entity.DepreciationRatePercent,
                IsInsured = entity.IsInsured,
                InsuredValue = entity.InsuredValue,
                WarrantyStartDate = entity.WarrantyStartDate,
                WarrantyEndDate = entity.WarrantyEndDate,
                Description = entity.Description,
                ApprovalProcesses = AssetApprovalSettingsHelper.BuildFromAsset(
                    entity,
                    UnitOfWork,
                    GetRolesForOrganization(entity.OrganizationId),
                    entity.OrganizationId.HasValue
                        ? ApproverPickerHelper.BuildUserNameLookup(
                            BuildReferenceDataCache().GetUsersForDropdown(entity.OrganizationId.Value))
                        : null).ToList()
            };

            AssetTaxInputHelper.SeedTaxInputFromStoredAmount(model);
            ApplyAssetFormDefaults(model);
            PopulateLookups(model);
            PopulateDepreciationContext(model, entity);
            PopulateAssetApprovalFormOptions(entity.OrganizationId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Edit")]
        public ActionResult Edit([Bind(Prefix = "")] AssetEditVm viewModel)
        {
            var assetEntity = UnitOfWork.Repository<Asset>().GetById(viewModel.Id);
            AssetTaxInputHelper.ApplyTaxInput(viewModel);
            ApplyAssetFormDefaults(viewModel);
            ClearOptionalAssetFieldErrors(viewModel);
            PopulateLookups(viewModel);
            PopulateDepreciationContext(viewModel, assetEntity);
            PopulateAssetApprovalFormOptions(assetEntity == null ? null : assetEntity.OrganizationId);
            AssetApprovalSettingsHelper.ValidateApprovalProcesses(viewModel.ApprovalProcesses, (key, message) => ModelState.AddModelError(key, message));
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
                viewModel.CanManageDepreciationSettings = CanManageDepreciationSettings();
                _assetService.Update(viewModel);
                TempData["Message"] = "Asset updated successfully.";
                return RedirectToAction("Details", new { id = viewModel.Id });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                PopulateAssetApprovalFormOptions(assetEntity == null ? null : assetEntity.OrganizationId);
                return View(viewModel);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Delete")]
        public ActionResult Delete(int id)
        {
            _assetService.Delete(id);
            TempData["Message"] = "Asset archived.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Dispose")]
        public ActionResult RequestDisposal(int id, string disposalReason, DisposalMethod disposalMethod, string notes)
        {
            try
            {
                _assetService.RequestDisposal(new AssetDisposalRequestVm
                {
                    AssetId = id,
                    DisposalReason = disposalReason,
                    DisposalMethod = disposalMethod,
                    Notes = notes
                }, User.GetUserId());
                TempData["Message"] = "Disposal request submitted.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAssetDetailsTab(id, "disposal");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.ApproveDisposal")]
        public ActionResult ApproveDisposal(int id, decimal? disposalAmount, string notes)
        {
            try
            {
                _assetService.ApproveDisposal(new AssetDisposalApprovalVm
                {
                    AssetId = id,
                    DisposalAmount = disposalAmount,
                    Notes = notes
                }, User.GetUserId(), GetCurrentUserRoleId(), IsCurrentUserSuperAdmin());
                var asset = _assetService.GetById(id);
                TempData["Message"] = asset != null && asset.CurrentStatus == AssetStatus.Disposed
                    ? "Disposal approved and asset marked as disposed."
                    : "Disposal stage approved. Additional approval may still be required.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAssetDetailsTab(id, "disposal");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.ApproveDisposal")]
        public ActionResult RejectDisposal(int id, string notes)
        {
            try
            {
                _assetService.RejectDisposal(new AssetDisposalApprovalVm
                {
                    AssetId = id,
                    Notes = notes
                }, User.GetUserId(), GetCurrentUserRoleId(), IsCurrentUserSuperAdmin());
                TempData["Message"] = "Disposal request rejected.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAssetDetailsTab(id, "disposal");
        }

        private string BuildDisposalBlockedReason(AssetDetailsVm model)
        {
            if (model == null)
            {
                return null;
            }

            if (model.CurrentStatus == AssetStatus.Disposed || model.CurrentStatus == AssetStatus.Retired)
            {
                return "This asset is already disposed or retired.";
            }

            if (model.PendingDisposal != null)
            {
                return null;
            }

            if (model.CurrentStatus == AssetStatus.AwaitingApproval)
            {
                return "Another approval workflow is in progress for this asset (for example a transfer). Resolve it before requesting disposal.";
            }

            return null;
        }

        private ActionResult RedirectToAssetDetailsTab(int id, string tab)
        {
            var url = Url.Action("Details", new { id }) + "#" + tab;
            return Redirect(url);
        }

        private bool CanApproveAssetRequests()
        {
            return _authorizationService.HasPermission(User.GetUserId(), "Assets.Request.Approve");
        }

        private IList<string> BuildBulkPermissionCodes()
        {
            var userId = User.GetUserId();
            var codes = new List<string>();
            if (_authorizationService.HasPermission(userId, "Assets.Edit"))
            {
                codes.Add("Assets.Edit");
            }

            if (_authorizationService.HasPermission(userId, "Assets.Assign"))
            {
                codes.Add("Assets.Assign");
            }

            return codes;
        }

        private bool HtmlHasPermission(string code)
        {
            return _authorizationService.HasPermission(User.GetUserId(), code);
        }

        private void PopulateLookups(AssetCreateVm model)
        {
            var categories = UnitOfWork.Repository<AssetCategory>().GetAll().OrderBy(x => x.Name).ToList();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", model?.CategoryId);

            var assetTypes = UnitOfWork.Repository<AssetType>().GetAll()
                .OrderBy(x => x.Name)
                .ToList();
            ViewBag.AssetTypeOptions = assetTypes;

            ViewBag.Departments = BuildDepartmentSelectList(model?.DepartmentId, activeOnly: false);
            ViewBag.Suppliers = BuildSupplierSelectList(model?.SupplierId, activeOnly: false);
            ViewBag.OrganizationCurrency = GetDefaultCurrencyCode();
            ViewBag.SubTypeLookupUrl = TenantUrlHelper.TenantRouteUrl(Url, "Lookup", "AssetSubTypes");
            ViewBag.SubTypeByTypeUrl = TenantUrlHelper.TenantRouteUrl(Url, "ByType", "AssetSubTypes");
            ViewBag.SubTypeCreateUrl = TenantUrlHelper.TenantRouteUrl(Url, "CreateFromAsset", "AssetSubTypes");
        }

        private string ResolveAssetSubTypeName(int subTypeId)
        {
            var subType = BuildAssetSubTypeService().GetById(subTypeId);
            return subType == null ? null : subType.Name;
        }

        private bool CanManageDepreciationSettings()
        {
            var userId = User.GetUserId();
            return _authorizationService.HasPermission(userId, "Depreciation.Manage")
                || _authorizationService.HasPermission(userId, "Financials.Edit");
        }

        private void PopulateDepreciationContext(AssetCreateVm model, Asset entity = null)
        {
            ViewBag.CanManageDepreciation = CanManageDepreciationSettings();
            if (model == null || model.CategoryId <= 0 || model.AssetTypeId <= 0)
            {
                ViewBag.EffectiveDepreciationLifeMonths = null;
                ViewBag.EffectiveDepreciationRatePercent = null;
                ViewBag.EffectiveDepreciationLifeSource = null;
                ViewBag.EffectiveDepreciationRateSource = null;
                return;
            }

            var previewAsset = entity ?? new Asset
            {
                CategoryId = model.CategoryId,
                AssetTypeId = model.AssetTypeId,
                UsefulLifeMonths = UsefulLifeResolver.Resolve(UnitOfWork, model.AssetTypeId, model.CategoryId),
                DepreciationLifeMonths = model.UseCustomDepreciationLife ? model.DepreciationLifeMonths : null,
                DepreciationRatePercent = model.UseCustomDepreciationRate ? model.DepreciationRatePercent : null,
                DepreciationMethod = model.DepreciationMethod,
                AcquisitionCost = model.AcquisitionCost,
                SalvageValue = model.SalvageValue,
                CurrentBookValue = model.AcquisitionCost
            };

            if (entity != null)
            {
                previewAsset.UsefulLifeMonths = entity.UsefulLifeMonths;
                previewAsset.DepreciationLifeMonths = model.UseCustomDepreciationLife ? model.DepreciationLifeMonths : entity.DepreciationLifeMonths;
                previewAsset.DepreciationRatePercent = model.UseCustomDepreciationRate ? model.DepreciationRatePercent : entity.DepreciationRatePercent;
                previewAsset.CurrentBookValue = entity.CurrentBookValue;
            }

            var assetType = UnitOfWork.Repository<AssetType>().GetById(model.AssetTypeId);
            var category = UnitOfWork.Repository<AssetCategory>().GetById(model.CategoryId);
            var settings = DepreciationSettingsResolver.Resolve(previewAsset, assetType, category);
            ViewBag.EffectiveDepreciationLifeMonths = settings.LifeMonths;
            ViewBag.EffectiveDepreciationRatePercent = settings.AnnualRatePercent;
            ViewBag.EffectiveDepreciationLifeSource = settings.LifeSource;
            ViewBag.EffectiveDepreciationRateSource = settings.RateSource;
        }

        private void ApplyAssetFormDefaults(AssetCreateVm model)
        {
            if (model == null)
            {
                return;
            }

            model.Currency = GetDefaultCurrencyCode();
            if (model.DepartmentId.HasValue && model.DepartmentId.Value <= 0)
            {
                model.DepartmentId = null;
            }

            if (model.SupplierId.HasValue && model.SupplierId.Value <= 0)
            {
                model.SupplierId = null;
            }
        }

        private void ClearOptionalAssetFieldErrors(AssetCreateVm model)
        {
            ModelState.Remove("CurrentStatus");
            ModelState.Remove("AssetTag");
            ModelState.Remove("Currency");
            ModelState.Remove("DepartmentId");
            ModelState.Remove("SupplierId");

            if (model == null)
            {
                return;
            }

            ModelState.SetModelValue("Currency", new ValueProviderResult(model.Currency, model.Currency, System.Globalization.CultureInfo.InvariantCulture));
            ModelState.SetModelValue(
                "DepartmentId",
                new ValueProviderResult(
                    model.DepartmentId?.ToString() ?? string.Empty,
                    model.DepartmentId?.ToString() ?? string.Empty,
                    System.Globalization.CultureInfo.InvariantCulture));
            ModelState.SetModelValue(
                "SupplierId",
                new ValueProviderResult(
                    model.SupplierId?.ToString() ?? string.Empty,
                    model.SupplierId?.ToString() ?? string.Empty,
                    System.Globalization.CultureInfo.InvariantCulture));
        }

        private void PopulateRoleOptions()
        {
            ViewBag.RoleOptions = BuildRoleOptionList();
        }

        private void PopulateAssetApprovalFormOptions(int? organizationId = null)
        {
            PopulateRoleOptions();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Application.Services;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Assets.View")]
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

        public ActionResult Index(AssetFilterVm filter, string sort = "tag", string direction = "asc", int page = 1, int pageSize = 10)
        {
            filter = ListRoleDefaultsHelper.ApplyAssetListDefaults(
                filter,
                GetCurrentUserProfile(),
                CanApproveAssetRequests(),
                IsCurrentUserSuperAdmin());
            var pageModel = _assetService.GetAssetListPage(filter, sort, direction, page, pageSize);
            EnrichAssetListCustodianNames(pageModel.Items);
            ViewBag.Departments = BuildDepartmentSelectList(filter?.DepartmentId, activeOnly: false);
            ViewBag.Statuses = new SelectList(System.Enum.GetValues(typeof(AssetStatus)).Cast<AssetStatus>().Select(x => new { Value = x, Text = x.ToString() }), "Value", "Text", filter?.Status);
            ViewBag.CanBulkEdit = HtmlHasPermission("Assets.Edit");
            ViewBag.Filter = filter;
            SetListSortViewBag(sort, direction);
            return View(ToAssetListPage(pageModel));
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
            ViewBag.AssetLabelPrint = AssetLabelPrintHelper.CreateModel(Request, Url, model);
            ViewBag.AssetId = id;
            ViewBag.AssetAuditLogs = BuildAuditLogService()
                .GetLogs(new AuditLogFilterVm { RelatedAssetId = id })
                .OrderByDescending(x => x.Timestamp)
                .Take(30)
                .ToList();
            ViewBag.DisposalBlockedReason = BuildDisposalBlockedReason(model);
            EnrichAssetDetails(model);
            model.Documents = BuildAssetDocumentService().GetByAsset(id).ToList();

            return View(model);
        }

        [PermissionAuthorize("Assets.View")]
        public ActionResult PrintLabel(int id)
        {
            var model = AssetLabelPrintHelper.CreateModel(_assetService, Request, Url, id);
            if (model == null)
            {
                return HttpNotFound();
            }

            return View(model);
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
            PopulateAssetApprovalFormOptions();
            AssetApprovalSettingsHelper.ValidateApprovalProcesses(viewModel.ApprovalProcesses, (key, message) => ModelState.AddModelError(key, message));
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
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
                "text/csv",
                "asset-import-template.csv");
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
            PopulateAssetApprovalFormOptions(entity.OrganizationId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Edit")]
        public ActionResult Edit([Bind(Prefix = "")] AssetEditVm viewModel)
        {
            AssetTaxInputHelper.ApplyTaxInput(viewModel);
            ApplyAssetFormDefaults(viewModel);
            ClearOptionalAssetFieldErrors(viewModel);
            PopulateLookups(viewModel);
            var assetEntity = UnitOfWork.Repository<Asset>().GetById(viewModel.Id);
            PopulateAssetApprovalFormOptions(assetEntity == null ? null : assetEntity.OrganizationId);
            AssetApprovalSettingsHelper.ValidateApprovalProcesses(viewModel.ApprovalProcesses, (key, message) => ModelState.AddModelError(key, message));
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
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
            PopulateAssetApproverPickerOptions(organizationId);
        }
    }
}

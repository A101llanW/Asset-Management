using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Suppliers.View")]
    public class SuppliersController : BaseController
    {
        private readonly ISupplierService _supplierService;
        private readonly ISupplierCatalogService _supplierCatalogService;

        public SuppliersController()
        {
            _supplierService = BuildSupplierService();
            _supplierCatalogService = BuildSupplierCatalogService();
        }

        public ActionResult Index(string search = null, string sort = "name", string direction = "asc", int page = 1, int pageSize = 10)
        {
            var items = _supplierService.GetAll();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                items = items.Where(x => (x.SupplierName ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.ContactPerson ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.Email ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.Phone ?? string.Empty).ToLowerInvariant().Contains(term));
            }

            switch ((sort ?? string.Empty).ToLowerInvariant())
            {
                case "contact":
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase) ? items.OrderByDescending(x => x.ContactPerson) : items.OrderBy(x => x.ContactPerson);
                    break;
                case "status":
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase) ? items.OrderByDescending(x => x.IsActive) : items.OrderBy(x => x.IsActive);
                    break;
                default:
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase) ? items.OrderByDescending(x => x.SupplierName) : items.OrderBy(x => x.SupplierName);
                    sort = "name";
                    break;
            }

            SetListSortViewBag(sort, direction);
            return View(BuildListPage(items, search, sort, direction, page, pageSize));
        }

        public ActionResult Details(int id, string returnUrl = null)
        {
            var model = _supplierService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.PurchaseCount = BuildPurchaseService().GetAll().Count(x => x.SupplierId == id);
            ViewBag.AssetCount = BuildAssetService().CountAssets(new AssetFilterVm { SupplierId = id });
            ViewBag.CatalogItems = _supplierCatalogService.GetBySupplier(id).ToList();
            ViewBag.CanEditCatalog = HtmlHasPermission("Suppliers.Edit");
            ViewBag.Categories = BuildCategorySelectList(null);
            ViewBag.AssetTypes = GetActiveAssetTypes();
            ViewBag.DefaultCurrency = GetDefaultCurrencyCode();
            ViewBag.AssetTypesUrl = Url.Action("AvailableAssetTypes");
            ViewBag.TaggedAssetsUrl = Url.Action("AvailableTaggedAssets");
            return View(model);
        }

        public JsonResult AvailableAssetTypes(int? categoryId)
        {
            var query = UnitOfWork.Repository<AssetType>().GetAll().Where(x => x.IsActive);
            if (categoryId.HasValue)
            {
                query = query.Where(x => x.AssetCategoryId == categoryId.Value);
            }

            var items = query
                .OrderBy(x => x.Name)
                .Select(x => new { id = x.Id, name = x.Name, categoryId = x.AssetCategoryId })
                .ToList();
            return Json(items, JsonRequestBehavior.AllowGet);
        }

        public JsonResult AvailableTaggedAssets(int? categoryId, int? assetTypeId)
        {
            var filter = new AssetFilterVm { OrganizationWide = true };
            if (categoryId.HasValue)
            {
                filter.CategoryId = categoryId.Value;
            }
            if (assetTypeId.HasValue)
            {
                filter.AssetTypeId = assetTypeId.Value;
            }

            var page = BuildAssetService().GetAssetListPage(filter, "tag", "asc", 1, 500);
            var items = page.Items
                .Select(x => new { id = x.Id, name = FormatTaggedAssetLabel(x) })
                .ToList();
            return Json(items, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Suppliers.Edit")]
        public ActionResult SaveCatalogItem(SupplierCatalogItemVm model, string returnUrl = null)
        {
            if (model == null || model.SupplierId <= 0)
            {
                TempData["Error"] = "Catalog item is required.";
                return RedirectToAction("Index");
            }

            try
            {
                if (model.Id > 0)
                {
                    _supplierCatalogService.Update(model);
                    TempData["Message"] = "Catalog item updated.";
                }
                else
                {
                    _supplierCatalogService.Create(model);
                    TempData["Message"] = "Catalog item added.";
                }
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id = model.SupplierId, returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Suppliers.Edit")]
        public ActionResult DeactivateCatalogItem(int supplierId, int catalogItemId, string returnUrl = null)
        {
            try
            {
                _supplierCatalogService.Deactivate(catalogItemId);
                TempData["Message"] = "Catalog item deactivated.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id = supplierId, returnUrl });
        }

        [PermissionAuthorize("Suppliers.Create")]
        public ActionResult Create(string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            PopulateCreateLookups();
            return View(BuildCreateModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Suppliers.Create")]
        public ActionResult Create(SupplierCreateVm model, string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            PopulateCreateLookups();
            if (model == null)
            {
                model = BuildCreateModel();
                ModelState.AddModelError("", "Supplier details are required.");
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                EnsureCatalogItemRows(model);
                return View(model);
            }

            try
            {
                var catalogCount = model.CatalogItems == null
                    ? 0
                    : model.CatalogItems.Count(x => x != null && !string.IsNullOrWhiteSpace(x.ItemName));
                var supplierId = _supplierService.CreateWithCatalog(model, model.CatalogItems);
                TempData["Message"] = "Supplier created.";
                TempData["Guidance"] = catalogCount > 0
                    ? "Supplier and catalog prices are saved. Use purchase record creation to compare offers before finalizing a PO."
                    : "Review the supplier profile and add catalog items if needed for price comparison.";
                return RedirectToAction("Details", new { id = supplierId, returnUrl = ViewBag.ReturnUrl });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                EnsureCatalogItemRows(model);
                return View(model);
            }
        }

        [PermissionAuthorize("Suppliers.Edit")]
        public ActionResult Edit(int id, string returnUrl = null)
        {
            var model = _supplierService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", null, new { id });
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Suppliers.Edit")]
        public ActionResult Edit(SupplierVm model, string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", null, new { id = model.Id });
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            _supplierService.Update(model);
            TempData["Message"] = "Supplier updated.";
            return RedirectToReturnUrl(returnUrl, "Details", null, new { id = model.Id });
        }

        private SelectList BuildCategorySelectList(int? selectedCategoryId)
        {
            var categories = UnitOfWork.Repository<AssetManagement.Domain.Entities.AssetCategory>().GetAll()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ToList();
            return new SelectList(categories, "Id", "Name", selectedCategoryId);
        }

        private void PopulateCreateLookups()
        {
            ViewBag.Categories = BuildCategorySelectList(null);
            ViewBag.AssetTypes = GetActiveAssetTypes();
            ViewBag.DefaultCurrency = GetDefaultCurrencyCode();
            ViewBag.AssetTypesUrl = Url.Action("AvailableAssetTypes");
            ViewBag.TaggedAssetsUrl = Url.Action("AvailableTaggedAssets");
        }

        private IList<AssetType> GetActiveAssetTypes()
        {
            return UnitOfWork.Repository<AssetType>().GetAll()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ToList();
        }

        private static string FormatTaggedAssetLabel(AssetListVm asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            var label = string.IsNullOrWhiteSpace(asset.AssetTag)
                ? asset.AssetName
                : asset.AssetTag + " - " + asset.AssetName;
            if (!string.IsNullOrWhiteSpace(asset.DepartmentName))
            {
                label += " · " + asset.DepartmentName;
            }

            return label;
        }

        private SupplierCreateVm BuildCreateModel()
        {
            var currency = GetDefaultCurrencyCode();
            var model = new SupplierCreateVm { IsActive = true };
            model.CatalogItems = new System.Collections.Generic.List<SupplierCatalogItemVm>
            {
                new SupplierCatalogItemVm { Currency = currency },
                new SupplierCatalogItemVm { Currency = currency }
            };
            return model;
        }

        private static void EnsureCatalogItemRows(SupplierCreateVm model)
        {
            if (model.CatalogItems == null)
            {
                model.CatalogItems = new System.Collections.Generic.List<SupplierCatalogItemVm>();
            }

            while (model.CatalogItems.Count < 2)
            {
                model.CatalogItems.Add(new SupplierCatalogItemVm());
            }
        }

        private bool HtmlHasPermission(string permissionCode)
        {
            return BuildAuthorizationService().HasPermission(User.GetUserId(), permissionCode);
        }
    }
}

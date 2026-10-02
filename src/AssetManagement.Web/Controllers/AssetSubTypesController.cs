using System;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;
namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Assets.Edit")]
    public class AssetSubTypesController : BaseController
    {
        private readonly IAssetSubTypeService _assetSubTypeService;
        private readonly IAssetService _assetService;

        public AssetSubTypesController()
        {
            _assetSubTypeService = BuildAssetSubTypeService();
            _assetService = BuildAssetService();
        }
        public ActionResult Index(int? assetTypeId = null, string search = null, bool? activeOnly = true)
        {
            var query = UnitOfWork.Repository<AssetSubType>().GetAll().AsQueryable();
            if (assetTypeId.HasValue && assetTypeId.Value > 0)
            {
                query = query.Where(x => x.AssetTypeId == assetTypeId.Value);
            }

            if (activeOnly != false)
            {
                query = query.Where(x => x.IsActive);
            }

            var loaded = query.OrderBy(x => x.Name).ToList();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                loaded = loaded.Where(x =>
                    (x.Name != null && x.Name.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    || (x.Brand != null && x.Brand.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    || (x.Model != null && x.Model.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    || (x.Sku != null && x.Sku.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            }

            var types = UnitOfWork.Repository<AssetType>().GetAll().ToDictionary(x => x.Id, x => x);
            var categories = UnitOfWork.Repository<AssetCategory>().GetAll().ToDictionary(x => x.Id, x => x.Name);
            var items = loaded.Select(x =>
            {
                AssetType assetType;
                types.TryGetValue(x.AssetTypeId, out assetType);
                string categoryName = null;
                if (assetType != null)
                {
                    categories.TryGetValue(assetType.AssetCategoryId, out categoryName);
                }

                return new AssetSubTypeIndexItemVm
                {
                    Id = x.Id,
                    Name = x.Name,
                    Brand = x.Brand,
                    Model = x.Model,
                    Sku = x.Sku,
                    AssetTypeId = x.AssetTypeId,
                    AssetTypeName = assetType != null ? assetType.Name : null,
                    AssetCategoryName = categoryName,
                    IsActive = x.IsActive
                };
            }).ToList();

            ViewBag.AssetTypeId = assetTypeId;
            ViewBag.Search = search;
            ViewBag.ActiveOnly = activeOnly != false;
            ViewBag.AssetTypes = new SelectList(
                UnitOfWork.Repository<AssetType>().GetAll().Where(x => x.IsActive).OrderBy(x => x.Name).ToList(),
                "Id", "Name", assetTypeId);
            return View(items);
        }

        public ActionResult Create(int? assetTypeId = null, string returnUrl = null)
        {
            if (!assetTypeId.HasValue || assetTypeId.Value <= 0)
            {
                ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index", "AssetTypes");
                ViewBag.AssetTypes = new SelectList(
                    UnitOfWork.Repository<AssetType>().GetAll().Where(x => x.IsActive).OrderBy(x => x.Name).ToList(),
                    "Id", "Name");
                return View("CreateSelectType");
            }

            var assetType = UnitOfWork.Repository<AssetType>().GetById(assetTypeId.Value);
            if (assetType == null)
            {
                return HttpNotFound();
            }

            var model = new AssetSubTypeEditVm
            {
                AssetTypeId = assetTypeId.Value,
                IsActive = true
            };
            PopulateAssetTypeContext(assetType);
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", "AssetTypes", new { id = assetTypeId.Value });
            ViewBag.OrganizationCurrency = GetDefaultCurrencyCode();
            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(AssetSubTypeEditVm model, string returnUrl = null, int assetTypeId = 0)
        {
            model = model ?? new AssetSubTypeEditVm();
            if (model.AssetTypeId <= 0 && assetTypeId > 0)
            {
                model.AssetTypeId = assetTypeId;
                ModelState.Remove(nameof(model.AssetTypeId));
            }
            var assetType = model.AssetTypeId > 0 ? UnitOfWork.Repository<AssetType>().GetById(model.AssetTypeId) : null;
            if (assetType == null)
            {
                ModelState.AddModelError(string.Empty, "Asset type not found.");
                PopulateAssetTypeContext(null);
                ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index", "AssetTypes");
                return View(model);
            }
            PopulateAssetTypeContext(assetType);
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", "AssetTypes", new { id = model.AssetTypeId });
            ViewBag.OrganizationCurrency = GetDefaultCurrencyCode();
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            try
            {
                var id = _assetSubTypeService.Create(model);
                TempData["Message"] = "Asset sub-type created.";
                return RedirectToTenantAware("AssetSubTypes", "Edit", new { id, returnUrl = ViewBag.ReturnUrl });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
        }
        public ActionResult Edit(int id, string returnUrl = null)
        {
            var subType = _assetSubTypeService.GetById(id);
            if (subType == null)
            {
                return HttpNotFound();
            }
            var assetType = UnitOfWork.Repository<AssetType>().GetById(subType.AssetTypeId);
            PopulateAssetTypeContext(assetType);
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", "AssetTypes", new { id = subType.AssetTypeId });
            ViewBag.StockCount = subType.StockCount;
            ViewBag.OrganizationCurrency = GetDefaultCurrencyCode();
            ViewBag.SubTypeAssetCount = _assetSubTypeService.CountAssets(id);
            return View(MapEditVm(subType));
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(AssetSubTypeEditVm model, string returnUrl = null)
        {
            var assetType = model == null ? null : UnitOfWork.Repository<AssetType>().GetById(model.AssetTypeId);
            if (assetType == null)
            {
                return HttpNotFound();
            }
            PopulateAssetTypeContext(assetType);
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", "AssetTypes", new { id = model.AssetTypeId });
            ViewBag.OrganizationCurrency = GetDefaultCurrencyCode();
            ViewBag.SubTypeAssetCount = _assetSubTypeService.CountAssets(model.Id);
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            try
            {
                _assetSubTypeService.Update(model);
                var guidance = ApplySubTypeDefaultCostToAssets(model);
                TempData["Message"] = "Asset sub-type updated.";
                if (!string.IsNullOrWhiteSpace(guidance))
                {
                    TempData["Guidance"] = guidance;
                }

                return RedirectToReturnUrl(returnUrl, "Details", "AssetTypes", new { id = model.AssetTypeId });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
        }
        [PermissionAuthorize("Assets.Create")]
        public JsonResult ByType(int assetTypeId)
        {
            var items = _assetSubTypeService.GetByAssetTypeId(assetTypeId)
                .Select(x => new
                {
                    id = x.Id,
                    name = x.Name,
                    brand = x.Brand,
                    model = x.Model,
                    stockCount = x.StockCount,
                    defaultAcquisitionCost = x.DefaultAcquisitionCost
                })
                .ToList();
            return Json(items, JsonRequestBehavior.AllowGet);
        }
        [PermissionAuthorize("Assets.Create")]
        public JsonResult Lookup(int assetTypeId, string brand = null, string model = null)
        {
            var match = _assetSubTypeService.Lookup(assetTypeId, brand, model);
            if (match == null)
            {
                return Json(new { matched = false }, JsonRequestBehavior.AllowGet);
            }
            return Json(new
            {
                matched = true,
                id = match.Id,
                name = match.Name,
                brand = match.Brand,
                model = match.Model,
                stockCount = match.StockCount,
                defaultAcquisitionCost = match.DefaultAcquisitionCost
            }, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Create")]
        public JsonResult CreateFromAsset(int assetTypeId, string name, string brand, string model, string specifications = null, string sku = null)
        {
            if (assetTypeId <= 0)
            {
                return Json(new { success = false, message = "Please select an asset type." });
            }

            var createModel = new AssetSubTypeCreateFromAssetVm
            {
                AssetTypeId = assetTypeId,
                Name = name,
                Brand = brand,
                Model = model,
                Specifications = specifications,
                Sku = sku
            };

            if (string.IsNullOrWhiteSpace(createModel.Name))
            {
                createModel.Name = AssetSubTypeNormalizer.BuildSuggestedName(createModel.Brand, createModel.Model);
            }
            if (string.IsNullOrWhiteSpace(createModel.Name))
            {
                return Json(new { success = false, message = "Enter a display name for the new sub-type." });
            }
            try
            {
                var id = _assetSubTypeService.CreateFromAsset(createModel);
                var created = _assetSubTypeService.GetById(id);
                var assetType = UnitOfWork.Repository<AssetType>().GetById(created.AssetTypeId);
                return Json(new
                {
                    success = true,
                    id = created.Id,
                    name = created.Name,
                    brand = created.Brand,
                    model = created.Model,
                    assetTypeId = created.AssetTypeId,
                    categoryId = assetType == null ? (int?)null : assetType.AssetCategoryId
                });
            }
            catch (BusinessException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        private void PopulateAssetTypeContext(AssetType assetType)
        {
            if (assetType == null)
            {
                return;
            }
            var category = assetType.AssetCategory ?? UnitOfWork.Repository<AssetCategory>().GetById(assetType.AssetCategoryId);
            ViewBag.AssetTypeName = assetType.Name;
            ViewBag.AssetCategoryName = category == null ? null : category.Name;
            ViewBag.AssetCategoryId = assetType.AssetCategoryId;
        }
        private static AssetSubTypeEditVm MapEditVm(AssetSubTypeVm subType)
        {
            return new AssetSubTypeEditVm
            {
                Id = subType.Id,
                AssetTypeId = subType.AssetTypeId,
                Name = subType.Name,
                Brand = subType.Brand,
                ItemModel = subType.Model,
                Specifications = subType.Specifications,
                Sku = subType.Sku,
                DefaultAcquisitionCost = subType.DefaultAcquisitionCost,
                PreviousDefaultAcquisitionCost = subType.DefaultAcquisitionCost,
                IsActive = subType.IsActive
            };
        }

        private string ApplySubTypeDefaultCostToAssets(AssetSubTypeEditVm model)
        {
            if (model == null || !model.DefaultAcquisitionCost.HasValue || model.DefaultAcquisitionCost.Value <= 0)
            {
                return null;
            }

            var scope = string.IsNullOrWhiteSpace(model.AcquisitionCostApplyScope)
                ? AcquisitionCostApplyScopes.Individual
                : model.AcquisitionCostApplyScope.Trim();

            if (string.Equals(scope, AcquisitionCostApplyScopes.Individual, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!string.Equals(scope, AcquisitionCostApplyScopes.SameSubType, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var filter = new AssetFilterVm { AssetSubTypeId = model.Id };
            var result = _assetService.ApplyAcquisitionCost(
                0,
                model.DefaultAcquisitionCost.Value,
                AcquisitionCostApplyScopes.SameSubType,
                filter,
                User.GetUserId());

            if (result.ProcessedCount <= 0)
            {
                return result.SkippedCount > 0
                    ? "Default price saved. No asset prices were updated."
                    : null;
            }

            var guidance = "Default price saved and applied to " + result.ProcessedCount + " asset(s).";
            if (result.SkippedCount > 0)
            {
                guidance += " " + result.SkippedCount + " skipped.";
            }

            return guidance;
        }
    }
}

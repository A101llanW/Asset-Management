using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Assets.Edit")]
    public class AssetSubTypesController : BaseController
    {
        private readonly IAssetSubTypeService _assetSubTypeService;
        public AssetSubTypesController()
        {
            _assetSubTypeService = BuildAssetSubTypeService();
        }
        public ActionResult Create(int assetTypeId, string returnUrl = null)
        {
            var assetType = UnitOfWork.Repository<AssetType>().GetById(assetTypeId);
            if (assetType == null)
            {
                return HttpNotFound();
            }
            var model = new AssetSubTypeEditVm
            {
                AssetTypeId = assetTypeId,
                IsActive = true
            };
            PopulateAssetTypeContext(assetType);
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", "AssetTypes", new { id = assetTypeId });
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
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            try
            {
                _assetSubTypeService.Update(model);
                TempData["Message"] = "Asset sub-type updated.";
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
                    stockCount = x.StockCount
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
                stockCount = match.StockCount
            }, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Create")]
        public JsonResult CreateFromAsset(AssetSubTypeCreateFromAssetVm model)
        {
            if (model == null)
            {
                return Json(new { success = false, message = "Sub-type details are required." });
            }
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                model.Name = AssetSubTypeNormalizer.BuildSuggestedName(model.Brand, model.Model);
            }
            try
            {
                var id = _assetSubTypeService.CreateFromAsset(model);
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
                IsActive = subType.IsActive
            };
        }
    }
}

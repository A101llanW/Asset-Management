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
            // Query() (not GetAll()): type, active and search filters run as one SQL WHERE instead of
            // loading every sub-type of the organisation. The exact match below still runs in memory.
            var query = UnitOfWork.Repository<AssetSubType>().Query();
            if (assetTypeId.HasValue && assetTypeId.Value > 0)
            {
                var typeId = assetTypeId.Value;
                query = query.Where(x => x.AssetTypeId == typeId);
            }

            if (activeOnly != false)
            {
                query = query.Where(x => x.IsActive);
            }

            var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            // LIKE wildcards are not escaped by the repository, so such terms are matched in memory only.
            if (term != null && term.IndexOfAny(new[] { '%', '_', '[' }) < 0)
            {
                query = query.Where(x => x.Name.Contains(term) || x.Brand.Contains(term) || x.Model.Contains(term) || x.Sku.Contains(term));
            }

            var loaded = query.ToList().OrderBy(x => x.Name).ToList();
            if (term != null)
            {
                loaded = loaded.Where(x =>
                    (x.Name != null && x.Name.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    || (x.Brand != null && x.Brand.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    || (x.Model != null && x.Model.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    || (x.Sku != null && x.Sku.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            }

            var allTypes = UnitOfWork.Repository<AssetType>().GetAll();
            var types = allTypes.ToDictionary(x => x.Id, x => x);
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
                    IsActive = x.IsActive,
                    DefaultAcquisitionCost = x.DefaultAcquisitionCost
                };
            }).ToList();

            ViewBag.AssetTypeId = assetTypeId;
            ViewBag.Search = search;
            ViewBag.ActiveOnly = activeOnly != false;
            ViewBag.AssetTypes = new SelectList(
                allTypes.Where(x => x.IsActive).OrderBy(x => x.Name).ToList(),
                "Id", "Name", assetTypeId);
            return View(items);
        }

        private const int DetailsPageSize = 25;

        /// <summary>
        /// Sub-type details (Allan, 2026-10-08): the sub-type's active assets one by one, mirroring how Type
        /// Details lists sub-types. Org/department scoped and server-paged (ids paged first, then rows).
        /// Each asset shows its own price; a blank price is "Not set" (never the sub-type price).
        /// </summary>
        public ActionResult Details(int id, int page = 1, string search = null, string returnUrl = null)
        {
            var subType = _assetSubTypeService.GetById(id);
            if (subType == null)
            {
                return HttpNotFound();
            }

            var assetType = UnitOfWork.Repository<AssetType>().GetById(subType.AssetTypeId);
            PopulateAssetTypeContext(assetType);

            var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            var listFilter = new AssetFilterVm { AssetSubTypeId = id, Search = term };
            var assets = _assetService.GetAssetListPage(listFilter, "tag", "asc", page, DetailsPageSize);
            EnrichAssetListCustodianNames(assets.Items);
            var summary = _assetService.GetAssetPriceSummary(new AssetFilterVm { AssetSubTypeId = id });

            var model = new AssetSubTypeDetailsVm
            {
                SubType = subType,
                AssetTypeName = assetType == null ? subType.AssetTypeName : assetType.Name,
                AssetCategoryName = ViewBag.AssetCategoryName as string ?? subType.AssetCategoryName,
                Assets = assets,
                PriceSummary = summary,
                Search = term,
                TotalPages = Math.Max(1, (int)Math.Ceiling((double)assets.TotalCount / Math.Max(1, assets.PageSize))),
                CanEditPrice = true
            };

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            return View(model);
        }

        /// <summary>
        /// Sets or clears one asset's own price from the sub-type details list. Same permission as Asset Edit
        /// (Assets.Edit), tenant + department scoped, audited as Assets.SetPrice. Blank = "Not set"; 0 is allowed.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult SetAssetPrice(int assetId, int subTypeId, string price)
        {
            decimal? parsed;
            if (!AssetPriceRules.TryParse(price, out parsed))
            {
                return Json(new { success = false, message = "Enter a number, or leave it blank for \"Not set\"." });
            }

            var error = AssetPriceRules.Validate(parsed);
            if (error != null)
            {
                return Json(new { success = false, message = error });
            }

            var asset = UnitOfWork.Repository<Asset>().GetById(assetId);
            if (asset == null || !asset.IsActive || asset.AssetSubTypeId != subTypeId)
            {
                return Json(new { success = false, message = "Asset not found for this sub-type." });
            }

            try
            {
                _assetService.SetAcquisitionCost(assetId, parsed, User.GetUserId());
            }
            catch (BusinessException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }

            var summary = _assetService.GetAssetPriceSummary(new AssetFilterVm { AssetSubTypeId = subTypeId });
            return Json(new
            {
                success = true,
                assetId = assetId,
                price = parsed.HasValue ? parsed.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : string.Empty,
                priceDisplay = AssetPriceRules.Format(parsed),
                isSet = parsed.HasValue,
                pricedCount = summary.PricedCount,
                unpricedCount = summary.UnpricedCount,
                pricedTotalDisplay = CurrencyFormatter.Format(summary.PricedTotal),
                unpricedNote = AssetPriceRules.FormatUnpricedNote(summary.UnpricedCount)
            });
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
                // The sub-type price is a reference value only: it is never copied into assets
                // (Allan, 2026-10-08). The old "apply default price to all assets" step was removed.
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
    }
}

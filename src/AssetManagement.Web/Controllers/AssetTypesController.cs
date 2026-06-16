using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Web.Filters;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Assets.Edit")]
    public class AssetTypesController : BaseController
    {
        public ActionResult Index(string search = null, int? categoryId = null, string sort = "name", string direction = "asc", int page = 1, int pageSize = 10)
        {
            var items = UnitOfWork.Repository<AssetType>().GetAll();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                items = items.Where(x => (x.Name ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.Description ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.AssetCategory.Name ?? string.Empty).ToLowerInvariant().Contains(term));
            }

            if (categoryId.HasValue)
            {
                items = items.Where(x => x.AssetCategoryId == categoryId.Value);
            }

            switch ((sort ?? string.Empty).ToLowerInvariant())
            {
                case "category":
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase)
                        ? items.OrderByDescending(x => x.AssetCategory.Name)
                        : items.OrderBy(x => x.AssetCategory.Name);
                    break;
                case "status":
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase)
                        ? items.OrderByDescending(x => x.IsActive)
                        : items.OrderBy(x => x.IsActive);
                    break;
                default:
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase)
                        ? items.OrderByDescending(x => x.Name)
                        : items.OrderBy(x => x.Name);
                    sort = "name";
                    break;
            }

            var models = items.ToList().Select(x => new AssetTypeVm
            {
                Id = x.Id,
                AssetCategoryId = x.AssetCategoryId,
                Name = x.Name,
                Description = x.Description,
                IsActive = x.IsActive,
                UseCustomUsefulLife = x.UsefulLifeMonths.HasValue,
                UsefulLifeMonths = x.UsefulLifeMonths
            });

            ViewBag.Categories = BuildCategorySelectList(categoryId);
            ViewBag.CategoryLookup = UnitOfWork.Repository<AssetCategory>().GetAll().ToDictionary(x => x.Id, x => x.Name);
            ViewBag.Sort = sort;
            ViewBag.Direction = direction;
            return View(BuildListPage(models, search, sort, direction, page, pageSize));
        }

        public ActionResult Details(int id, string returnUrl = null)
        {
            var entity = UnitOfWork.Repository<AssetType>().GetById(id);
            if (entity == null)
            {
                return HttpNotFound();
            }

            var model = MapAssetTypeVm(entity);

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.CategoryName = entity.AssetCategory?.Name;
            ViewBag.CanManageUsefulLife = IsCurrentUserCompanyAdmin();
            ViewBag.CategoryDefaultUsefulLifeMonths = entity.AssetCategory?.DefaultUsefulLifeMonths;
            ViewBag.AssetCount = BuildAssetService().CountAssets(new AssetFilterVm { AssetTypeId = id });
            return View(model);
        }

        public ActionResult Create(int? categoryId = null, string returnUrl = null)
        {
            var model = new AssetTypeVm
            {
                AssetCategoryId = categoryId ?? 0,
                IsActive = true
            };
            PopulateCategories(model.AssetCategoryId);
            PopulateUsefulLifeContext(model.AssetCategoryId);
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(AssetTypeVm model, string returnUrl = null)
        {
            PopulateCategories(model.AssetCategoryId);
            PopulateUsefulLifeContext(model.AssetCategoryId);
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ValidateAssetType(model);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var entity = new AssetType
            {
                AssetCategoryId = model.AssetCategoryId,
                Name = model.Name.Trim(),
                Description = model.Description,
                IsActive = model.IsActive
            };
            ApplyAssetTypeUsefulLife(entity, model);

            UnitOfWork.Repository<AssetType>().Add(entity);
            UnitOfWork.SaveChanges();
            TempData["Message"] = "Asset type created.";
            TempData["Guidance"] = "Next step: use this type when creating or editing assets in the selected category.";
            return RedirectToAction("Details", new { id = entity.Id, returnUrl = ViewBag.ReturnUrl });
        }

        public ActionResult Edit(int id, string returnUrl = null)
        {
            var entity = UnitOfWork.Repository<AssetType>().GetById(id);
            if (entity == null)
            {
                return HttpNotFound();
            }

            PopulateCategories(entity.AssetCategoryId);
            PopulateUsefulLifeContext(entity.AssetCategoryId);
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", null, new { id });
            return View(MapAssetTypeVm(entity));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(AssetTypeVm model, string returnUrl = null)
        {
            PopulateCategories(model.AssetCategoryId);
            PopulateUsefulLifeContext(model.AssetCategoryId);
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", null, new { id = model.Id });
            ValidateAssetType(model);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var entity = UnitOfWork.Repository<AssetType>().GetById(model.Id);
            if (entity == null)
            {
                return HttpNotFound();
            }

            entity.AssetCategoryId = model.AssetCategoryId;
            entity.Name = model.Name.Trim();
            entity.Description = model.Description;
            entity.IsActive = model.IsActive;
            ApplyAssetTypeUsefulLife(entity, model);
            UnitOfWork.Repository<AssetType>().Update(entity);
            UnitOfWork.SaveChanges();
            TempData["Message"] = "Asset type updated.";
            return RedirectToReturnUrl(returnUrl, "Details", null, new { id = model.Id });
        }

        private void PopulateCategories(int? selectedCategoryId = null)
        {
            ViewBag.Categories = BuildCategorySelectList(selectedCategoryId);
        }

        private SelectList BuildCategorySelectList(int? selectedCategoryId = null)
        {
            var categories = UnitOfWork.Repository<AssetCategory>().GetAll()
                .OrderBy(x => x.Name)
                .Select(x => new { x.Id, x.Name })
                .ToList();
            return new SelectList(categories, "Id", "Name", selectedCategoryId);
        }

        private void ValidateAssetType(AssetTypeVm model)
        {
            if (model == null)
            {
                return;
            }

            if (model.AssetCategoryId <= 0)
            {
                ModelState.AddModelError(nameof(model.AssetCategoryId), "Please select a category.");
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                return;
            }

            var name = model.Name.Trim();
            var exists = UnitOfWork.Repository<AssetType>().GetAll()
                .Any(x => x.Id != model.Id
                    && x.AssetCategoryId == model.AssetCategoryId
                    && x.Name.ToLower() == name.ToLower());
            if (exists)
            {
                ModelState.AddModelError(nameof(model.Name), "This category already has an asset type with the same name.");
            }
        }

        private static AssetTypeVm MapAssetTypeVm(AssetType entity)
        {
            return new AssetTypeVm
            {
                Id = entity.Id,
                AssetCategoryId = entity.AssetCategoryId,
                Name = entity.Name,
                Description = entity.Description,
                IsActive = entity.IsActive,
                UseCustomUsefulLife = entity.UsefulLifeMonths.HasValue,
                UsefulLifeMonths = entity.UsefulLifeMonths
            };
        }

        private void PopulateUsefulLifeContext(int categoryId)
        {
            ViewBag.CanManageUsefulLife = IsCurrentUserCompanyAdmin();
            if (categoryId <= 0)
            {
                ViewBag.CategoryDefaultUsefulLifeMonths = null;
                return;
            }

            var category = UnitOfWork.Repository<AssetCategory>().GetById(categoryId);
            ViewBag.CategoryDefaultUsefulLifeMonths = category == null ? null : category.DefaultUsefulLifeMonths;
        }

        private void ApplyAssetTypeUsefulLife(AssetType entity, AssetTypeVm model)
        {
            if (!IsCurrentUserCompanyAdmin() || entity == null || model == null)
            {
                return;
            }

            if (model.UseCustomUsefulLife && model.UsefulLifeMonths.HasValue && model.UsefulLifeMonths.Value > 0)
            {
                entity.UsefulLifeMonths = model.UsefulLifeMonths;
                return;
            }

            entity.UsefulLifeMonths = null;
        }
    }
}

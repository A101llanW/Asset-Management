using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Web.Filters;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Assets.Edit")]
    public class AssetCategoriesController : BaseController
    {
        public ActionResult Index(string search = null, string sort = "name", string direction = "asc", int page = 1, int pageSize = 10)
        {
            var items = UnitOfWork.Repository<AssetCategory>().GetAll();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                items = items.Where(x => (x.Name ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.Description ?? string.Empty).ToLowerInvariant().Contains(term));
            }

            switch ((sort ?? string.Empty).ToLowerInvariant())
            {
                case "status":
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase)
                        ? items.OrderByDescending(x => x.IsActive)
                        : items.OrderBy(x => x.IsActive);
                    break;
                case "types":
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase)
                        ? items.OrderByDescending(x => x.AssetTypes.Count)
                        : items.OrderBy(x => x.AssetTypes.Count);
                    break;
                default:
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase)
                        ? items.OrderByDescending(x => x.Name)
                        : items.OrderBy(x => x.Name);
                    sort = "name";
                    break;
            }

            var models = items.ToList().Select(x => new AssetCategoryVm
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                IsActive = x.IsActive,
                DefaultUsefulLifeMonths = x.DefaultUsefulLifeMonths
            });

            ViewBag.TypeLookup = UnitOfWork.Repository<AssetType>().GetAll()
                .GroupBy(x => x.AssetCategoryId)
                .ToDictionary(x => x.Key, x => x.Count());
            ViewBag.Sort = sort;
            ViewBag.Direction = direction;
            return View(BuildListPage(models, search, sort, direction, page, pageSize));
        }

        public ActionResult Details(int id, string returnUrl = null)
        {
            var entity = UnitOfWork.Repository<AssetCategory>().GetById(id);
            if (entity == null)
            {
                return HttpNotFound();
            }

            var model = new AssetCategoryVm
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                IsActive = entity.IsActive,
                DefaultUsefulLifeMonths = entity.DefaultUsefulLifeMonths
            };

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.CanManageUsefulLife = IsCurrentUserCompanyAdmin();
            ViewBag.AssetTypeCount = UnitOfWork.Repository<AssetType>().Find(x => x.AssetCategoryId == id).Count();
            ViewBag.AssetCount = BuildAssetService().CountAssets(new AssetFilterVm { CategoryId = id });
            ViewBag.AssetTypes = UnitOfWork.Repository<AssetType>().Find(x => x.AssetCategoryId == id)
                .OrderBy(x => x.Name)
                .ToList();
            return View(model);
        }

        public ActionResult Create(string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.CanManageUsefulLife = IsCurrentUserCompanyAdmin();
            return View(new AssetCategoryVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(AssetCategoryVm model, string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.CanManageUsefulLife = IsCurrentUserCompanyAdmin();
            ValidateCategory(model);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var entity = new AssetCategory
            {
                Name = model.Name.Trim(),
                Description = model.Description,
                IsActive = model.IsActive
            };
            ApplyCategoryUsefulLife(entity, model);

            UnitOfWork.Repository<AssetCategory>().Add(entity);
            UnitOfWork.SaveChanges();
            TempData["Message"] = "Asset category created.";
            TempData["Guidance"] = "Next step: add one or more asset types under this category so assets can be classified correctly.";
            return RedirectToAction("Details", new { id = entity.Id, returnUrl = ViewBag.ReturnUrl });
        }

        public ActionResult Edit(int id, string returnUrl = null)
        {
            var entity = UnitOfWork.Repository<AssetCategory>().GetById(id);
            if (entity == null)
            {
                return HttpNotFound();
            }

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", null, new { id });
            ViewBag.CanManageUsefulLife = IsCurrentUserCompanyAdmin();
            return View(new AssetCategoryVm
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                IsActive = entity.IsActive,
                DefaultUsefulLifeMonths = entity.DefaultUsefulLifeMonths
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(AssetCategoryVm model, string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", null, new { id = model.Id });
            ViewBag.CanManageUsefulLife = IsCurrentUserCompanyAdmin();
            ValidateCategory(model);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var entity = UnitOfWork.Repository<AssetCategory>().GetById(model.Id);
            if (entity == null)
            {
                return HttpNotFound();
            }

            entity.Name = model.Name.Trim();
            entity.Description = model.Description;
            entity.IsActive = model.IsActive;
            ApplyCategoryUsefulLife(entity, model);
            UnitOfWork.Repository<AssetCategory>().Update(entity);
            UnitOfWork.SaveChanges();
            TempData["Message"] = "Asset category updated.";
            return RedirectToReturnUrl(returnUrl, "Details", null, new { id = model.Id });
        }

        private void ValidateCategory(AssetCategoryVm model)
        {
            if (string.IsNullOrWhiteSpace(model?.Name))
            {
                return;
            }

            var name = model.Name.Trim();
            var exists = UnitOfWork.Repository<AssetCategory>().GetAll()
                .Any(x => x.Id != model.Id && x.Name.ToLower() == name.ToLower());
            if (exists)
            {
                ModelState.AddModelError(nameof(model.Name), "An asset category with this name already exists.");
            }
        }

        private void ApplyCategoryUsefulLife(AssetCategory entity, AssetCategoryVm model)
        {
            if (!IsCurrentUserCompanyAdmin() || entity == null || model == null)
            {
                return;
            }

            entity.DefaultUsefulLifeMonths = model.DefaultUsefulLifeMonths.HasValue && model.DefaultUsefulLifeMonths.Value > 0
                ? model.DefaultUsefulLifeMonths
                : null;
        }
    }
}

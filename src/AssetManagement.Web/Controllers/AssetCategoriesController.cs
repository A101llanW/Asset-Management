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
            var orgId = ResolveCurrentOrganizationId();
            var catalogQuery = BuildCatalogQueryRepository();
            var pageResult = orgId.HasValue && catalogQuery != null
                ? catalogQuery.GetAssetCategoryListPage(orgId.Value, search, sort, direction, page, pageSize)
                : new PagedListVm<AssetCategoryListVm>();

            ViewBag.TypeLookup = pageResult.Items.ToDictionary(x => x.Id, x => x.TypeCount);
            ViewBag.Sort = sort;
            ViewBag.Direction = direction;
            var models = pageResult.Items.Select(x => new AssetCategoryVm
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                IsActive = x.IsActive,
                DefaultUsefulLifeMonths = x.DefaultUsefulLifeMonths,
                DefaultDepreciationLifeMonths = x.DefaultDepreciationLifeMonths,
                DefaultDepreciationRatePercent = x.DefaultDepreciationRatePercent
            });
            return View(ToListPage(new PagedListVm<AssetCategoryVm>
            {
                Items = models.ToList(),
                TotalCount = pageResult.TotalCount,
                Search = pageResult.Search,
                Sort = pageResult.Sort,
                Direction = pageResult.Direction,
                Page = pageResult.Page,
                PageSize = pageResult.PageSize
            }));
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
                DefaultUsefulLifeMonths = entity.DefaultUsefulLifeMonths,
                DefaultDepreciationLifeMonths = entity.DefaultDepreciationLifeMonths,
                DefaultDepreciationRatePercent = entity.DefaultDepreciationRatePercent
            };

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.CanManageUsefulLife = IsCurrentUserCompanyAdmin();
            ViewBag.CanManageDepreciation = IsCurrentUserCompanyAdmin();
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
            ViewBag.CanManageDepreciation = IsCurrentUserCompanyAdmin();
            return View(new AssetCategoryVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(AssetCategoryVm model, string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.CanManageUsefulLife = IsCurrentUserCompanyAdmin();
            ViewBag.CanManageDepreciation = IsCurrentUserCompanyAdmin();
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
            ApplyCategoryDepreciation(entity, model);

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
                DefaultUsefulLifeMonths = entity.DefaultUsefulLifeMonths,
                DefaultDepreciationLifeMonths = entity.DefaultDepreciationLifeMonths,
                DefaultDepreciationRatePercent = entity.DefaultDepreciationRatePercent
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(AssetCategoryVm model, string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", null, new { id = model.Id });
            ViewBag.CanManageUsefulLife = IsCurrentUserCompanyAdmin();
            ViewBag.CanManageDepreciation = IsCurrentUserCompanyAdmin();
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
            ApplyCategoryDepreciation(entity, model);
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

        private void ApplyCategoryDepreciation(AssetCategory entity, AssetCategoryVm model)
        {
            if (!IsCurrentUserCompanyAdmin() || entity == null || model == null)
            {
                return;
            }

            entity.DefaultDepreciationLifeMonths = model.DefaultDepreciationLifeMonths.HasValue && model.DefaultDepreciationLifeMonths.Value > 0
                ? model.DefaultDepreciationLifeMonths
                : null;

            entity.DefaultDepreciationRatePercent = model.DefaultDepreciationRatePercent.HasValue && model.DefaultDepreciationRatePercent.Value > 0
                ? model.DefaultDepreciationRatePercent
                : null;
        }
    }
}

using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Assets.View")]
    public class SearchController : BaseController
    {
        private readonly ISearchService _searchService;

        public SearchController()
        {
            _searchService = DependencyResolver.Current.GetService<ISearchService>();
        }

        public ActionResult Index(string q)
        {
            var model = _searchService.Search(q, 50);
            return View(model);
        }
    }
}

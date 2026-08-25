using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Security;
using AssetManagement.Domain.Entities;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Controllers
{
    [AnyPermissionAuthorize("Users.Invite")]
    [ModuleAccess(ModulePermissionCatalog.Users)]
    public class UserInvitationsController : BaseController
    {
        private readonly IUserInvitationService _invitationService;
        private readonly IRoleService _roleService;
        private readonly IOrganizationScopeService _organizationScope;

        public UserInvitationsController()
        {
            _invitationService = DependencyResolver.Current.GetService<IUserInvitationService>();
            _roleService = BuildRoleService();
            _organizationScope = DependencyResolver.Current.GetService<IOrganizationScopeService>();
        }

        public ActionResult Index(int page = 1, int pageSize = 10)
        {
            var organizationId = GetCurrentOrganizationIdOrDeny();
            if (!organizationId.HasValue)
            {
                return new HttpStatusCodeResult(403);
            }

            var pageResult = _invitationService.GetListPage(organizationId.Value, page, pageSize);
            return View(ToListPage(pageResult));
        }

        public ActionResult Create(string returnUrl = null)
        {
            var organizationId = GetCurrentOrganizationIdOrDeny();
            if (!organizationId.HasValue)
            {
                return new HttpStatusCodeResult(403);
            }

            ConfigureCreateViewBag(returnUrl, organizationId.Value);
            return View(new UserInvitationCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(UserInvitationCreateViewModel model, string returnUrl = null)
        {
            var organizationId = GetCurrentOrganizationIdOrDeny();
            if (!organizationId.HasValue)
            {
                return new HttpStatusCodeResult(403);
            }

            ConfigureCreateViewBag(returnUrl, organizationId.Value);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var organization = ResolveOrganization(organizationId.Value);
            if (organization == null)
            {
                return HttpNotFound();
            }

            var request = new UserInvitationCreateRequest
            {
                OrganizationId = organizationId.Value,
                InvitedByUserId = User.GetUserId(),
                Email = model.Email,
                RoleId = model.RoleId,
                DepartmentId = model.DepartmentId,
                OrganizationSlug = organization.Slug,
                OrganizationName = organization.Name
            };

            var result = _invitationService.CreateInvitation(request);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors ?? new string[0])
                {
                    ModelState.AddModelError("", error);
                }

                return View(model);
            }

            TempData["Message"] = "Invitation sent to " + model.Email + ".";
            TempData["InviteLink"] = result.InviteLink;
            return RedirectToAction("Index");
        }

        private void ConfigureCreateViewBag(string returnUrl, int organizationId)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.Roles = new SelectList(_roleService.GetRoles(), "Id", "Name");
            ViewBag.Departments = new SelectList(BuildDepartmentService().GetAll(), "Id", "Name");
        }

        private int? GetCurrentOrganizationIdOrDeny()
        {
            return _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId();
        }

        private Organization ResolveOrganization(int organizationId)
        {
            return UnitOfWork.Repository<Organization>().GetAll()
                .FirstOrDefault(x => x.Id == organizationId);
        }
    }
}

using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Web.Filters;
using AssetManagement.Web.ViewModels;
using AssetManagement.Infrastructure.Identity;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Users.View")]
    public class UsersController : BaseController
    {
        private readonly IUserService _userService;
        private readonly IRoleService _roleService;

        public UsersController()
        {
            _userService = BuildUserService();
            _roleService = BuildRoleService();
        }

        public ActionResult Index()
        {
            ViewBag.Roles = _roleService.GetRoles();
            return View(_userService.GetAll());
        }

        [PermissionAuthorize("Users.Create")]
        public ActionResult Create()
        {
            ViewBag.Roles = _roleService.GetRoles();
            ViewBag.Departments = BuildDepartmentService().GetAll();
            return View(new UserCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Users.Create")]
        public ActionResult Create(UserCreateViewModel model)
        {
            ViewBag.Roles = _roleService.GetRoles();
            ViewBag.Departments = BuildDepartmentService().GetAll();
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var manager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(DbContext));
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                EmployeeNumber = model.EmployeeNumber,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Phone = model.Phone,
                DepartmentId = model.DepartmentId,
                PositionTitle = model.PositionTitle,
                RoleId = model.RoleId,
                IsActive = true,
                EmailConfirmed = true
            };

            var result = manager.Create(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error);
                }

                return View(model);
            }

            TempData["Message"] = "User created successfully.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Users.Edit")]
        public ActionResult AssignRole(string userId, int roleId)
        {
            _userService.AssignRole(userId, roleId);
            TempData["Message"] = "User role updated.";
            return RedirectToAction("Index");
        }
    }
}

using System;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Assets.Assign")]
    public class AssignmentsController : BaseController
    {
        private readonly IAssignmentService _assignmentService;
        private readonly IUserService _userService;
        private readonly IDepartmentService _departmentService;

        public AssignmentsController()
        {
            _assignmentService = BuildAssignmentService();
            _userService = BuildUserService();
            _departmentService = BuildDepartmentService();
        }

        public ActionResult Index(int assetId)
        {
            ViewBag.AssetId = assetId;
            var users = _userService.GetAll()
                .ToDictionary(x => x.Id, x => BuildUserLabel(x));
            var departments = _departmentService.GetAll()
                .ToDictionary(x => x.Id, x => x.Name);

            var assignments = _assignmentService.GetByAsset(assetId)
                .Select(x =>
                {
                    x.ToUserName = !string.IsNullOrWhiteSpace(x.ToUserId) && users.ContainsKey(x.ToUserId)
                        ? users[x.ToUserId]
                        : x.ToUserName;
                    x.ToDepartmentName = x.ToDepartmentId.HasValue && departments.ContainsKey(x.ToDepartmentId.Value)
                        ? departments[x.ToDepartmentId.Value]
                        : x.ToDepartmentName;
                    return x;
                })
                .ToList();

            return View(assignments);
        }

        public ActionResult Create(int assetId)
        {
            var model = new AssetAssignmentVm
            {
                AssetId = assetId,
                AssignedDate = DateTime.UtcNow,
                AssignmentType = AssignmentType.Permanent.ToString(),
                HandedOverById = CurrentUserContext.UserId
            };

            PopulateLookups(model);
            ViewBag.AssetContext = BuildAssetWorkflowContext(assetId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(AssetAssignmentVm model)
        {
            PopulateLookups(model);
            ViewBag.AssetContext = BuildAssetWorkflowContext(model.AssetId);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                _assignmentService.Assign(model);
                TempData["Message"] = "Asset assigned successfully.";
                return RedirectToAction("Details", "Assets", new { id = model.AssetId });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        private void PopulateLookups(AssetAssignmentVm model)
        {
            var users = _userService.GetAll()
                .Where(x => x.IsActive)
                .Select(x => new
                {
                    x.Id,
                    Name = BuildUserLabel(x)
                })
                .OrderBy(x => x.Name)
                .ToList();
            ViewBag.Users = new SelectList(users, "Id", "Name", model?.ToUserId);

            var departments = _departmentService.GetAll()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ToList();
            ViewBag.Departments = new SelectList(departments, "Id", "Name", model?.ToDepartmentId);

            var selectedType = string.IsNullOrWhiteSpace(model?.AssignmentType)
                ? AssignmentType.Permanent.ToString()
                : model.AssignmentType;
            var assignmentTypes = Enum.GetNames(typeof(AssignmentType))
                .Select(x => new { Value = x, Text = x })
                .ToList();
            ViewBag.AssignmentTypes = new SelectList(assignmentTypes, "Value", "Text", selectedType);
        }

        private static string BuildUserLabel(UserVm user)
        {
            var name = (user.FirstName + " " + user.LastName).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return user.Email ?? user.Id;
            }

            return string.IsNullOrWhiteSpace(user.Email) ? name : name + " (" + user.Email + ")";
        }
    }
}

using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Domain.Entities;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Reports.View")]
    public class NotificationsController : BaseController
    {
        private readonly INotificationService _notificationService;

        public NotificationsController()
        {
            _notificationService = BuildNotificationService();
        }

        public ActionResult Index()
        {
            var notifications = UnitOfWork.Repository<Notification>().GetAll()
                .OrderByDescending(x => x.CreatedAt)
                .Take(100)
                .ToList();
            return View(notifications);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Generate()
        {
            _notificationService.GenerateSystemNotifications();
            TempData["Message"] = "Notification generation executed.";
            return RedirectToAction("Index");
        }
    }
}

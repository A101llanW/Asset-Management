using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Security;

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

        public ActionResult Index(bool unreadOnly = false)
        {
            var userId = User.GetUserId();
            ViewBag.UnreadOnly = unreadOnly;
            return View(_notificationService.GetInboxForUser(userId, unreadOnly, 100));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkRead(int id)
        {
            _notificationService.MarkAsRead(id, User.GetUserId());
            return RedirectToAction("Index");
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

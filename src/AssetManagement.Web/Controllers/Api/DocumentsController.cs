using System.Web.Mvc;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers.Api
{
    [Authorize]
    public class DocumentsController : Controller
    {
        [HttpPost]
        [PermissionAuthorize("Documents.Upload")]
        [ValidateAntiForgeryToken]
        public ActionResult Upload(int assetId)
        {
            if (Request.Files.Count == 0)
            {
                return Json(new { success = false, message = "No files uploaded." });
            }

            // Attachment service hook point. Wire to FileSystemStorageProvider + AssetDocuments in production flow.
            return Json(new { success = true, message = "Upload endpoint scaffold is ready.", fileCount = Request.Files.Count, assetId });
        }
    }
}

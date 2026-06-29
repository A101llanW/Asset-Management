using System.IO;
using System.Web;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Controllers
{
    [Authorize]
    public class DocumentsController : BaseController
    {
        private readonly IAssetDocumentService _documentService;
        private readonly IFileStorageProvider _storage;
        private readonly IAssetService _assetService;

        public DocumentsController()
        {
            _documentService = BuildAssetDocumentService();
            _storage = DependencyResolver.Current.GetService<IFileStorageProvider>();
            _assetService = BuildAssetService();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Upload(int assetId, string documentType, HttpPostedFileBase attachment)
        {
            if (_assetService.GetById(assetId) == null)
            {
                TempData["Error"] = "Asset not found.";
                return RedirectToAction("Index", "Assets");
            }

            documentType = AssetDocumentTypeCatalog.NormalizeType(documentType);
            if (string.IsNullOrWhiteSpace(documentType))
            {
                TempData["Error"] = "Select or enter a document type before uploading.";
                return Redirect(Url.Action("Details", "Assets", new { id = assetId }) + "#documents");
            }

            if (attachment == null || attachment.ContentLength == 0)
            {
                TempData["Error"] = "Select a file to upload.";
                return Redirect(Url.Action("Details", "Assets", new { id = assetId }) + "#documents");
            }

            try
            {
                using (var stream = attachment.InputStream)
                {
                    _documentService.Upload(
                        assetId,
                        documentType,
                        attachment.FileName,
                        attachment.ContentType,
                        stream,
                        User.GetUserId());
                }

                TempData["Message"] = "Document uploaded successfully.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return Redirect(Url.Action("Details", "Assets", new { id = assetId }) + "#documents");
        }

        public ActionResult Download(int id)
        {
            var userId = User.GetUserId();
            var document = _documentService.GetById(id);
            if (document == null)
            {
                return HttpNotFound();
            }

            if (_assetService.GetById(document.AssetId) == null)
            {
                return HttpNotFound();
            }

            try
            {
                var relativePath = _documentService.GetStoredRelativePath(id, userId);
                var physicalPath = _storage.GetFullPath(relativePath);
                if (!System.IO.File.Exists(physicalPath))
                {
                    return HttpNotFound();
                }

                return File(physicalPath, document.ContentType ?? "application/octet-stream", document.FileName);
            }
            catch (BusinessException)
            {
                return new HttpStatusCodeResult(403, "You do not have permission to download this document.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Documents.Delete")]
        public ActionResult Delete(int id, int assetId)
        {
            var document = _documentService.GetById(id);
            if (document == null || document.AssetId != assetId)
            {
                TempData["Error"] = "Document not found.";
                return Redirect(Url.Action("Details", "Assets", new { id = assetId }) + "#documents");
            }

            if (_assetService.GetById(assetId) == null)
            {
                TempData["Error"] = "Asset not found.";
                return RedirectToAction("Index", "Assets");
            }

            try
            {
                _documentService.Delete(id, User.GetUserId());
                TempData["Message"] = "Document deleted.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return Redirect(Url.Action("Details", "Assets", new { id = assetId }) + "#documents");
        }
    }
}

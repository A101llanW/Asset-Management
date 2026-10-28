using System;
using System.Globalization;
using System.IO;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Helpers;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Controllers
{
    /// <summary>
    /// Hidden how-to video page (Allan, 2026-10-28: "reactivate the hidden help page for uploading the video. Make it
    /// accessible while hiding that redirect 'help' button"). Reachable only by direct URL: /{tenant}/HelpVideos.
    /// Every signed-in tenant user can watch ([TenantAuthorize] on BaseController); upload, replace and delete need
    /// Settings.Manage. Files: ~/App_Data/HelpVideos; list: table HelpVideo (migration 109).
    /// Uploads are chunked (4 MB) because IIS caps a request at 30,000,000 bytes and httpRuntime at 50 MB.
    /// A topic can instead point at an unlisted YouTube video (Allan, 2026-10-28: "On yt, only users with the link should
    /// be able to view"): only the id is stored and it is embedded from youtube-nocookie.com with rel=0. The id only ever
    /// appears in this page's HTML, which (like every action here) is behind sign-in: anonymous requests get a 302 to login.
    /// </summary>
    public class HelpVideosController : BaseController
    {
        public const string VideoRootVirtualPath = "~/App_Data/HelpVideos";

        private readonly IHelpVideoStore _store;

        public HelpVideosController()
            : this(DependencyResolver.Current.GetService<IHelpVideoStore>())
        {
        }

        public HelpVideosController(IHelpVideoStore store)
        {
            _store = store;
        }

        [HttpGet]
        public ActionResult Index()
        {
            var organizationId = ResolveCurrentOrganizationId();
            if (!organizationId.HasValue || _store == null)
            {
                return NotFoundPage();
            }

            ViewBag.Title = "How-to videos";
            var model = HelpVideoCatalog.Build(_store.GetVisible(organizationId.Value), HasPermission(HelpVideoRules.ManagePermission));
            return View(model);
        }

        /// <summary>Video bytes with HTTP Range support. Same viewers as the page; only the tenant's own or all-tenant rows.</summary>
        [AcceptVerbs(HttpVerbs.Get | HttpVerbs.Head)]
        public ActionResult Stream(int id)
        {
            var organizationId = ResolveCurrentOrganizationId();
            if (!organizationId.HasValue || _store == null)
            {
                return HttpNotFound();
            }

            var video = _store.GetVisibleById(organizationId.Value, id);
            var path = video == null || video.IsYouTube ? null : Files().GetExistingVideoPath(video.OrganizationId, video.StoredFileName);
            if (path == null)
            {
                return HttpNotFound();
            }

            return new HelpVideoStreamResult(path, video.ContentType, System.IO.File.GetLastWriteTimeUtc(path));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(HelpVideoRules.ManagePermission)]
        public ActionResult UploadStart(string topicSlug, string title, string fileName, long? totalBytes)
        {
            var organizationId = ResolveCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return JsonError("No organization is selected.");
            }

            var slug = HelpVideoRules.NormalizeTopicSlug(topicSlug);
            if (slug == null)
            {
                return JsonError("Choose a topic for the video.");
            }

            var cleanName = HelpVideoRules.SanitizeFileName(fileName);
            var extension = HelpVideoRules.GetAllowedExtension(cleanName);
            if (extension == null)
            {
                return JsonError("Only .mp4 and .webm videos can be uploaded.");
            }

            var displayTitle = HelpVideoRules.NormalizeTitle(title, Path.GetFileNameWithoutExtension(cleanName));
            if (displayTitle == null)
            {
                return JsonError("Enter a title for the video.");
            }

            try
            {
                var session = Files().StartUpload(new HelpVideoUploadSession
                {
                    OrganizationId = organizationId.Value,
                    UserId = User.GetUserId(),
                    TopicSlug = slug,
                    Title = displayTitle,
                    OriginalFileName = cleanName,
                    Extension = extension,
                    TotalBytes = totalBytes ?? 0
                });
                return Json(new { ok = true, uploadId = session.UploadId, chunkSize = HelpVideoRules.ChunkSizeBytes });
            }
            catch (HelpVideoUploadException ex)
            {
                return JsonError(ex.Message);
            }
        }

        /// <summary>One 4 MB piece (form field "chunk") at byte "offset". Not written to the HTTP audit log (AuditLogAttribute).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(HelpVideoRules.ManagePermission)]
        public ActionResult UploadChunk(string uploadId, long? offset)
        {
            var session = LoadOwnSession(uploadId);
            if (session == null)
            {
                return JsonError("The upload was not found. Start it again.");
            }

            var chunk = Request.Files["chunk"];
            if (chunk == null || chunk.ContentLength <= 0)
            {
                return JsonError("The upload chunk is empty.");
            }

            try
            {
                var received = Files().AppendChunk(session, offset ?? -1, chunk.InputStream, chunk.ContentLength);
                return Json(new { ok = true, received = received });
            }
            catch (HelpVideoUploadException ex)
            {
                return JsonError(ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(HelpVideoRules.ManagePermission)]
        public ActionResult UploadComplete(string uploadId)
        {
            var session = LoadOwnSession(uploadId);
            if (session == null)
            {
                return JsonError("The upload was not found. Start it again.");
            }

            var files = Files();
            string stored;
            try
            {
                stored = files.CompleteUpload(session);
            }
            catch (HelpVideoUploadException ex)
            {
                return JsonError(ex.Message);
            }

            HelpVideoSaveResult saved;
            try
            {
                saved = _store.Save(new HelpVideoSave
                {
                    OrganizationId = session.OrganizationId,
                    TopicSlug = session.TopicSlug,
                    Title = session.Title,
                    OriginalFileName = session.OriginalFileName,
                    StoredFileName = stored,
                    ContentType = HelpVideoRules.ContentTypeForExtension(session.Extension),
                    SizeBytes = session.TotalBytes,
                    UploadedById = session.UserId,
                    UploadedAtUtc = DateTime.UtcNow
                });
            }
            catch
            {
                files.DeleteVideo(session.OrganizationId, stored);
                throw;
            }

            if (saved.Replaced && !string.IsNullOrEmpty(saved.PreviousStoredFileName)
                && !string.Equals(saved.PreviousStoredFileName, stored, StringComparison.OrdinalIgnoreCase))
            {
                files.DeleteVideo(session.OrganizationId, saved.PreviousStoredFileName);
            }

            AuditWriter.Write(
                saved.Replaced ? HelpVideoRules.AuditReplace : HelpVideoRules.AuditUpload,
                HelpVideoRules.AuditEntityType,
                saved.Id.ToString(CultureInfo.InvariantCulture),
                saved.Replaced ? "StoredFileName=" + saved.PreviousStoredFileName : null,
                "Topic=" + session.TopicSlug + "; Title=" + session.Title + "; File=" + session.OriginalFileName
                    + "; Size=" + session.TotalBytes.ToString(CultureInfo.InvariantCulture));

            return Json(new { ok = true, id = saved.Id, replaced = saved.Replaced });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(HelpVideoRules.ManagePermission)]
        public ActionResult UploadCancel(string uploadId)
        {
            if (LoadOwnSession(uploadId) != null)
            {
                Files().AbortUpload(uploadId);
            }

            return Json(new { ok = true });
        }

        /// <summary>Points a topic at an unlisted YouTube video (link or id). Replaces the tenant's video for that topic.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(HelpVideoRules.ManagePermission)]
        public ActionResult SetYouTube(string topicSlug, string title, string youTubeUrl)
        {
            var organizationId = ResolveCurrentOrganizationId();
            if (!organizationId.HasValue || _store == null)
            {
                return NotFoundPage();
            }

            var slug = HelpVideoRules.NormalizeTopicSlug(topicSlug);
            var videoId = HelpVideoRules.ParseYouTubeId(youTubeUrl);
            if (slug == null || videoId == null)
            {
                TempData["Error"] = slug == null
                    ? "Choose a topic for the video."
                    : "That is not a YouTube video link. Paste the video's Share link (https://youtu.be/...) or its 11-character id.";
                return RedirectToTenantAware("HelpVideos", "Index");
            }

            var displayTitle = HelpVideoRules.NormalizeTitle(title, TopicTitle(slug) ?? "How-to video");
            var saved = _store.Save(new HelpVideoSave
            {
                OrganizationId = organizationId.Value,
                TopicSlug = slug,
                Title = displayTitle,
                YouTubeVideoId = videoId,
                UploadedById = User.GetUserId(),
                UploadedAtUtc = DateTime.UtcNow
            });

            if (saved.Replaced && !string.IsNullOrEmpty(saved.PreviousStoredFileName))
            {
                Files().DeleteVideo(organizationId.Value, saved.PreviousStoredFileName);
            }

            AuditWriter.Write(
                saved.Replaced ? HelpVideoRules.AuditReplace : HelpVideoRules.AuditUpload,
                HelpVideoRules.AuditEntityType,
                saved.Id.ToString(CultureInfo.InvariantCulture),
                saved.Replaced && !string.IsNullOrEmpty(saved.PreviousStoredFileName) ? "StoredFileName=" + saved.PreviousStoredFileName : null,
                "Topic=" + slug + "; Title=" + displayTitle + "; YouTube=" + videoId);
            TempData["Message"] = "YouTube video linked to \"" + displayTitle + "\".";
            return RedirectToTenantAware("HelpVideos", "Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(HelpVideoRules.ManagePermission)]
        public ActionResult Delete(int id)
        {
            var organizationId = ResolveCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return NotFoundPage();
            }

            var deleted = _store.Delete(organizationId.Value, id);
            if (deleted == null)
            {
                TempData["Error"] = "That video was not found, or it is shared with all tenants and cannot be deleted here.";
                return RedirectToTenantAware("HelpVideos", "Index");
            }

            if (!deleted.IsYouTube)
            {
                Files().DeleteVideo(deleted.OrganizationId, deleted.StoredFileName);
            }

            AuditWriter.Write(
                HelpVideoRules.AuditDelete,
                HelpVideoRules.AuditEntityType,
                deleted.Id.ToString(CultureInfo.InvariantCulture),
                "Topic=" + deleted.TopicSlug + "; Title=" + deleted.Title
                    + (deleted.IsYouTube ? "; YouTube=" + deleted.YouTubeVideoId : "; File=" + deleted.OriginalFileName),
                null);
            TempData["Message"] = "Video \"" + deleted.Title + "\" deleted.";
            return RedirectToTenantAware("HelpVideos", "Index");
        }

        private HelpVideoUploadSession LoadOwnSession(string uploadId)
        {
            var organizationId = ResolveCurrentOrganizationId();
            var session = Files().LoadSession(uploadId);
            if (session == null || !organizationId.HasValue || session.OrganizationId != organizationId.Value
                || !string.Equals(session.UserId, User.GetUserId(), StringComparison.Ordinal))
            {
                return null;
            }

            return session;
        }

        private static string TopicTitle(string slug)
        {
            foreach (var group in HelpVideoCatalog.BuildGroups())
            {
                foreach (var topic in group.Topics)
                {
                    if (string.Equals(topic.Id, slug, StringComparison.Ordinal))
                    {
                        return topic.Title;
                    }
                }
            }

            return null;
        }

        private HelpVideoFiles Files()
        {
            return new HelpVideoFiles(Server.MapPath(VideoRootVirtualPath));
        }

        private JsonResult JsonError(string message)
        {
            Response.StatusCode = 400;
            Response.TrySkipIisCustomErrors = true;
            return Json(new { ok = false, error = message });
        }
    }
}

using System;
using System.Collections.Generic;

namespace AssetManagement.Application.Contracts
{
    /// <summary>
    /// One how-to video (migration 109, table HelpVideo): either an uploaded file (StoredFileName, ContentType,
    /// SizeBytes) or an unlisted YouTube video (YouTubeVideoId; the file fields are null / 0).
    /// </summary>
    public class HelpVideoInfo
    {
        public int Id { get; set; }

        /// <summary>Owning tenant. NULL = shown to every tenant (only the import helper writes those rows).</summary>
        public int? OrganizationId { get; set; }

        public string TopicSlug { get; set; }
        public string Title { get; set; }
        public string OriginalFileName { get; set; }

        /// <summary>File name under ~/App_Data/HelpVideos/{org id or "global"}/ (a GUID plus .mp4 / .webm).</summary>
        public string StoredFileName { get; set; }

        public string ContentType { get; set; }

        /// <summary>0 for a YouTube row.</summary>
        public long SizeBytes { get; set; }

        /// <summary>11-character id of an unlisted YouTube video; null for an uploaded file.</summary>
        public string YouTubeVideoId { get; set; }

        public string UploadedById { get; set; }
        public string UploadedByName { get; set; }
        public DateTime UploadedAtUtc { get; set; }

        public bool IsYouTube
        {
            get { return !string.IsNullOrEmpty(YouTubeVideoId); }
        }

        public bool IsGlobal
        {
            get { return !OrganizationId.HasValue; }
        }
    }

    public class HelpVideoSave
    {
        public int OrganizationId { get; set; }
        public string TopicSlug { get; set; }
        public string Title { get; set; }
        public string OriginalFileName { get; set; }
        public string StoredFileName { get; set; }
        public string ContentType { get; set; }
        public long SizeBytes { get; set; }

        /// <summary>Set for a YouTube row; the file fields are then left null / 0.</summary>
        public string YouTubeVideoId { get; set; }

        public string UploadedById { get; set; }
        public DateTime UploadedAtUtc { get; set; }
    }

    public class HelpVideoSaveResult
    {
        public int Id { get; set; }

        /// <summary>True when an existing video of the same topic (same tenant) was replaced.</summary>
        public bool Replaced { get; set; }

        /// <summary>Stored file of the replaced video (null when it was a YouTube row), for the caller to delete from disk after the row is saved.</summary>
        public string PreviousStoredFileName { get; set; }
    }

    /// <summary>
    /// SQL side of the hidden how-to video page. Every call is tenant-scoped: reads return the tenant's own rows plus the
    /// all-tenant (OrganizationId NULL) rows; writes only ever touch the tenant's own rows.
    /// </summary>
    public interface IHelpVideoStore
    {
        IList<HelpVideoInfo> GetVisible(int organizationId);

        /// <summary>Null when the video does not exist or belongs to another tenant.</summary>
        HelpVideoInfo GetVisibleById(int organizationId, int id);

        /// <summary>Insert, or replace the tenant's existing video for the same topic (one video per topic per tenant).</summary>
        HelpVideoSaveResult Save(HelpVideoSave video);

        /// <summary>Deletes one of the tenant's own videos (never an all-tenant row). Null when nothing was deleted.</summary>
        HelpVideoInfo Delete(int organizationId, int id);
    }
}

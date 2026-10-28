-- 109 (feat/hidden-help-videos): metadata for the hidden how-to video page (/{tenant}/HelpVideos).
-- Brief: Allan via Debugging bot, 2026-10-28: "reactivate the hidden help page for uploading the video".
-- 109 is the next free number on every branch (108_requisition_line_asset_name.sql is the last one applied locally).
-- What it does (additive only; no existing table, column, index or row is changed):
--   HelpVideo - one row per how-to video: tenant (NULL = shown to every tenant), topic slug, title, and EITHER
--   an uploaded file (original file name, stored file name = a GUID + .mp4/.webm under
--   ~/App_Data/HelpVideos/{org id or "global"}/, content type, size) OR an unlisted YouTube video id
--   (YouTubeVideoId, 11 characters; embedded as https://www.youtube-nocookie.com/embed/{id}?rel=0, Allan 2026-10-28:
--   "On yt, only users with the link should be able to view"). CK_HelpVideo_Source enforces exactly one of the two.
--   Uploader and upload time (UTC). One video per topic per tenant (UX_HelpVideo_Org_Topic, not filtered;
--   DECISIONS A11). The video bytes stay on disk, never in SQL. App_Data is excluded from the publish robocopy.
-- No FK to Organization on purpose (NULL means all tenants; same as LabelPrintBatchItem.OrganizationId). The org purge
-- deletes the tenant's rows (OrganizationPurgeSql step HelpVideo).
-- Idempotent: every step is guarded; re-running is a no-op.
-- Rollback: database/scripts/ops/109_Rollback_HelpVideo_PREPARE_DO_NOT_RUN.sql (only after redeploying a build
--   without the help video page; the files under App_Data/HelpVideos are left on disk).
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.HelpVideo', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[HelpVideo]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_HelpVideo] PRIMARY KEY CLUSTERED,
        [OrganizationId] INT NULL,
        [TopicSlug] NVARCHAR(80) NOT NULL,
        [Title] NVARCHAR(200) NOT NULL,
        [OriginalFileName] NVARCHAR(260) NULL,
        [StoredFileName] NVARCHAR(100) NULL,
        [ContentType] NVARCHAR(50) NULL,
        [SizeBytes] BIGINT NULL,
        [YouTubeVideoId] VARCHAR(11) NULL,
        [UploadedById] NVARCHAR(256) NULL,
        [UploadedAt] DATETIME2(3) NOT NULL CONSTRAINT [DF_HelpVideo_UploadedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [CK_HelpVideo_ContentType] CHECK ([ContentType] IN (N'video/mp4', N'video/webm')),
        CONSTRAINT [CK_HelpVideo_SizeBytes] CHECK ([SizeBytes] > 0),
        CONSTRAINT [CK_HelpVideo_YouTubeVideoId] CHECK ([YouTubeVideoId] IS NULL
            OR (DATALENGTH([YouTubeVideoId]) = 11 AND [YouTubeVideoId] NOT LIKE '%[^A-Za-z0-9_-]%')),
        CONSTRAINT [CK_HelpVideo_Source] CHECK (
            ([YouTubeVideoId] IS NULL AND [OriginalFileName] IS NOT NULL AND [StoredFileName] IS NOT NULL
                AND [ContentType] IS NOT NULL AND [SizeBytes] IS NOT NULL)
            OR ([YouTubeVideoId] IS NOT NULL AND [OriginalFileName] IS NULL AND [StoredFileName] IS NULL
                AND [ContentType] IS NULL AND [SizeBytes] IS NULL))
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'dbo.HelpVideo') AND [name] = N'UX_HelpVideo_Org_Topic')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_HelpVideo_Org_Topic] ON [dbo].[HelpVideo] ([OrganizationId], [TopicSlug]);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'109_HelpVideo.sql')
BEGIN
    INSERT INTO [dbo].[SchemaMigrationHistory] ([ScriptName], [Checksum], [AppliedAt], [AppliedBy])
    VALUES (N'109_HelpVideo.sql', NULL, SYSUTCDATETIME(), SUSER_SNAME());
END

COMMIT TRANSACTION;

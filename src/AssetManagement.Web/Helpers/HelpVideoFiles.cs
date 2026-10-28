using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AssetManagement.Application.Helpers;

namespace AssetManagement.Web.Helpers
{
    /// <summary>Upload problem with a message that is safe to show to the admin.</summary>
    public class HelpVideoUploadException : InvalidOperationException
    {
        public HelpVideoUploadException(string message)
            : base(message)
        {
        }
    }

    /// <summary>One chunked upload in progress (stored next to the .part file as {uploadId}.session).</summary>
    public class HelpVideoUploadSession
    {
        public string UploadId { get; set; }
        public int OrganizationId { get; set; }
        public string UserId { get; set; }
        public string TopicSlug { get; set; }
        public string Title { get; set; }
        public string OriginalFileName { get; set; }
        public string Extension { get; set; }
        public long TotalBytes { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    /// <summary>
    /// Disk side of the hidden how-to video page. Videos: {root}/{org id or "global"}/{guid}.mp4|.webm.
    /// Uploads in progress: {root}/_incoming/{uploadId}.part + .session (stale ones are removed after a day).
    /// The root is ~/App_Data/HelpVideos (never served directly; App_Data is skipped by the publish robocopy).
    /// No System.Web dependency so the chunk logic is unit-testable against a temp folder.
    /// </summary>
    public class HelpVideoFiles
    {
        public const string IncomingFolderName = "_incoming";
        public const string GlobalFolderName = "global";
        public static readonly TimeSpan StaleUploadAge = TimeSpan.FromHours(24);

        private readonly string _root;

        public HelpVideoFiles(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentNullException("rootPath");
            }

            _root = Path.GetFullPath(rootPath);
        }

        public string RootPath
        {
            get { return _root; }
        }

        /// <summary>Full path of a stored video, or null when the name is not a valid stored name or the file is missing.</summary>
        public string GetExistingVideoPath(int? organizationId, string storedFileName)
        {
            var path = GetVideoPath(organizationId, storedFileName);
            return path != null && File.Exists(path) ? path : null;
        }

        public string GetVideoPath(int? organizationId, string storedFileName)
        {
            if (!HelpVideoRules.IsValidStoredFileName(storedFileName))
            {
                return null;
            }

            var path = Path.GetFullPath(Path.Combine(FolderFor(organizationId), storedFileName));
            return IsUnderRoot(path) ? path : null;
        }

        public string FolderFor(int? organizationId)
        {
            var name = organizationId.HasValue
                ? organizationId.Value.ToString(CultureInfo.InvariantCulture)
                : GlobalFolderName;
            return Path.Combine(_root, name);
        }

        public HelpVideoUploadSession StartUpload(HelpVideoUploadSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException("session");
            }

            if (session.TotalBytes <= 0)
            {
                throw new HelpVideoUploadException("The file is empty.");
            }

            if (session.TotalBytes > HelpVideoRules.MaxFileBytes)
            {
                throw new HelpVideoUploadException("The file is larger than " + HelpVideoRules.FormatSize(HelpVideoRules.MaxFileBytes) + ".");
            }

            if (HelpVideoRules.ContentTypeForExtension(session.Extension) == null)
            {
                throw new HelpVideoUploadException("Only .mp4 and .webm videos can be uploaded.");
            }

            CleanupStaleUploads(StaleUploadAge);
            var incoming = IncomingFolder();
            Directory.CreateDirectory(incoming);
            session.UploadId = Guid.NewGuid().ToString("N");
            session.CreatedUtc = DateTime.UtcNow;
            using (File.Create(PartPath(session.UploadId)))
            {
            }

            File.WriteAllText(SessionPath(session.UploadId), Serialize(session), Encoding.UTF8);
            return session;
        }

        /// <summary>Null when the upload id is invalid, unknown or already finished.</summary>
        public HelpVideoUploadSession LoadSession(string uploadId)
        {
            if (!HelpVideoRules.IsValidUploadId(uploadId))
            {
                return null;
            }

            var path = SessionPath(uploadId);
            if (!File.Exists(path) || !File.Exists(PartPath(uploadId)))
            {
                return null;
            }

            try
            {
                return Deserialize(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception)
            {
                // Damaged or hand-edited session file: treat the upload as unknown.
                return null;
            }
        }

        public long GetReceivedBytes(string uploadId)
        {
            var part = PartPath(uploadId);
            return File.Exists(part) ? new FileInfo(part).Length : 0;
        }

        /// <summary>
        /// Writes one chunk at <paramref name="offset"/>. A resent chunk that is already stored is accepted as-is (retry
        /// after a lost response); a chunk past the end of what was received is rejected. Returns the bytes received so far.
        /// </summary>
        public long AppendChunk(HelpVideoUploadSession session, long offset, Stream data, int length)
        {
            if (session == null)
            {
                throw new ArgumentNullException("session");
            }

            if (data == null || length <= 0)
            {
                throw new HelpVideoUploadException("The upload chunk is empty.");
            }

            if (length > HelpVideoRules.MaxChunkBytes)
            {
                throw new HelpVideoUploadException("The upload chunk is too large.");
            }

            if (offset < 0 || offset + length > session.TotalBytes)
            {
                throw new HelpVideoUploadException("The upload chunk does not fit the file size.");
            }

            using (var stream = new FileStream(PartPath(session.UploadId), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var current = stream.Length;
                if (offset > current)
                {
                    throw new HelpVideoUploadException("An upload chunk is missing. Start the upload again.");
                }

                if (offset + length <= current)
                {
                    // Duplicate of a chunk already stored (the browser retried): nothing to write.
                    return current;
                }

                if (offset < current)
                {
                    stream.SetLength(offset);
                }

                stream.Seek(offset, SeekOrigin.Begin);
                var buffer = new byte[64 * 1024];
                var remaining = length;
                while (remaining > 0)
                {
                    var read = data.Read(buffer, 0, Math.Min(buffer.Length, remaining));
                    if (read <= 0)
                    {
                        break;
                    }

                    stream.Write(buffer, 0, read);
                    remaining -= read;
                }

                if (remaining > 0)
                {
                    stream.SetLength(offset);
                    throw new HelpVideoUploadException("The upload chunk was cut short. Try again.");
                }

                return stream.Length;
            }
        }

        /// <summary>
        /// Checks the size and the file header, then moves the upload into the tenant folder under a new GUID name.
        /// Returns the stored file name. The upload files are removed either way when the check fails.
        /// </summary>
        public string CompleteUpload(HelpVideoUploadSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException("session");
            }

            var part = PartPath(session.UploadId);
            if (!File.Exists(part))
            {
                throw new HelpVideoUploadException("The upload was not found. Start it again.");
            }

            var length = new FileInfo(part).Length;
            if (length != session.TotalBytes)
            {
                throw new HelpVideoUploadException("The upload is incomplete (" + length.ToString(CultureInfo.InvariantCulture)
                    + " of " + session.TotalBytes.ToString(CultureInfo.InvariantCulture) + " bytes).");
            }

            var header = new byte[16];
            int count;
            using (var stream = File.OpenRead(part))
            {
                count = stream.Read(header, 0, header.Length);
            }

            if (!HelpVideoRules.HeaderMatchesExtension(header, count, session.Extension))
            {
                AbortUpload(session.UploadId);
                throw new HelpVideoUploadException("The file is not a valid " + session.Extension.TrimStart('.').ToUpperInvariant() + " video.");
            }

            var folder = FolderFor(session.OrganizationId);
            Directory.CreateDirectory(folder);
            var stored = Guid.NewGuid().ToString("N") + session.Extension.ToLowerInvariant();
            File.Move(part, Path.Combine(folder, stored));
            TryDelete(SessionPath(session.UploadId));
            return stored;
        }

        public void AbortUpload(string uploadId)
        {
            if (!HelpVideoRules.IsValidUploadId(uploadId))
            {
                return;
            }

            TryDelete(PartPath(uploadId));
            TryDelete(SessionPath(uploadId));
        }

        public bool DeleteVideo(int? organizationId, string storedFileName)
        {
            var path = GetVideoPath(organizationId, storedFileName);
            return path != null && TryDelete(path);
        }

        public int CleanupStaleUploads(TimeSpan maxAge)
        {
            var incoming = IncomingFolder();
            if (!Directory.Exists(incoming))
            {
                return 0;
            }

            var removed = 0;
            var cutoff = DateTime.UtcNow - maxAge;
            foreach (var file in Directory.GetFiles(incoming))
            {
                var ext = Path.GetExtension(file);
                if ((string.Equals(ext, ".part", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".session", StringComparison.OrdinalIgnoreCase))
                    && File.GetLastWriteTimeUtc(file) < cutoff
                    && TryDelete(file))
                {
                    removed++;
                }
            }

            return removed;
        }

        private bool IsUnderRoot(string fullPath)
        {
            var root = _root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        private string IncomingFolder()
        {
            return Path.Combine(_root, IncomingFolderName);
        }

        private string PartPath(string uploadId)
        {
            if (!HelpVideoRules.IsValidUploadId(uploadId))
            {
                throw new HelpVideoUploadException("Unknown upload.");
            }

            return Path.Combine(IncomingFolder(), uploadId + ".part");
        }

        private string SessionPath(string uploadId)
        {
            if (!HelpVideoRules.IsValidUploadId(uploadId))
            {
                throw new HelpVideoUploadException("Unknown upload.");
            }

            return Path.Combine(IncomingFolder(), uploadId + ".session");
        }

        private static bool TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return false;
        }

        internal static string Serialize(HelpVideoUploadSession s)
        {
            var lines = new List<string>
            {
                "UploadId=" + Encode(s.UploadId),
                "OrganizationId=" + s.OrganizationId.ToString(CultureInfo.InvariantCulture),
                "UserId=" + Encode(s.UserId),
                "TopicSlug=" + Encode(s.TopicSlug),
                "Title=" + Encode(s.Title),
                "OriginalFileName=" + Encode(s.OriginalFileName),
                "Extension=" + Encode(s.Extension),
                "TotalBytes=" + s.TotalBytes.ToString(CultureInfo.InvariantCulture),
                "CreatedUtc=" + s.CreatedUtc.ToString("o", CultureInfo.InvariantCulture)
            };
            return string.Join("\n", lines);
        }

        internal static HelpVideoUploadSession Deserialize(string text)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in (text ?? string.Empty).Split('\n'))
            {
                var eq = line.IndexOf('=');
                if (eq > 0)
                {
                    values[line.Substring(0, eq)] = line.Substring(eq + 1).TrimEnd('\r');
                }
            }

            string v;
            return new HelpVideoUploadSession
            {
                UploadId = Decode(values.TryGetValue("UploadId", out v) ? v : null),
                OrganizationId = int.Parse(values["OrganizationId"], NumberStyles.Integer, CultureInfo.InvariantCulture),
                UserId = Decode(values.TryGetValue("UserId", out v) ? v : null),
                TopicSlug = Decode(values.TryGetValue("TopicSlug", out v) ? v : null),
                Title = Decode(values.TryGetValue("Title", out v) ? v : null),
                OriginalFileName = Decode(values.TryGetValue("OriginalFileName", out v) ? v : null),
                Extension = Decode(values.TryGetValue("Extension", out v) ? v : null),
                TotalBytes = long.Parse(values["TotalBytes"], NumberStyles.Integer, CultureInfo.InvariantCulture),
                CreatedUtc = DateTime.Parse(values["CreatedUtc"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            };
        }

        private static string Encode(string value)
        {
            return value == null ? string.Empty : Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        private static string Decode(string value)
        {
            return string.IsNullOrEmpty(value) ? null : Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
    }
}

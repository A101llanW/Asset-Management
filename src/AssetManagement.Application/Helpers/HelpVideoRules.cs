using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Rules for the hidden how-to video page (migration 109): allowed formats, file-name and topic clean-up, chunk sizes
    /// and HTTP Range parsing. Kept free of System.Web so it is unit-testable.
    /// </summary>
    public static class HelpVideoRules
    {
        /// <summary>Upload / replace / delete need this permission (the Settings module's only right).</summary>
        public const string ManagePermission = "Settings.Manage";

        /// <summary>
        /// Upload chunk size. Local IIS has maxRequestLength 51200 KB and no maxAllowedContentLength (IIS default
        /// 30,000,000 bytes), so a 4 MB chunk plus the multipart overhead stays far below both without a Web.config change.
        /// </summary>
        public const int ChunkSizeBytes = 4 * 1024 * 1024;

        /// <summary>Largest chunk the server accepts (the client sends <see cref="ChunkSizeBytes"/>).</summary>
        public const int MaxChunkBytes = 8 * 1024 * 1024;

        /// <summary>Largest video accepted (500 MB).</summary>
        public const long MaxFileBytes = 500L * 1024 * 1024;

        public const int MaxTitleLength = 200;
        public const int MaxFileNameLength = 120;
        public const int MaxTopicSlugLength = 80;

        public const string AuditUpload = "HelpVideos.Upload";
        public const string AuditReplace = "HelpVideos.Replace";
        public const string AuditDelete = "HelpVideos.Delete";
        public const string AuditEntityType = "HelpVideo";

        private static readonly Regex SlugPattern = new Regex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant);

        /// <summary>".mp4" or ".webm" (lower case) for an allowed file name, otherwise null.</summary>
        public static string GetAllowedExtension(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            var name = fileName.Trim();
            var dot = name.LastIndexOf('.');
            if (dot < 0 || dot == name.Length - 1)
            {
                return null;
            }

            var ext = name.Substring(dot).ToLowerInvariant();
            return ext == ".mp4" || ext == ".webm" ? ext : null;
        }

        /// <summary>Content type from the extension only (the browser-sent type is never trusted).</summary>
        public static string ContentTypeForExtension(string extension)
        {
            switch ((extension ?? string.Empty).ToLowerInvariant())
            {
                case ".mp4":
                    return "video/mp4";
                case ".webm":
                    return "video/webm";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Checks the first bytes of the file: MP4 has "ftyp" at offset 4, WebM starts with the EBML id 1A 45 DF A3.
        /// </summary>
        public static bool HeaderMatchesExtension(byte[] header, int count, string extension)
        {
            if (header == null)
            {
                return false;
            }

            count = Math.Min(count, header.Length);
            switch ((extension ?? string.Empty).ToLowerInvariant())
            {
                case ".mp4":
                    return count >= 8 && header[4] == (byte)'f' && header[5] == (byte)'t' && header[6] == (byte)'y' && header[7] == (byte)'p';
                case ".webm":
                    return count >= 4 && header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Display-safe file name: no path, only letters, digits, space, dot, dash and underscore, no leading dots,
        /// at most <see cref="MaxFileNameLength"/> characters, extension kept. Null when nothing usable is left.
        /// </summary>
        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            var name = fileName.Trim();
            var slash = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
            if (slash >= 0)
            {
                name = name.Substring(slash + 1);
            }

            var builder = new StringBuilder(name.Length);
            foreach (var ch in name)
            {
                var ok = (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')
                    || ch == '.' || ch == '-' || ch == '_' || ch == ' ';
                builder.Append(ok ? ch : '_');
            }

            var cleaned = Regex.Replace(builder.ToString(), "_{2,}", "_");
            cleaned = Regex.Replace(cleaned, " {2,}", " ");
            cleaned = Regex.Replace(cleaned, @"\.{2,}", ".");
            cleaned = cleaned.Trim(' ', '.', '_');
            if (cleaned.Length == 0)
            {
                return null;
            }

            if (cleaned.Length > MaxFileNameLength)
            {
                var ext = Path.GetExtension(cleaned) ?? string.Empty;
                if (ext.Length > 10)
                {
                    ext = string.Empty;
                }

                cleaned = cleaned.Substring(0, MaxFileNameLength - ext.Length).TrimEnd(' ', '.', '_') + ext;
            }

            return cleaned;
        }

        /// <summary>Lower-case topic slug (a-z, 0-9, single dashes), or null when it cannot be made valid.</summary>
        public static string NormalizeTopicSlug(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            var value = Regex.Replace(slug.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            if (value.Length == 0 || value.Length > MaxTopicSlugLength || !SlugPattern.IsMatch(value))
            {
                return null;
            }

            return value;
        }

        public static string NormalizeTitle(string title, string fallback)
        {
            var value = string.IsNullOrWhiteSpace(title) ? fallback : title;
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            value = Regex.Replace(value.Trim(), @"\s+", " ");
            return value.Length > MaxTitleLength ? value.Substring(0, MaxTitleLength).TrimEnd() : value;
        }

        /// <summary>Upload ids are 32 lower-case hex characters (Guid "N"); anything else is rejected before it reaches a path.</summary>
        public static bool IsValidUploadId(string uploadId)
        {
            return !string.IsNullOrEmpty(uploadId) && Regex.IsMatch(uploadId, "^[0-9a-f]{32}$", RegexOptions.CultureInvariant);
        }

        /// <summary>Stored names are a Guid "N" plus .mp4 / .webm, which also keeps them from escaping the video folder.</summary>
        public static bool IsValidStoredFileName(string storedFileName)
        {
            return !string.IsNullOrEmpty(storedFileName)
                && Regex.IsMatch(storedFileName, "^[0-9a-f]{32}\\.(mp4|webm)$", RegexOptions.CultureInvariant);
        }

        /// <summary>Privacy-enhanced embed host. Unlisted videos embed by id; no YouTube API / oEmbed call is made.</summary>
        public const string YouTubeEmbedBase = "https://www.youtube-nocookie.com/embed/";

        /// <summary>YouTube video ids are 11 characters of [A-Za-z0-9_-].</summary>
        public static bool IsValidYouTubeId(string id)
        {
            return !string.IsNullOrEmpty(id) && Regex.IsMatch(id, "^[A-Za-z0-9_-]{11}$", RegexOptions.CultureInvariant);
        }

        /// <summary>
        /// Video id from what an admin pastes: the bare id, or a youtu.be / youtube.com (watch?v=, embed/, shorts/, live/, v/)
        /// / youtube-nocookie.com link. Other hosts, playlists and anything else return null. Purely local parsing.
        /// </summary>
        public static string ParseYouTubeId(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            var text = input.Trim();
            if (IsValidYouTubeId(text))
            {
                return text;
            }

            if (text.IndexOf("://", StringComparison.Ordinal) < 0)
            {
                text = "https://" + text;
            }

            Uri uri;
            if (!Uri.TryCreate(text, UriKind.Absolute, out uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                return null;
            }

            var host = uri.Host.ToLowerInvariant();
            if (host.StartsWith("www.", StringComparison.Ordinal))
            {
                host = host.Substring(4);
            }
            else if (host.StartsWith("m.", StringComparison.Ordinal))
            {
                host = host.Substring(2);
            }

            var segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string candidate = null;
            if (host == "youtu.be")
            {
                candidate = segments.Length == 1 ? segments[0] : null;
            }
            else if (host == "youtube.com" || host == "youtube-nocookie.com")
            {
                if (segments.Length == 1 && string.Equals(segments[0], "watch", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var pair in uri.Query.TrimStart('?').Split('&'))
                    {
                        if (pair.StartsWith("v=", StringComparison.Ordinal))
                        {
                            candidate = pair.Substring(2);
                            break;
                        }
                    }
                }
                else if (segments.Length == 2)
                {
                    var kind = segments[0].ToLowerInvariant();
                    if (kind == "embed" || kind == "shorts" || kind == "live" || kind == "v")
                    {
                        candidate = segments[1];
                    }
                }
            }

            return IsValidYouTubeId(candidate) ? candidate : null;
        }

        /// <summary>https://www.youtube-nocookie.com/embed/{id}?rel=0 (Allan 2026-10-28), or null for an invalid id.</summary>
        public static string YouTubeEmbedUrl(string id)
        {
            return IsValidYouTubeId(id) ? YouTubeEmbedBase + id + "?rel=0" : null;
        }

        public static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024)
            {
                return (bytes / (1024d * 1024d)).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
            }

            if (bytes >= 1024)
            {
                return (bytes / 1024d).ToString("0", CultureInfo.InvariantCulture) + " KB";
            }

            return bytes.ToString(CultureInfo.InvariantCulture) + " bytes";
        }

        /// <summary>
        /// Parses a single-range "Range: bytes=..." header against a file of <paramref name="length"/> bytes.
        /// Returns <see cref="RangeParseResult.None"/> (send the whole file) when the header is missing, not a byte range,
        /// or asks for several ranges; <see cref="RangeParseResult.Unsatisfiable"/> (416) when it is outside the file.
        /// </summary>
        public static RangeParseResult TryParseRange(string rangeHeader, long length, out long start, out long end)
        {
            start = 0;
            end = length - 1;
            if (string.IsNullOrWhiteSpace(rangeHeader))
            {
                return RangeParseResult.None;
            }

            var value = rangeHeader.Trim();
            if (!value.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
            {
                return RangeParseResult.None;
            }

            value = value.Substring(6).Trim();
            if (value.IndexOf(',') >= 0)
            {
                return RangeParseResult.None;
            }

            var dash = value.IndexOf('-');
            if (dash < 0)
            {
                return RangeParseResult.None;
            }

            var first = value.Substring(0, dash).Trim();
            var last = value.Substring(dash + 1).Trim();
            long a;
            long b;
            if (first.Length == 0)
            {
                // Suffix range: the last N bytes.
                if (!long.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out b))
                {
                    return RangeParseResult.None;
                }

                if (b <= 0 || length <= 0)
                {
                    return RangeParseResult.Unsatisfiable;
                }

                start = Math.Max(0, length - b);
                end = length - 1;
                return RangeParseResult.Partial;
            }

            if (!long.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out a))
            {
                return RangeParseResult.None;
            }

            if (a >= length)
            {
                return RangeParseResult.Unsatisfiable;
            }

            if (last.Length == 0)
            {
                start = a;
                end = length - 1;
                return RangeParseResult.Partial;
            }

            if (!long.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out b) || b < a)
            {
                return RangeParseResult.None;
            }

            start = a;
            end = Math.Min(b, length - 1);
            return RangeParseResult.Partial;
        }
    }

    public enum RangeParseResult
    {
        None = 0,
        Partial = 1,
        Unsatisfiable = 2
    }
}

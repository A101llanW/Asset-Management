using System;
using System.Globalization;
using System.IO;
using System.Web;
using System.Web.Mvc;
using AssetManagement.Application.Helpers;

namespace AssetManagement.Web.Helpers
{
    /// <summary>
    /// Streams a help video from App_Data with HTTP Range support (206 Partial Content), so the browser can seek and
    /// start playback before the whole file arrives. Writes in ExecuteResult, after the global NoCache filter has set
    /// its headers, in 64 KB pieces, and stops when the browser disconnects.
    /// </summary>
    public class HelpVideoStreamResult : ActionResult
    {
        private const int BufferSize = 64 * 1024;

        public HelpVideoStreamResult(string filePath, string contentType, DateTime lastModifiedUtc)
        {
            FilePath = filePath;
            ContentType = contentType;
            LastModifiedUtc = lastModifiedUtc;
        }

        public string FilePath { get; private set; }
        public string ContentType { get; private set; }
        public DateTime LastModifiedUtc { get; private set; }

        public override void ExecuteResult(ControllerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            var request = context.HttpContext.Request;
            var response = context.HttpContext.Response;
            var length = new FileInfo(FilePath).Length;
            var etag = "\"" + LastModifiedUtc.Ticks.ToString("x", CultureInfo.InvariantCulture) + "-" + length.ToString("x", CultureInfo.InvariantCulture) + "\"";

            response.BufferOutput = false;
            response.ContentType = ContentType;
            response.AppendHeader("Accept-Ranges", "bytes");
            response.AppendHeader("ETag", etag);
            response.AppendHeader("X-Content-Type-Options", "nosniff");

            long start;
            long end;
            var rangeHeader = request.Headers["Range"];
            var ifRange = request.Headers["If-Range"];
            if (!string.IsNullOrEmpty(ifRange) && !string.Equals(ifRange, etag, StringComparison.Ordinal))
            {
                rangeHeader = null;
            }

            var range = HelpVideoRules.TryParseRange(rangeHeader, length, out start, out end);
            if (range == RangeParseResult.Unsatisfiable)
            {
                response.StatusCode = 416;
                response.TrySkipIisCustomErrors = true;
                response.AppendHeader("Content-Range", "bytes */" + length.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (range == RangeParseResult.Partial)
            {
                response.StatusCode = 206;
                response.AppendHeader("Content-Range", "bytes " + start.ToString(CultureInfo.InvariantCulture) + "-"
                    + end.ToString(CultureInfo.InvariantCulture) + "/" + length.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                response.StatusCode = 200;
                start = 0;
                end = length - 1;
            }

            var count = end - start + 1;
            response.AppendHeader("Content-Length", count.ToString(CultureInfo.InvariantCulture));
            if (string.Equals(request.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase) || count <= 0)
            {
                return;
            }

            try
            {
                using (var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize))
                {
                    stream.Seek(start, SeekOrigin.Begin);
                    var buffer = new byte[BufferSize];
                    var remaining = count;
                    while (remaining > 0 && response.IsClientConnected)
                    {
                        var read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                        if (read <= 0)
                        {
                            break;
                        }

                        response.OutputStream.Write(buffer, 0, read);
                        response.Flush();
                        remaining -= read;
                    }
                }
            }
            catch (HttpException)
            {
                // The browser closed the connection (seek or page left); nothing to do.
            }
        }
    }
}

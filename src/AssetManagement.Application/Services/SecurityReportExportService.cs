using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Services
{
    public class SecurityReportExportService : ISecurityReportExportService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrganizationScopeService _organizationScope;

        public SecurityReportExportService(IUnitOfWork unitOfWork, IOrganizationScopeService organizationScope)
        {
            _unitOfWork = unitOfWork;
            _organizationScope = organizationScope;
        }

        public byte[] ExportCsv(SecurityLogsPageVm page)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Type,Timestamp (EAT),Username,IpAddress,Organization,Success,Detail");
            AppendLoginRows(builder, page);
            AppendEventRows(builder, page);
            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        public string ExportHtml(SecurityLogsPageVm page, string applicationBaseUrl = null)
        {
            var branding = ReportBrandingHelper.Resolve(_unitOfWork, _organizationScope, applicationBaseUrl);
            var builder = new StringBuilder();
            builder.Append("<html><head><meta charset=\"utf-8\"/><title>Security Report</title>");
            builder.Append("<style>.report-brand-bar{text-align:center;padding:16px 24px 8px;background:#fff;border-bottom:1px solid #edf2f7;}");
            builder.Append(".report-brand-logo{max-height:64px;max-width:240px;object-fit:contain;}");
            builder.Append(".report-brand-name{font-size:18px;font-weight:600;color:#2c3e50;letter-spacing:0.02em;}</style></head><body>");
            builder.Append(ReportHtmlBuilder.BuildBrandHeaderFragment(branding));
            builder.Append("<h1>Security Report</h1>");
            builder.Append("<h2>Login Attempts</h2><table border=\"1\" cellpadding=\"4\"><tr><th>Timestamp</th><th>Username</th><th>IP</th><th>Org</th><th>Success</th><th>Reason</th></tr>");
            if (page != null && page.LoginAttempts != null)
            {
                foreach (var row in page.LoginAttempts)
                {
                    builder.Append("<tr><td>").Append(KenyaTimeHelper.FormatUtc(row.AttemptedAtUtc)).Append("</td><td>")
                        .Append(HtmlEncode(row.Username)).Append("</td><td>")
                        .Append(HtmlEncode(row.IpAddress)).Append("</td><td>")
                        .Append(row.OrganizationId.HasValue ? row.OrganizationId.Value.ToString() : string.Empty).Append("</td><td>")
                        .Append(row.WasSuccessful ? "Yes" : "No").Append("</td><td>")
                        .Append(HtmlEncode(row.FailureReason)).Append("</td></tr>");
                }
            }

            builder.Append("</table><h2>Security Events</h2><table border=\"1\" cellpadding=\"4\"><tr><th>Timestamp</th><th>Type</th><th>Email</th><th>IP</th><th>Org</th></tr>");
            if (page != null && page.SecurityEvents != null)
            {
                foreach (var row in page.SecurityEvents)
                {
                    builder.Append("<tr><td>").Append(KenyaTimeHelper.FormatUtc(row.CreatedAtUtc)).Append("</td><td>")
                        .Append(HtmlEncode(row.EventType)).Append("</td><td>")
                        .Append(HtmlEncode(row.Email)).Append("</td><td>")
                        .Append(HtmlEncode(row.IpAddress)).Append("</td><td>")
                        .Append(row.OrganizationId.HasValue ? row.OrganizationId.Value.ToString() : string.Empty).Append("</td></tr>");
                }
            }

            builder.Append("</table></body></html>");
            return builder.ToString();
        }

        private static void AppendLoginRows(StringBuilder builder, SecurityLogsPageVm page)
        {
            if (page == null || page.LoginAttempts == null)
            {
                return;
            }

            foreach (var row in page.LoginAttempts)
            {
                builder.Append("Login,");
                builder.Append(Csv(KenyaTimeHelper.FormatUtc(row.AttemptedAtUtc)));
                builder.Append(',');
                builder.Append(Csv(row.Username));
                builder.Append(',');
                builder.Append(Csv(row.IpAddress));
                builder.Append(',');
                builder.Append(Csv(row.OrganizationId));
                builder.Append(',');
                builder.Append(row.WasSuccessful ? "Yes" : "No");
                builder.Append(',');
                builder.AppendLine(Csv(row.FailureReason));
            }
        }

        private static void AppendEventRows(StringBuilder builder, SecurityLogsPageVm page)
        {
            if (page == null || page.SecurityEvents == null)
            {
                return;
            }

            foreach (var row in page.SecurityEvents)
            {
                builder.Append("Event,");
                builder.Append(Csv(KenyaTimeHelper.FormatUtc(row.CreatedAtUtc)));
                builder.Append(',');
                builder.Append(Csv(row.Email));
                builder.Append(',');
                builder.Append(Csv(row.IpAddress));
                builder.Append(',');
                builder.Append(Csv(row.OrganizationId));
                builder.Append(',');
                builder.Append("Yes,");
                builder.AppendLine(Csv(row.EventType));
            }
        }

        private static string Csv(object value)
        {
            var text = value == null ? string.Empty : Convert.ToString(value);
            if (text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
            {
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            }

            return text;
        }

        private static string HtmlEncode(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }
    }
}

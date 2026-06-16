using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace AssetManagement.Application.Helpers
{
    public static class ReportHtmlBuilder
    {
        public static string BuildReport(
            string title,
            string subtitle,
            string themeColor,
            string generatedBy,
            string reportCode,
            string periodLabel,
            string filterSummary,
            IList<ReportStatCard> stats,
            IList<string> headers,
            IList<IList<string>> rows,
            string footerNote)
        {
            var sb = new StringBuilder();
            sb.Append(GetDocumentStart(themeColor));
            sb.Append("<div class=\"report-frame\">");
            sb.AppendFormat(
                "<div class=\"report-header-box\"><h1>{0}</h1><p>{1}</p></div>",
                Encode(title),
                Encode(subtitle));
            sb.Append("<div class=\"report-meta\">");
            sb.AppendFormat("<div class=\"meta-item\"><span class=\"meta-label\">Generated</span><span class=\"meta-value\">{0}</span></div>",
                Encode(DateTime.UtcNow.ToString("dd MMM yyyy HH:mm") + " UTC"));
            sb.AppendFormat("<div class=\"meta-item\"><span class=\"meta-label\">Prepared by</span><span class=\"meta-value\">{0}</span></div>",
                Encode(string.IsNullOrWhiteSpace(generatedBy) ? "System" : generatedBy));
            sb.AppendFormat("<div class=\"meta-item\"><span class=\"meta-label\">Report code</span><span class=\"meta-value\">{0}</span></div>",
                Encode(reportCode));
            sb.Append("</div>");

            if (!string.IsNullOrWhiteSpace(periodLabel) || !string.IsNullOrWhiteSpace(filterSummary))
            {
                sb.Append("<div class=\"report-filter-box\">");
                if (!string.IsNullOrWhiteSpace(periodLabel))
                {
                    sb.AppendFormat("<div><strong>Period:</strong> {0}</div>", Encode(periodLabel));
                }
                if (!string.IsNullOrWhiteSpace(filterSummary))
                {
                    sb.AppendFormat("<div><strong>Filters:</strong> {0}</div>", Encode(filterSummary));
                }
                sb.Append("</div>");
            }

            if (stats != null && stats.Count > 0)
            {
                sb.Append("<div class=\"stats-grid\">");
                foreach (var stat in stats)
                {
                    sb.AppendFormat(
                        "<div class=\"stat-card\"><div class=\"stat-value\">{0}</div><div class=\"stat-label\">{1}</div></div>",
                        Encode(stat.Value),
                        Encode(stat.Label));
                }
                sb.Append("</div>");
            }

            sb.Append(BuildTable(headers, rows));
            sb.AppendFormat(
                "<div class=\"report-footer\"><p>{0}</p><p class=\"report-footer-note\">{1}</p></div>",
                Encode("Nanosoft Asset Suite — Confidential"),
                Encode(footerNote ?? string.Empty));
            sb.Append("</div></body></html>");
            return sb.ToString();
        }

        private static string BuildTable(IList<string> headers, IList<IList<string>> rows)
        {
            var sb = new StringBuilder();
            sb.Append("<table class=\"report-table\"><thead><tr>");
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    sb.AppendFormat("<th>{0}</th>", Encode(header));
                }
            }
            sb.Append("</tr></thead><tbody>");

            if (rows == null || rows.Count == 0)
            {
                sb.AppendFormat(
                    "<tr><td colspan=\"{0}\" class=\"text-center text-muted\">No records matched your filters.</td></tr>",
                    headers == null || headers.Count == 0 ? 1 : headers.Count);
            }
            else
            {
                foreach (var row in rows)
                {
                    sb.Append("<tr>");
                    foreach (var cell in row)
                    {
                        sb.AppendFormat("<td>{0}</td>", Encode(cell));
                    }
                    sb.Append("</tr>");
                }
            }

            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        private static string GetDocumentStart(string themeColor)
        {
            var color = string.IsNullOrWhiteSpace(themeColor) ? "#0d6efd" : themeColor;
            return string.Format(@"<!DOCTYPE html><html><head><meta charset=""utf-8""/><style>
@page {{ margin: 1.5cm; size: A4; }}
body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; color: #2c3e50; background: #fff; }}
.report-frame {{ border: 1px solid #e1e8ed; border-radius: 16px; margin: 0; background: #fff; box-shadow: 0 5px 15px rgba(0,0,0,0.03); border-top: 5px solid {0}; overflow: hidden; }}
.report-header-box {{ text-align: center; background: {0}; color: white; padding: 28px 20px; }}
.report-header-box h1 {{ margin: 0; font-size: 26px; font-weight: 600; text-transform: uppercase; }}
.report-header-box p {{ margin: 6px 0 0 0; opacity: 0.92; font-size: 14px; }}
.report-meta {{ display: table; width: calc(100% - 48px); background: #f8f9fa; padding: 14px; border-radius: 10px; margin: 20px 24px; border-left: 5px solid {0}; }}
.meta-item {{ display: table-cell; width: 33.33%; vertical-align: top; }}
.meta-label {{ font-size: 10px; color: #7f8c8d; text-transform: uppercase; font-weight: bold; display: block; }}
.meta-value {{ font-size: 13px; color: #2c3e50; font-weight: 600; }}
.report-filter-box {{ margin: 0 24px 16px 24px; padding: 12px 14px; background: #eef6ff; border-radius: 10px; font-size: 13px; line-height: 1.5; }}
.stats-grid {{ display: table; width: calc(100% - 48px); margin: 0 24px 20px 24px; border-spacing: 12px 0; }}
.stat-card {{ display: table-cell; background: #f8f9fa; border-radius: 12px; padding: 16px; text-align: center; border: 1px solid #edf2f7; }}
.stat-value {{ font-size: 22px; font-weight: 700; color: {0}; }}
.stat-label {{ font-size: 11px; text-transform: uppercase; color: #7f8c8d; font-weight: 600; margin-top: 4px; }}
.report-table {{ border-collapse: collapse; width: calc(100% - 48px); margin: 0 24px 24px 24px; }}
.report-table th {{ background: {0}; color: white; padding: 12px; text-align: left; font-size: 11px; text-transform: uppercase; }}
.report-table td {{ border-bottom: 1px solid #f1f4f6; padding: 10px 12px; font-size: 12px; }}
.report-footer {{ text-align: center; padding: 16px 24px 24px 24px; color: #95a5a6; font-size: 11px; }}
.report-footer-note {{ margin-top: 6px; }}
.text-center {{ text-align: center; }}
.text-muted {{ color: #95a5a6; }}
</style></head><body>", color);
        }

        private static string Encode(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }
    }

    public class ReportStatCard
    {
        public string Label { get; set; }

        public string Value { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Helpers
{
    public static class ReportHtmlBuilder
    {
        private const string DefaultThemeColor = "#1a5278";

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
            var fragment = BuildReportFragment(
                title,
                subtitle,
                themeColor,
                generatedBy,
                reportCode,
                periodLabel,
                filterSummary,
                stats,
                headers,
                rows,
                footerNote);
            return WrapDocument(fragment);
        }

        public static string BuildReportFragment(
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
            var color = ResolveThemeColor(themeColor);
            var sb = new StringBuilder();
            sb.Append(GetStylesBlock(color));
            sb.Append("<div class=\"report-frame\">");
            sb.AppendFormat(
                "<div class=\"report-header-box\"><h1>{0}</h1><p>{1}</p></div>",
                Encode(title),
                Encode(subtitle));
            sb.Append("<div class=\"report-meta\">");
            sb.AppendFormat(
                "<div class=\"meta-item\"><span class=\"meta-label\">Generated</span><span class=\"meta-value\">{0}</span></div>",
                Encode(DateTime.UtcNow.ToString("dd MMM yyyy HH:mm") + " UTC"));
            sb.AppendFormat(
                "<div class=\"meta-item\"><span class=\"meta-label\">Prepared by</span><span class=\"meta-value\">{0}</span></div>",
                Encode(string.IsNullOrWhiteSpace(generatedBy) ? "System" : generatedBy));
            sb.AppendFormat(
                "<div class=\"meta-item\"><span class=\"meta-label\">Report code</span><span class=\"meta-value\">{0}</span></div>",
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
            sb.Append("</div>");
            return sb.ToString();
        }

        public static string BuildRequisitionDocument(PurchaseRequestDetailVm model, string generatedBy)
        {
            return WrapDocument(BuildRequisitionFragment(model, generatedBy));
        }

        public static string BuildRequisitionFragment(PurchaseRequestDetailVm model, string generatedBy)
        {
            if (model == null)
            {
                throw new ArgumentNullException("model");
            }

            var color = DefaultThemeColor;
            var sb = new StringBuilder();
            sb.Append(GetStylesBlock(color));
            sb.Append("<div class=\"report-frame\">");
            sb.AppendFormat(
                "<div class=\"report-header-box\"><h1>{0}</h1><p>Purchase requisition</p></div>",
                Encode(model.RequestNumber));

            sb.Append("<div class=\"report-meta\">");
            sb.AppendFormat(
                "<div class=\"meta-item\"><span class=\"meta-label\">Status</span><span class=\"meta-value\">{0}</span></div>",
                Encode(model.ApprovalStatus));
            sb.AppendFormat(
                "<div class=\"meta-item\"><span class=\"meta-label\">Department</span><span class=\"meta-value\">{0}</span></div>",
                Encode(string.IsNullOrWhiteSpace(model.DepartmentName) ? "—" : model.DepartmentName));
            sb.AppendFormat(
                "<div class=\"meta-item\"><span class=\"meta-label\">Generated</span><span class=\"meta-value\">{0}</span></div>",
                Encode(DateTime.UtcNow.ToString("dd MMM yyyy HH:mm") + " UTC"));
            sb.Append("</div>");

            sb.Append("<div class=\"detail-section\">");
            sb.Append("<h2 class=\"detail-section-title\">Request details</h2>");
            sb.Append(BuildDetailRow("Submitted by", string.IsNullOrWhiteSpace(model.RequestedByName) ? model.RequestedById : model.RequestedByName));
            if (!string.IsNullOrWhiteSpace(model.OrderByUserName) || !string.IsNullOrWhiteSpace(model.OrderByUserId))
            {
                sb.Append(BuildDetailRow("Order by", string.IsNullOrWhiteSpace(model.OrderByUserName) ? model.OrderByUserId : model.OrderByUserName));
            }

            sb.Append(BuildDetailRow("Item", model.ItemDescription));
            if (model.TargetAssetId.HasValue)
            {
                var assetLabel = string.IsNullOrWhiteSpace(model.TargetAssetTag)
                    ? (string.IsNullOrWhiteSpace(model.TargetAssetName) ? model.TargetAssetId.Value.ToString() : model.TargetAssetName)
                    : model.TargetAssetTag + (string.IsNullOrWhiteSpace(model.TargetAssetName) ? string.Empty : " — " + model.TargetAssetName);
                sb.Append(BuildDetailRow("Tagged asset", assetLabel));
            }

            sb.Append(BuildDetailRow("Qty in stock", model.QuantityInStock.HasValue ? model.QuantityInStock.Value.ToString() : "—"));
            sb.Append(BuildDetailRow("Qty to order", model.Quantity.ToString()));
            sb.Append(BuildDetailRow("Required by", model.RequiredDate.HasValue ? model.RequiredDate.Value.ToString("yyyy-MM-dd") : "—"));
            sb.Append(BuildDetailRow("Est. unit cost", FormatMoney(model.EstimatedUnitCost, model.Currency)));
            sb.Append(BuildDetailRow("Est. total", FormatMoney(model.EstimatedUnitCost * model.Quantity, model.Currency)));
            sb.Append(BuildDetailRow("Submitted", model.CreatedAt.ToString("yyyy-MM-dd HH:mm")));
            if (model.ApprovedAt.HasValue)
            {
                sb.Append(BuildDetailRow("Approved", model.ApprovedAt.Value.ToString("yyyy-MM-dd HH:mm")));
            }

            sb.Append("</div>");

            sb.Append("<div class=\"detail-section\">");
            sb.Append("<h2 class=\"detail-section-title\">Justification</h2>");
            sb.AppendFormat("<p class=\"detail-text\">{0}</p>", Encode(string.IsNullOrWhiteSpace(model.Justification) ? "—" : model.Justification));
            if (!string.IsNullOrWhiteSpace(model.Notes))
            {
                sb.Append("<h2 class=\"detail-section-title\">Notes</h2>");
                sb.AppendFormat("<p class=\"detail-text\">{0}</p>", Encode(model.Notes));
            }

            if (model.HasAttachment && !string.IsNullOrWhiteSpace(model.AttachmentFileName))
            {
                sb.AppendFormat(
                    "<p class=\"detail-note\"><strong>Supporting file on record:</strong> {0}</p>",
                    Encode(model.AttachmentFileName));
            }

            sb.Append("</div>");

            var historyHeaders = new[] { "Stage", "Role", "Approver", "Decision", "Date", "Notes" };
            var historyRows = (model.ApprovalHistory ?? Enumerable.Empty<ApprovalDecisionHistoryVm>())
                .Select(x => (IList<string>)new List<string>
                {
                    x.StageNumber.ToString(),
                    x.RoleName,
                    x.ApproverName,
                    x.Decision,
                    x.DecisionDateText,
                    x.Notes
                })
                .ToList();
            sb.Append("<div class=\"detail-section\">");
            sb.Append("<h2 class=\"detail-section-title\">Approval history</h2>");
            sb.Append(BuildTable(historyHeaders, historyRows));
            sb.Append("</div>");

            sb.AppendFormat(
                "<div class=\"report-footer\"><p>{0}</p><p class=\"report-footer-note\">Prepared by {1}</p></div>",
                Encode("Nanosoft Asset Suite — Confidential"),
                Encode(string.IsNullOrWhiteSpace(generatedBy) ? "System" : generatedBy));
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string BuildDetailRow(string label, string value)
        {
            return string.Format(
                "<div class=\"detail-row\"><span class=\"detail-label\">{0}</span><span class=\"detail-value\">{1}</span></div>",
                Encode(label),
                Encode(string.IsNullOrWhiteSpace(value) ? "—" : value));
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

        private static string WrapDocument(string fragment)
        {
            return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>Document</title></head><body>" + fragment + "</body></html>";
        }

        private static string GetStylesBlock(string themeColor)
        {
            var color = ResolveThemeColor(themeColor);
            return string.Format(@"<style>
@page {{ margin: 1.5cm; size: A4; }}
html, body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; padding: 0; color: #2c3e50; background: #fff; min-height: 0; }}
.report-frame {{ border: 1px solid #e1e8ed; border-radius: 16px; margin: 0; background: #fff; box-shadow: 0 5px 15px rgba(0,0,0,0.03); border-top: 5px solid {0}; overflow: hidden; min-height: 0; }}
.report-header-box {{ text-align: center; background: {0}; color: white; padding: 28px 20px; -webkit-print-color-adjust: exact; print-color-adjust: exact; }}
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
.detail-section {{ margin: 0 24px 20px 24px; }}
.detail-section-title {{ font-size: 14px; text-transform: uppercase; letter-spacing: 0.04em; color: {0}; margin: 0 0 10px 0; }}
.detail-row {{ display: table; width: 100%; padding: 8px 0; border-bottom: 1px solid #edf2f7; }}
.detail-label {{ display: table-cell; width: 34%; font-size: 11px; text-transform: uppercase; color: #7f8c8d; font-weight: 700; vertical-align: top; padding-right: 12px; }}
.detail-value {{ display: table-cell; font-size: 13px; color: #2c3e50; vertical-align: top; }}
.detail-text {{ margin: 0; font-size: 13px; line-height: 1.55; white-space: pre-wrap; }}
.detail-note {{ margin: 12px 0 0 0; font-size: 12px; color: #566573; }}
.report-table {{ border-collapse: collapse; width: calc(100% - 48px); margin: 0 24px 24px 24px; }}
.report-table th {{ background: {0}; color: white; padding: 12px; text-align: left; font-size: 11px; text-transform: uppercase; -webkit-print-color-adjust: exact; print-color-adjust: exact; }}
.report-table td {{ border-bottom: 1px solid #f1f4f6; padding: 10px 12px; font-size: 12px; }}
.report-footer {{ text-align: center; padding: 16px 24px 24px 24px; color: #95a5a6; font-size: 11px; }}
.report-footer-note {{ margin-top: 6px; }}
.text-center {{ text-align: center; }}
.text-muted {{ color: #95a5a6; }}
</style>", color);
        }

        private static string ResolveThemeColor(string themeColor)
        {
            return string.IsNullOrWhiteSpace(themeColor) ? DefaultThemeColor : themeColor;
        }

        private static string FormatMoney(decimal amount, string currency)
        {
            var code = string.IsNullOrWhiteSpace(currency) ? string.Empty : " " + currency.Trim();
            return amount.ToString("N2") + code;
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

using System;

namespace AssetManagement.Application.Contracts
{
    /// <summary>One label going to a printer: which asset, how, and in which batch (rel1022a print log).</summary>
    public class LabelPrintJob
    {
        public int OrganizationId { get; set; }
        public int AssetId { get; set; }
        public string AssetTag { get; set; }
        public int? BatchId { get; set; }
        public bool IsReprint { get; set; }
        /// <summary>Zebra (Browser Print) or BrowserDialog (the browser's own print dialog).</summary>
        public string Mode { get; set; }
        public string TemplateId { get; set; }
        public string CodeType { get; set; }
        public string RequestedByUserId { get; set; }
        public DateTime RequestedAtUtc { get; set; }
    }

    public class LabelPrintDispatchResult
    {
        /// <summary>Where the label bytes go: "Client" (the user's browser sends them) or "DryRun" (tests; nothing is sent).</summary>
        public string Channel { get; set; }

        /// <summary>True only when this dispatcher itself delivered bytes to a device. Neither shipped dispatcher ever does.</summary>
        public bool SentByServer { get; set; }
    }

    /// <summary>
    /// rel1022a: every label print goes through this seam so it can be logged, and replaced by a dry-run in tests.
    /// The server NEVER talks to a printer: in production (<see cref="Services.LabelPrint.ClientBrowserPrintDispatcher"/>)
    /// the ZPL is sent by the user's browser through Zebra Browser Print exactly as before; the server only records the print.
    /// Tests use <see cref="Services.LabelPrint.DryRunLabelPrintDispatcher"/>, which only remembers the jobs.
    /// </summary>
    public interface ILabelPrintDispatcher
    {
        LabelPrintDispatchResult Dispatch(LabelPrintJob job);
    }
}

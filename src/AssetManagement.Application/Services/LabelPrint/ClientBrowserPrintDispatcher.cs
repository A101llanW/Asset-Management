using System;
using AssetManagement.Application.Contracts;

namespace AssetManagement.Application.Services.LabelPrint
{
    /// <summary>
    /// Production dispatcher. Label bytes are sent by the user's browser (asset-label-zebra.js -> Zebra Browser Print on the
    /// user's PC), unchanged from before rel1022a. This class deliberately has no network, socket, file or device access:
    /// it only confirms that the print is a client-side dispatch so the log can say so.
    /// </summary>
    public class ClientBrowserPrintDispatcher : ILabelPrintDispatcher
    {
        public const string ChannelName = "Client";

        public LabelPrintDispatchResult Dispatch(LabelPrintJob job)
        {
            if (job == null)
            {
                throw new ArgumentNullException("job");
            }

            return new LabelPrintDispatchResult { Channel = ChannelName, SentByServer = false };
        }
    }
}

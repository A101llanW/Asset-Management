using System;
using System.Collections.Generic;
using AssetManagement.Application.Contracts;

namespace AssetManagement.Application.Services.LabelPrint
{
    /// <summary>Mock / dry-run dispatcher for tests: remembers every job and sends nothing anywhere.</summary>
    public class DryRunLabelPrintDispatcher : ILabelPrintDispatcher
    {
        public const string ChannelName = "DryRun";

        private readonly List<LabelPrintJob> _jobs = new List<LabelPrintJob>();

        public IList<LabelPrintJob> Jobs
        {
            get { return _jobs; }
        }

        public LabelPrintDispatchResult Dispatch(LabelPrintJob job)
        {
            if (job == null)
            {
                throw new ArgumentNullException("job");
            }

            _jobs.Add(job);
            return new LabelPrintDispatchResult { Channel = ChannelName, SentByServer = false };
        }
    }
}

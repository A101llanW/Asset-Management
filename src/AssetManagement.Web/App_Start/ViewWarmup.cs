using System;
using System.Diagnostics;
using System.Threading;
using System.Web;
using System.Web.Compilation;
using System.Web.Hosting;

namespace AssetManagement.Web.App_Start
{
    /// <summary>
    /// After any app-pool start (deploy, recycle, idle shutdown) each Razor view compiles on its first
    /// use; with the deployed compilation settings (debug, batch off) that is one compiler run per view,
    /// measured at ~1.4 s for the first Dashboard, ~2.7 s for Asset Create and ~4 s for the first
    /// VerifyMfa. This compiles the login-to-dashboard and asset-form views on one background thread
    /// shortly after start, while the first visitor is still on the login page. Best effort only:
    /// failures are traced and the view simply compiles on demand as before.
    /// </summary>
    public static class ViewWarmup
    {
        private static readonly string[] ViewPaths =
        {
            "~/Views/_ViewStart.cshtml",
            "~/Views/Account/Login.cshtml",
            "~/Views/Shared/_PublicLayout.cshtml",
            "~/Views/Shared/_LoginBackground.cshtml",
            "~/Views/Shared/_AuthPortalBrand.cshtml",
            "~/Views/Account/VerifyMfa.cshtml",
            "~/Views/Shared/_Layout.cshtml",
            "~/Views/Shared/_Alerts.cshtml",
            "~/Views/Shared/_SidebarNav.cshtml",
            "~/Views/Shared/_SupportSessionOverlay.cshtml",
            "~/Views/Shared/_HelpBot.cshtml",
            "~/Views/Shared/_PageHeader.cshtml",
            "~/Views/Dashboard/Index.cshtml",
            "~/Views/Assets/Index.cshtml",
            "~/Views/Shared/_ListBulkToolbar.cshtml",
            "~/Views/Shared/_Pagination.cshtml",
            "~/Views/Assets/_AssetRoomGroups.cshtml",
            "~/Views/Assets/Edit.cshtml",
            "~/Views/Assets/Create.cshtml",
            "~/Views/Assets/_AssetClassificationFields.cshtml",
            "~/Views/Shared/_AssetTaxAmountFields.cshtml",
            "~/Views/Shared/_AssetDepreciationFields.cshtml",
            "~/Views/Shared/_ApprovalProcessMatrix.cshtml",
            "~/Views/Shared/_FormActions.cshtml",
            "~/Views/Shared/_AssetClassificationPickerModal.cshtml",
            "~/Views/Shared/_AcquisitionCostApplyModal.cshtml",
            "~/Views/Assets/Details.cshtml"
        };

        private static int _started;

        public static void Start()
        {
            if (!HostingEnvironment.IsHosted || Interlocked.Exchange(ref _started, 1) == 1)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(_ => Run());
        }

        private static void Run()
        {
            // Let the request that started the app compile its own page first.
            Thread.Sleep(1500);
            var watch = Stopwatch.StartNew();
            var compiled = 0;
            foreach (var path in ViewPaths)
            {
                if (HostingEnvironment.ShutdownReason != ApplicationShutdownReason.None)
                {
                    return;
                }

                try
                {
                    if (HostingEnvironment.VirtualPathProvider != null
                        && HostingEnvironment.VirtualPathProvider.FileExists(path)
                        && BuildManager.GetCompiledType(path) != null)
                    {
                        compiled++;
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning("View warm-up skipped {0}: {1}", path, ex.Message);
                }
            }

            Trace.WriteLine(string.Format("View warm-up compiled {0} views in {1} ms.", compiled, watch.ElapsedMilliseconds));
        }
    }
}
using System;
using System.Configuration;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Organizations;
using AssetManagement.Web.App_Start;

namespace AssetManagement.Web
{
    public class MvcApplication : System.Web.HttpApplication
    {
        private static readonly object NotificationScheduleLock = new object();
        private static readonly object LicenseExpiryLock = new object();
        private static readonly object OutboxDispatchLock = new object();
        private static DateTime _lastNotificationScheduleCheckUtc = DateTime.MinValue;
        private static DateTime _lastLicenseExpiryCheckUtc = DateTime.MinValue;
        private static DateTime _lastOutboxDispatchCheckUtc = DateTime.MinValue;

        protected void Application_Start()
        {
            DatabaseConfig.Configure();
            AutofacConfig.Register();
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        protected void Application_BeginRequest()
        {
            TryGenerateScheduledNotifications();
            TryProcessLicenseExpiry();
            TryProcessOutbox();
        }

        private static void TryGenerateScheduledNotifications()
        {
            if (DateTime.UtcNow - _lastNotificationScheduleCheckUtc < TimeSpan.FromHours(1))
            {
                return;
            }

            lock (NotificationScheduleLock)
            {
                if (DateTime.UtcNow - _lastNotificationScheduleCheckUtc < TimeSpan.FromHours(1))
                {
                    return;
                }

                _lastNotificationScheduleCheckUtc = DateTime.UtcNow;
            }

            try
            {
                var notificationService = DependencyResolver.Current.GetService<INotificationService>();
                notificationService?.TryGenerateScheduledNotifications();
            }
            catch
            {
                // Scheduled generation must not block requests.
            }
        }

        private static void TryProcessOutbox()
        {
            var interval = GetOutboxDispatchInterval();
            if (interval > TimeSpan.Zero && DateTime.UtcNow - _lastOutboxDispatchCheckUtc < interval)
            {
                return;
            }

            lock (OutboxDispatchLock)
            {
                if (interval > TimeSpan.Zero && DateTime.UtcNow - _lastOutboxDispatchCheckUtc < interval)
                {
                    return;
                }

                _lastOutboxDispatchCheckUtc = DateTime.UtcNow;
            }

            try
            {
                var dispatcher = DependencyResolver.Current.GetService<IOutboxDispatcher>();
                dispatcher?.ProcessPending(25);
            }
            catch
            {
                // Outbox dispatch must not block requests.
            }
        }

        private static TimeSpan GetOutboxDispatchInterval()
        {
            var setting = ConfigurationManager.AppSettings["OutboxDispatchIntervalSeconds"];
            int seconds;
            if (int.TryParse(setting, out seconds))
            {
                return seconds <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(seconds);
            }

            return TimeSpan.FromMinutes(5);
        }

        private static void TryProcessLicenseExpiry()
        {
            if (DateTime.UtcNow - _lastLicenseExpiryCheckUtc < TimeSpan.FromHours(1))
            {
                return;
            }

            lock (LicenseExpiryLock)
            {
                if (DateTime.UtcNow - _lastLicenseExpiryCheckUtc < TimeSpan.FromHours(1))
                {
                    return;
                }

                _lastLicenseExpiryCheckUtc = DateTime.UtcNow;
            }

            try
            {
                var licenseService = DependencyResolver.Current.GetService<IOrganizationLicenseService>();
                licenseService?.ProcessExpiredLicenses();
            }
            catch
            {
                // License expiry processing must not block requests.
            }
        }
    }
}

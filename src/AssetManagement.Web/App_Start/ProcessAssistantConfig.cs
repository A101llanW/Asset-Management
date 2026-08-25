using System;
using System.Configuration;

namespace AssetManagement.Web.App_Start
{
    // Hidden until future rollout. Toggle via Web.config appSettings ProcessAssistantEnabled=true.
    public static class ProcessAssistantConfig
    {
        public static bool IsEnabled
        {
            get
            {
                var setting = ConfigurationManager.AppSettings["ProcessAssistantEnabled"];
                return setting != null && setting.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}

using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    public static class AssetDocumentProcessHelper
    {
        /// <summary>
        /// Photo evidence applies when the asset still exists (not lost or stolen).
        /// </summary>
        public static bool IncidentTypeRequiresPhoto(IncidentType type)
        {
            return type != IncidentType.Lost && type != IncidentType.Stolen;
        }

        public static string BuildIncidentProcessReference(string incidentNumber)
        {
            return string.IsNullOrWhiteSpace(incidentNumber) ? "Incident" : "Incident " + incidentNumber.Trim();
        }
    }
}

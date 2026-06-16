using System.Collections.Generic;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.ViewModels
{
    public class AssetBulkActionRequestVm
    {
        public IList<int> AssetIds { get; set; }

        public string Action { get; set; }

        public int? TargetDepartmentId { get; set; }

        public AssetStatus? TargetStatus { get; set; }

        public string Notes { get; set; }

        public IList<string> PermissionCodes { get; set; }
    }

    public class AssetBulkActionResultVm
    {
        public int ProcessedCount { get; set; }

        public int SkippedCount { get; set; }

        public IList<string> Messages { get; set; }
    }
}

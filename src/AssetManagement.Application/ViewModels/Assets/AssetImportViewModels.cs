using System.Collections.Generic;

namespace AssetManagement.Application.ViewModels
{
    public class AssetImportResultVm
    {
        public int ImportedCount { get; set; }

        public int SkippedCount { get; set; }

        public IList<string> Messages { get; set; } = new List<string>();
    }
}

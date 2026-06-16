using System;
using System.Collections.Generic;

namespace AssetManagement.Web.ViewModels
{
    public class ListPageViewModel<T>
    {
        public IEnumerable<T> Items { get; set; } = new T[0];

        public string Search { get; set; }

        public string Sort { get; set; }

        public string Direction { get; set; } = "asc";

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 10;

        public int TotalCount { get; set; }

        public int TotalPages => PageSize <= 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);

        public int StartItem => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;

        public int EndItem => Math.Min(Page * PageSize, TotalCount);
    }

    public class PaginationViewModel
    {
        public int Page { get; set; }

        public int TotalPages { get; set; }

        public int TotalCount { get; set; }

        public int StartItem { get; set; }

        public int EndItem { get; set; }
    }
}

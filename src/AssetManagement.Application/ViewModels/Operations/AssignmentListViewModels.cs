using System;
using System.Collections.Generic;

namespace AssetManagement.Application.ViewModels
{
    public class AssignmentFilterVm
    {
        public string Search { get; set; }

        public int? DepartmentId { get; set; }

        public string CustodianUserId { get; set; }

        public bool? ActiveOnly { get; set; }
    }

    public class AssignmentListVm
    {
        public int Id { get; set; }

        public int AssetId { get; set; }

        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public string ToUserName { get; set; }

        public string ToDepartmentName { get; set; }

        public string AssignmentType { get; set; }

        public DateTime AssignedDate { get; set; }

        public DateTime? ExpectedReturnDate { get; set; }

        public bool RecipientAcknowledged { get; set; }

        public bool IsCurrentCustody { get; set; }
    }

    public class AssignmentListPageVm
    {
        public IList<AssignmentListVm> Items { get; set; }

        public int TotalCount { get; set; }

        public string Search { get; set; }

        public string Sort { get; set; }

        public string Direction { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }
}

using System.Collections.Generic;

namespace AssetManagement.Application.ViewModels
{
    public class RoomOtherParentGroupVm
    {
        public int AdminId { get; set; }

        public string AdminLabel { get; set; }

        public IList<RoomOtherParentSubOptionVm> SubDepartments { get; set; } = new List<RoomOtherParentSubOptionVm>();
    }

    public class RoomOtherParentSubOptionVm
    {
        public int SubDepartmentId { get; set; }

        public string Label { get; set; }
    }
}

using System.Web;
using System.Web.Mvc;
using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Web.Helpers
{
    public static class DepartmentKindHtmlHelpers
    {
        public static IHtmlString DepartmentKindBadge(this HtmlHelper html, DepartmentKind kind)
        {
            string css;
            switch (kind)
            {
                case DepartmentKind.Administrative:
                    css = "bg-primary";
                    break;
                case DepartmentKind.SubDepartment:
                    css = "bg-info text-dark";
                    break;
                case DepartmentKind.Grade:
                    css = "bg-purple";
                    break;
                case DepartmentKind.Class:
                    css = "bg-info text-dark";
                    break;
                case DepartmentKind.Room:
                    css = "bg-warning text-dark";
                    break;
                default:
                    css = "bg-secondary";
                    break;
            }

            // Kind badges: Department / Sub-department / Room (+ Grade / Class).
            string labelText;
            switch (kind)
            {
                case DepartmentKind.Administrative:
                    labelText = "Department";
                    break;
                case DepartmentKind.SubDepartment:
                    labelText = "Sub-department";
                    break;
                case DepartmentKind.Room:
                    labelText = "Room";
                    break;
                default:
                    labelText = DepartmentOrgHierarchyDisplay.FormatDepartmentKind(kind);
                    break;
            }
            var label = HttpUtility.HtmlEncode(labelText);
            return new HtmlString("<span class=\"badge " + css + " badge-kind\">" + label + "</span>");
        }
    }
}

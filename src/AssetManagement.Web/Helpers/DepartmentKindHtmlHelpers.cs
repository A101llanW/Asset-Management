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
                    css = "bg-secondary";
                    break;
                case DepartmentKind.SubDepartment:
                    css = "bg-primary";
                    break;
                case DepartmentKind.Grade:
                    css = "bg-purple";
                    break;
                case DepartmentKind.Class:
                    css = "bg-info";
                    break;
                case DepartmentKind.Room:
                    css = "bg-warning";
                    break;
                default:
                    css = "bg-secondary";
                    break;
            }

            var label = HttpUtility.HtmlEncode(DepartmentHierarchyRules.DisplayLabel(kind));
            return new HtmlString("<span class=\"badge " + css + " badge-kind\">" + label + "</span>");
        }
    }
}

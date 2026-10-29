using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Web.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    /// <summary>
    /// QA 188ce56 follow-up (Allan, 2026-10-29: classes show as "6A" under "Year 6", display only, tags unchanged):
    /// Asset Details custody history, flow summaries, pickers and JSON labels go through GradeDisplay, and the
    /// Departments Details/Edit breadcrumb shows the parent path. The printed label payload keeps the stored name.
    /// </summary>
    [TestFixture]
    public class GradeLabelHistoryTests
    {
        private static List<DepartmentVm> SchoolTree()
        {
            return new List<DepartmentVm>
            {
                new DepartmentVm { Id = 1, Name = "Academics", DepartmentKind = DepartmentKind.Administrative },
                new DepartmentVm { Id = 2, Name = "Grade 6", ParentDepartmentId = 1, DepartmentKind = DepartmentKind.Grade },
                new DepartmentVm { Id = 3, Name = "Grade 6A", ParentDepartmentId = 2, DepartmentKind = DepartmentKind.Class },
                new DepartmentVm { Id = 4, Name = "Grade 6 homerooms", ParentDepartmentId = 1, DepartmentKind = DepartmentKind.SubDepartment },
                new DepartmentVm { Id = 1482, Name = "Grade 6A", ParentDepartmentId = 4, DepartmentKind = DepartmentKind.Room }
            };
        }

        [Test]
        public void AncestorTrail_ClassUnderYear_IsRootFirstWithDisplayLabels()
        {
            var tree = SchoolTree();
            var trail = DepartmentTreeHelper.GetAncestorTrail(tree.Single(x => x.Id == 3), tree);
            CollectionAssert.AreEqual(new[] { 1, 2 }, trail.Select(x => x.Key).ToArray());
            CollectionAssert.AreEqual(new[] { "Academics", "Year 6" }, trail.Select(x => x.Value).ToArray());
        }

        [Test]
        public void AncestorTrail_RoomUnderSubDepartment_IncludesSubDepartmentAndPillar()
        {
            var tree = SchoolTree();
            var trail = DepartmentTreeHelper.GetAncestorTrail(tree.Single(x => x.Id == 1482), tree);
            CollectionAssert.AreEqual(new[] { "Academics", "Year 6 homerooms" }, trail.Select(x => x.Value).ToArray());
        }

        [Test]
        public void AncestorTrail_TopLevel_IsEmpty()
        {
            var tree = SchoolTree();
            Assert.AreEqual(0, DepartmentTreeHelper.GetAncestorTrail(tree[0], tree).Count);
            Assert.AreEqual(0, DepartmentTreeHelper.GetAncestorTrail(null, tree).Count);
            Assert.AreEqual(0, DepartmentTreeHelper.GetAncestorTrail(tree[2], (IEnumerable<DepartmentVm>)null).Count);
        }

        [Test]
        public void AncestorTrail_StopsAtParentOutsideScope()
        {
            // A scoped user who cannot see "Academics" only gets the parents in their own list (no name leak).
            var scoped = SchoolTree().Where(x => x.Id != 1).ToList();
            var trail = DepartmentTreeHelper.GetAncestorTrail(scoped.Single(x => x.Id == 3), scoped);
            CollectionAssert.AreEqual(new[] { "Year 6" }, trail.Select(x => x.Value).ToArray());
        }

        [Test]
        public void AncestorTrail_IsCycleSafe()
        {
            var a = new DepartmentVm { Id = 10, Name = "Grade 7", ParentDepartmentId = 11 };
            var b = new DepartmentVm { Id = 11, Name = "Grade 7B", ParentDepartmentId = 10 };
            var trail = DepartmentTreeHelper.GetAncestorTrail(a, new[] { a, b });
            CollectionAssert.AreEqual(new[] { "7B" }, trail.Select(x => x.Value).ToArray());
        }

        [Test]
        public void Breadcrumbs_Details_ShowFullPath_AncestorsLinked_CurrentNotLinked()
        {
            var tree = SchoolTree();
            var crumbs = DepartmentBreadcrumbs.Build(
                "/a/Departments",
                DepartmentTreeHelper.GetAncestorTrail(tree.Single(x => x.Id == 3), tree),
                id => "/a/Departments/Details/" + id,
                GradeDisplay.ToYear("Grade 6A"),
                null);
            CollectionAssert.AreEqual(new[] { "Departments", "Academics", "Year 6", "6A" }, crumbs.Select(x => x.Label).ToArray());
            Assert.AreEqual("/a/Departments", crumbs[0].Url);
            Assert.AreEqual("/a/Departments/Details/1", crumbs[1].Url);
            Assert.AreEqual("/a/Departments/Details/2", crumbs[2].Url);
            Assert.IsNull(crumbs[3].Url);
        }

        [Test]
        public void Breadcrumbs_Edit_LinkCurrentAndAddTrailingCrumb()
        {
            var tree = SchoolTree();
            var crumbs = DepartmentBreadcrumbs.Build(
                "/a/Departments",
                DepartmentTreeHelper.GetAncestorTrail(tree.Single(x => x.Id == 1482), tree),
                id => "/d/" + id,
                GradeDisplay.ToYear("Grade 6A"),
                "/d/1482",
                "Edit room");
            CollectionAssert.AreEqual(new[] { "Departments", "Academics", "Year 6 homerooms", "6A", "Edit room" }, crumbs.Select(x => x.Label).ToArray());
            Assert.AreEqual("/d/1482", crumbs[3].Url);
            Assert.IsNull(crumbs[4].Url);
        }

        [Test]
        public void Breadcrumbs_NoAncestors_KeepsOldShape()
        {
            var crumbs = DepartmentBreadcrumbs.Build("/a/Departments", null, null, "ICT", null);
            CollectionAssert.AreEqual(new[] { "Departments", "ICT" }, crumbs.Select(x => x.Label).ToArray());
        }

        [TestCase("Grade 6A", "Grade 6", "6A (Year 6)")]
        [TestCase("Grade 6", null, "Year 6")]
        [TestCase("ICT", "", "ICT")]
        [TestCase("Art room", "Grade 6 homerooms", "Art room (Year 6 homerooms)")]
        public void FormatNameWithParent_UsesDisplayLabels(string name, string parent, string expected)
        {
            Assert.AreEqual(expected, DepartmentLabelHelper.FormatNameWithParent(name, parent));
        }

        [Test]
        public void AssetDetails_CustodyAndPendingTransfer_UseGradeDisplay()
        {
            var view = ReadWeb("Views", "Assets", "Details.cshtml");
            StringAssert.Contains("GradeDisplay.ToYear(eventItem.FromEntity)", view);
            StringAssert.Contains("GradeDisplay.ToYear(eventItem.ToEntity)", view);
            StringAssert.Contains("GradeDisplay.ToYear(pendingTransfer.FromEntity)", view);
            StringAssert.Contains("GradeDisplay.ToYear(pendingTransfer.ToEntity)", view);
            StringAssert.DoesNotContain("<td>@eventItem.FromEntity</td>", view);
            StringAssert.DoesNotContain("<td>@eventItem.ToEntity</td>", view);
        }

        [Test]
        public void AssetMovementTab_StillUsesGradeDisplay()
        {
            var view = ReadWeb("Views", "Shared", "_AssetMovementTab.cshtml");
            StringAssert.Contains("GradeDisplay.ToYear(r.FromDepartmentName)", view);
            StringAssert.Contains("GradeDisplay.ToYear(r.ToDepartmentName)", view);
        }

        [Test]
        public void PrintedLabelPayload_KeepsStoredDepartmentName()
        {
            // data-department-name feeds asset-label.js (the printed physical label); it is not relabelled here.
            var view = ReadWeb("Views", "Assets", "_AssetLabelContent.cshtml");
            StringAssert.Contains("data-department-name=\"@Model.DepartmentName\"", view);
            var js = ReadWeb("Scripts", "app", "asset-label.js");
            StringAssert.Contains("data-department-name", js);
        }

        [Test]
        public void DepartmentPages_BreadcrumbUsesAncestorTrail()
        {
            foreach (var name in new[] { "Details.cshtml", "Edit.cshtml" })
            {
                var view = ReadWeb("Views", "Departments", name);
                StringAssert.Contains("DepartmentBreadcrumbs.Build(", view, name);
                StringAssert.Contains("ViewBag.BreadcrumbAncestors", view, name);
            }

            var controller = ReadWeb("Controllers", "DepartmentsController.cs");
            Assert.AreEqual(2, Regex.Matches(controller, Regex.Escape("ViewBag.BreadcrumbAncestors = DepartmentTreeHelper.GetAncestorTrail(model, scopedDepartments);")).Count);
        }

        [Test]
        public void FlowSummariesAndSources_UseGradeDisplay()
        {
            var index = ReadWeb("Views", "Departments", "Index.cshtml");
            foreach (var v in new[] { "parent", "child", "grandchild", "item" })
            {
                StringAssert.Contains("GradeDisplay.ToYear(" + v + ".EffectiveRequisitionFlowSummary)", index);
            }

            StringAssert.Contains("GradeDisplay.ToYear(Model.EffectiveRequisitionFlowSummary)", ReadWeb("Views", "Departments", "Details.cshtml"));
            StringAssert.Contains("GradeDisplay.ToYear(room.EffectiveRequisitionFlowSummary)", ReadWeb("Views", "Departments", "RequisitionFlows.cshtml"));
            StringAssert.Contains("GradeDisplay.ToYear(effectiveFlowSummary)", ReadWeb("Views", "Departments", "_RoomRequisitionApproval.cshtml"));
            StringAssert.Contains("GradeDisplay.ToYear(Model.ApprovalFlowSource)", ReadWeb("Views", "PurchaseRequests", "Details.cshtml"));
            StringAssert.Contains("GradeDisplay.ToYear(item.ApprovalFlowSource)", ReadWeb("Views", "PurchaseRequests", "Batch.cshtml"));
        }

        [Test]
        public void OtherRawDepartmentNames_NowUseGradeDisplay()
        {
            StringAssert.Contains("GradeDisplay.ToYear(Model.TargetDepartmentName)) : Model.RequestedByName", ReadWeb("Views", "AssetRequests", "Details.cshtml"));
            StringAssert.Contains("GradeDisplay.ToYear(selectedDeptName)", ReadWeb("Views", "Shared", "_DepartmentDispositionPrompt.cshtml"));
            StringAssert.Contains("GradeDisplay.ToYear(code.DepartmentName)", ReadWeb("Views", "Settings", "_AssetTagLevelPreview.cshtml"));
            StringAssert.Contains("GradeDisplay.ToYear(p.RoomLabel)", ReadWeb("Views", "PurchaseRequests", "Create.cshtml"));
            foreach (var c in new[] { "UsersController.cs", "RolesController.cs", "UserInvitationsController.cs" })
            {
                var source = ReadWeb("Controllers", c);
                StringAssert.Contains("DepartmentLabelHelper.FormatNameWithParent(d.Name, d.ParentDepartmentName)", source, c);
                StringAssert.DoesNotContain("d.Name + \" (\" + d.ParentDepartmentName + \")\"", source, c);
            }

            var suppliers = ReadWeb("Controllers", "SuppliersController.cs");
            StringAssert.Contains("GradeDisplay.ToYear(x.DepartmentName)", suppliers);
            StringAssert.Contains("GradeDisplay.ToYear(asset.DepartmentName)", suppliers);
            var requests = ReadWeb("Controllers", "AssetRequestsController.cs");
            StringAssert.Contains("GradeDisplay.ToYear(DepartmentTreeHelper.BuildPathLabel(x.Id, departments))", requests);
            var departments = ReadWeb("Controllers", "DepartmentsController.cs");
            StringAssert.Contains("childName = GradeDisplay.ToYear(x.ChildName)", departments);
            StringAssert.Contains("FlowSummary = GradeDisplay.ToYear(x.FlowSummary)", departments);
            var app = Path.Combine(RepositoryRoot(), "src", "AssetManagement.Application", "Services", "Purchases");
            StringAssert.Contains("\" - \" + GradeDisplay.ToYear(departmentName)", File.ReadAllText(Path.Combine(app, "PurchaseService.cs")));
            var receiving = File.ReadAllText(Path.Combine(app, "ReceivingService.cs"));
            StringAssert.Contains("placed in \" + GradeDisplay.ToYear(departmentName)", receiving);
            StringAssert.Contains("Intended destination: \" + GradeDisplay.ToYear(departmentName)", receiving);
        }

        private static string ReadWeb(params string[] parts)
        {
            return File.ReadAllText(Path.Combine(new[] { RepositoryRoot(), "src", "AssetManagement.Web" }.Concat(parts).ToArray()));
        }

        private static string RepositoryRoot()
        {
            var fromEnvironment = Environment.GetEnvironmentVariable("CODEXASSET_ROOT");
            if (!string.IsNullOrWhiteSpace(fromEnvironment) && Directory.Exists(Path.Combine(fromEnvironment, "src", "AssetManagement.Application")))
            {
                return fromEnvironment;
            }

            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "src", "AssetManagement.Application")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Repository root not found.");
        }
    }
}

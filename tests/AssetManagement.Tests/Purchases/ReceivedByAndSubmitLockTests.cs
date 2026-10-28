using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Purchases
{
    /// <summary>
    /// Local browser check 2026-10-28 fixes: "Received by" shows the user's name (id fallback), the global
    /// submit lock leaves a cancelled submit usable, and long item names wrap in the PO lines tables.
    /// </summary>
    [TestFixture]
    public class ReceivedByAndSubmitLockTests
    {
        private static FakeUserService Users()
        {
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "a461-role-procman", FirstName = "Demo", LastName = "ProcMgr", Email = "procmanager@example.test" });
            users.Seed(new UserVm { Id = "email-only", Email = "emailonly@example.test" });
            return users;
        }

        [Test]
        public void FillReceivedBy_UsesDisplayName_CaseInsensitiveId()
        {
            var rows = new[]
            {
                new AssetReceivingListVm { ReceivedById = "a461-role-procman" },
                new AssetReceivingListVm { ReceivedById = "A461-ROLE-PROCMAN" }
            };

            ClassificationOverrideNames.FillReceivedBy(Users(), rows);

            Assert.AreEqual("Demo ProcMgr", rows[0].ReceivedByDisplay);
            Assert.AreEqual("Demo ProcMgr", rows[1].ReceivedByDisplay);
        }

        [Test]
        public void FillReceivedBy_FallsBackToEmail_ThenId_ThenNothing()
        {
            var emailOnly = new AssetReceivingListVm { ReceivedById = "email-only" };
            var unknown = new AssetReceivingListVm { ReceivedById = "gone-user" };
            var blank = new AssetReceivingListVm { ReceivedById = "  " };
            var none = new AssetReceivingListVm();

            ClassificationOverrideNames.FillReceivedBy(Users(), new[] { emailOnly, unknown, blank, none, null });

            Assert.AreEqual("emailonly@example.test", emailOnly.ReceivedByDisplay);
            Assert.AreEqual("gone-user", unknown.ReceivedByDisplay, "unknown user shows the id");
            Assert.IsNull(blank.ReceivedByDisplay, "blank id shows the empty placeholder in the view");
            Assert.IsNull(none.ReceivedByDisplay);
        }

        [Test]
        public void FillReceivedBy_NullInputs_DoNothing()
        {
            var row = new AssetReceivingListVm { ReceivedById = "a461-role-procman" };
            Assert.DoesNotThrow(() => ClassificationOverrideNames.FillReceivedBy(null, new[] { row }));
            Assert.DoesNotThrow(() => ClassificationOverrideNames.FillReceivedBy(Users(), null));
            Assert.AreEqual("a461-role-procman", row.ReceivedByDisplay, "no user service: id fallback");
        }

        [Test]
        public void TrailNames_StillFilled_AfterSharedLookup()
        {
            var change = new ClassificationOverrideVm { ChangedById = "a461-role-procman" };
            ClassificationOverrideNames.Fill(Users(), new[] { change });
            Assert.AreEqual("Demo ProcMgr", change.ChangedByDisplay);
        }

        [Test]
        public void ReceivingService_FillsReceivedByNames()
        {
            var source = Read("src", "AssetManagement.Application", "Services", "Purchases", "ReceivingService.cs");
            StringAssert.Contains("ClassificationOverrideNames.FillReceivedBy(_userService, list);", source);
        }

        [Test]
        public void PurchaseDetails_ReceivedByColumn_ShowsDisplayNotRawId()
        {
            var view = Read("src", "AssetManagement.Web", "Views", "Purchases", "Details.cshtml");
            StringAssert.Contains("@(line.ReceivedByDisplay ?? DisplayText.Empty)", view);
            StringAssert.DoesNotContain(": line.ReceivedById)", view);
        }

        [Test]
        public void SubmitLock_SkipsCancelledSubmits_AndStaysIe11Safe()
        {
            var js = Read("src", "AssetManagement.Web", "Scripts", "app", "site-interactive.js");
            StringAssert.Contains("if (event.defaultPrevented || form.getAttribute(\"data-am-submitting\") === \"true\")", js);
            StringAssert.Contains("window.setTimeout(function () {", js);
            StringAssert.Contains("form.removeAttribute(\"data-am-submitting\");", js);

            var deferred = js.Substring(js.IndexOf("window.setTimeout(function () {", StringComparison.Ordinal));
            Assert.Less(deferred.IndexOf("if (event.defaultPrevented)", StringComparison.Ordinal),
                deferred.IndexOf("control.disabled = true;", StringComparison.Ordinal),
                "the cancel re-check runs before the buttons are disabled");

            Assert.IsFalse(js.Contains("=>"), "no arrow functions (IE11)");
            Assert.IsFalse(Regex.IsMatch(js, @"\b(let|const|class)\s"), "no let/const/class (IE11)");
            Assert.IsFalse(js.Contains("`"), "no template literals (IE11)");
        }

        [Test]
        public void PoLinesTables_WrapLongItemNames()
        {
            var css = Read("src", "AssetManagement.Web", "Content", "css", "site.css");
            StringAssert.Contains("#po-lines-table td:nth-child(2), #po-lines-card td:nth-child(2) { overflow-wrap: anywhere; word-break: break-word; }", css);
        }

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "src", "AssetManagement.Web")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            Assert.Inconclusive("Repository root not found.");
            return null;
        }

        private static string Read(params string[] parts)
        {
            return File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));
        }
    }
}

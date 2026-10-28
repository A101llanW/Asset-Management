using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Helpers;
using AssetManagement.Infrastructure.Services;
using AssetManagement.Tests.Requisitions;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.ViewModels;
using NUnit.Framework;

namespace AssetManagement.Tests.HelpVideos
{
    /// <summary>Hidden how-to video page (feat/hidden-help-videos, Allan 2026-10-28): rules, chunked upload, catalog.</summary>
    [TestFixture]
    public class HelpVideoRulesTests
    {
        [TestCase("intro.mp4", ".mp4")]
        [TestCase("Intro.MP4", ".mp4")]
        [TestCase("clip.webm", ".webm")]
        [TestCase("clip.mov", null)]
        [TestCase("clip.mp4.exe", null)]
        [TestCase("mp4", null)]
        [TestCase("clip.", null)]
        [TestCase("", null)]
        [TestCase(null, null)]
        public void OnlyMp4AndWebmAreAllowed(string name, string expected)
        {
            Assert.AreEqual(expected, HelpVideoRules.GetAllowedExtension(name));
        }

        [Test]
        public void ContentType_ComesFromTheExtension()
        {
            Assert.AreEqual("video/mp4", HelpVideoRules.ContentTypeForExtension(".mp4"));
            Assert.AreEqual("video/webm", HelpVideoRules.ContentTypeForExtension(".WEBM"));
            Assert.IsNull(HelpVideoRules.ContentTypeForExtension(".avi"));
        }

        [TestCase(@"C:\fakepath\HOD start requisition.mp4", "HOD start requisition.mp4")]
        [TestCase("../../web.config.mp4", "web.config.mp4")]
        [TestCase("..\\..\\App_Data\\x.webm", "x.webm")]
        [TestCase("<b>clip (1)</b>.mp4", "b_.mp4")]
        [TestCase("clip (1) \u00e9.mp4", "clip _1_ _.mp4")]
        [TestCase("   ...hidden.mp4", "hidden.mp4")]
        [TestCase("a  b__c.mp4", "a b_c.mp4")]
        [TestCase("../", null)]
        [TestCase("   ", null)]
        public void SanitizeFileName_StripsPathsAndOddCharacters(string input, string expected)
        {
            Assert.AreEqual(expected, HelpVideoRules.SanitizeFileName(input));
        }

        [Test]
        public void SanitizeFileName_KeepsTheExtension_WhenShortened()
        {
            var name = HelpVideoRules.SanitizeFileName(new string('a', 300) + ".webm");
            Assert.AreEqual(HelpVideoRules.MaxFileNameLength, name.Length);
            StringAssert.EndsWith(".webm", name);
        }

        [TestCase("hod-start-requisition", "hod-start-requisition")]
        [TestCase("  HOD Start Requisition ", "hod-start-requisition")]
        [TestCase("a__b--c", "a-b-c")]
        [TestCase("../etc", "etc")]
        [TestCase("---", null)]
        [TestCase("", null)]
        public void NormalizeTopicSlug(string input, string expected)
        {
            Assert.AreEqual(expected, HelpVideoRules.NormalizeTopicSlug(input));
        }

        [Test]
        public void NormalizeTopicSlug_RejectsTooLong()
        {
            Assert.IsNull(HelpVideoRules.NormalizeTopicSlug(new string('a', HelpVideoRules.MaxTopicSlugLength + 1)));
            Assert.IsNotNull(HelpVideoRules.NormalizeTopicSlug(new string('a', HelpVideoRules.MaxTopicSlugLength)));
        }

        [Test]
        public void NormalizeTitle_FallsBackAndTrims()
        {
            Assert.AreEqual("hod start", HelpVideoRules.NormalizeTitle("  ", "hod start"));
            Assert.AreEqual("A B", HelpVideoRules.NormalizeTitle(" A \r\n B ", "x"));
            Assert.AreEqual(HelpVideoRules.MaxTitleLength, HelpVideoRules.NormalizeTitle(new string('t', 500), null).Length);
            Assert.IsNull(HelpVideoRules.NormalizeTitle(null, null));
        }

        [Test]
        public void HeaderCheck_Mp4NeedsFtyp_WebmNeedsEbml()
        {
            var mp4 = new byte[] { 0, 0, 0, 0x20, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i', (byte)'s', (byte)'o', (byte)'m' };
            var webm = new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0, 0, 0, 0 };
            Assert.IsTrue(HelpVideoRules.HeaderMatchesExtension(mp4, mp4.Length, ".mp4"));
            Assert.IsFalse(HelpVideoRules.HeaderMatchesExtension(mp4, mp4.Length, ".webm"));
            Assert.IsTrue(HelpVideoRules.HeaderMatchesExtension(webm, webm.Length, ".webm"));
            Assert.IsFalse(HelpVideoRules.HeaderMatchesExtension(webm, webm.Length, ".mp4"));
            Assert.IsFalse(HelpVideoRules.HeaderMatchesExtension(Encoding.ASCII.GetBytes("<html>hello</html>"), 18, ".mp4"));
            Assert.IsFalse(HelpVideoRules.HeaderMatchesExtension(mp4, 6, ".mp4"), "too few bytes read");
        }

        [Test]
        public void UploadIdsAndStoredNames_AreStrict()
        {
            var id = Guid.NewGuid().ToString("N");
            Assert.IsTrue(HelpVideoRules.IsValidUploadId(id));
            Assert.IsFalse(HelpVideoRules.IsValidUploadId("../" + id));
            Assert.IsFalse(HelpVideoRules.IsValidUploadId(id.ToUpperInvariant()));
            Assert.IsTrue(HelpVideoRules.IsValidStoredFileName(id + ".mp4"));
            Assert.IsTrue(HelpVideoRules.IsValidStoredFileName(id + ".webm"));
            Assert.IsFalse(HelpVideoRules.IsValidStoredFileName(id + ".exe"));
            Assert.IsFalse(HelpVideoRules.IsValidStoredFileName("..\\" + id + ".mp4"));
        }

        [TestCase(null, RangeParseResult.None, 0, 999)]
        [TestCase("bytes=0-", RangeParseResult.Partial, 0, 999)]
        [TestCase("bytes=100-199", RangeParseResult.Partial, 100, 199)]
        [TestCase("bytes=900-5000", RangeParseResult.Partial, 900, 999)]
        [TestCase("bytes=-100", RangeParseResult.Partial, 900, 999)]
        [TestCase("bytes=-5000", RangeParseResult.Partial, 0, 999)]
        [TestCase("bytes=1000-", RangeParseResult.Unsatisfiable, 0, 0)]
        [TestCase("bytes=-0", RangeParseResult.Unsatisfiable, 0, 0)]
        [TestCase("bytes=0-1,5-9", RangeParseResult.None, 0, 999)]
        [TestCase("bytes=9-1", RangeParseResult.None, 0, 999)]
        [TestCase("items=0-1", RangeParseResult.None, 0, 999)]
        [TestCase("bytes=abc", RangeParseResult.None, 0, 999)]
        public void RangeParsing(string header, RangeParseResult expected, long expectedStart, long expectedEnd)
        {
            long start;
            long end;
            var result = HelpVideoRules.TryParseRange(header, 1000, out start, out end);
            Assert.AreEqual(expected, result);
            if (expected != RangeParseResult.Unsatisfiable)
            {
                Assert.AreEqual(expectedStart, start);
                Assert.AreEqual(expectedEnd, end);
            }
        }

        [Test]
        public void ChunkSize_FitsTheIisRequestLimits()
        {
            // IIS default maxAllowedContentLength 30,000,000 bytes; httpRuntime maxRequestLength 51200 KB.
            Assert.LessOrEqual(HelpVideoRules.MaxChunkBytes, 30000000 - 1024 * 1024);
            Assert.AreEqual(4 * 1024 * 1024, HelpVideoRules.ChunkSizeBytes);
            Assert.AreEqual("Settings.Manage", HelpVideoRules.ManagePermission);
        }
    }

    [TestFixture]
    public class HelpVideoFilesTests
    {
        private string _root;
        private HelpVideoFiles _files;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "hv-tests-" + Guid.NewGuid().ToString("N"));
            _files = new HelpVideoFiles(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        private static byte[] FakeMp4(int length)
        {
            var bytes = new byte[length];
            new Random(7).NextBytes(bytes);
            bytes[4] = (byte)'f';
            bytes[5] = (byte)'t';
            bytes[6] = (byte)'y';
            bytes[7] = (byte)'p';
            return bytes;
        }

        private HelpVideoUploadSession Start(long total, string ext = ".mp4")
        {
            return _files.StartUpload(new HelpVideoUploadSession
            {
                OrganizationId = 13,
                UserId = "user-1",
                TopicSlug = "hod-start-requisition",
                Title = "HOD start",
                OriginalFileName = "hod.mp4",
                Extension = ext,
                TotalBytes = total
            });
        }

        private long Append(HelpVideoUploadSession s, byte[] all, int offset, int count)
        {
            using (var ms = new MemoryStream(all, offset, count))
            {
                return _files.AppendChunk(s, offset, ms, count);
            }
        }

        [Test]
        public void ChunkedUpload_RoundTrips_AndLandsInTheTenantFolder()
        {
            var data = FakeMp4(10000);
            var s = Start(data.Length);
            var loaded = _files.LoadSession(s.UploadId);
            Assert.AreEqual(13, loaded.OrganizationId);
            Assert.AreEqual("user-1", loaded.UserId);
            Assert.AreEqual("HOD start", loaded.Title);

            Assert.AreEqual(4000, Append(loaded, data, 0, 4000));
            Assert.AreEqual(4000, Append(loaded, data, 0, 4000), "resent chunk is ignored");
            Assert.AreEqual(8000, Append(loaded, data, 4000, 4000));
            Assert.AreEqual(10000, Append(loaded, data, 8000, 2000));

            var stored = _files.CompleteUpload(loaded);
            Assert.IsTrue(HelpVideoRules.IsValidStoredFileName(stored));
            var path = _files.GetExistingVideoPath(13, stored);
            Assert.IsNotNull(path);
            StringAssert.StartsWith(Path.Combine(_root, "13"), path);
            CollectionAssert.AreEqual(data, File.ReadAllBytes(path));
            Assert.IsNull(_files.LoadSession(s.UploadId), "session removed after completion");
            Assert.IsNull(_files.GetExistingVideoPath(14, stored), "other tenant folder");
            Assert.IsTrue(_files.DeleteVideo(13, stored));
            Assert.IsFalse(File.Exists(path));
        }

        [Test]
        public void MissingChunk_IsRejected()
        {
            var data = FakeMp4(10000);
            var s = Start(data.Length);
            Assert.Throws<HelpVideoUploadException>(() => Append(s, data, 4000, 4000));
        }

        [Test]
        public void ChunkPastTheDeclaredSize_IsRejected()
        {
            var data = FakeMp4(5000);
            var s = Start(4000);
            Assert.Throws<HelpVideoUploadException>(() => Append(s, data, 0, 5000));
        }

        [Test]
        public void IncompleteUpload_CannotComplete()
        {
            var data = FakeMp4(10000);
            var s = Start(data.Length);
            Append(s, data, 0, 4000);
            Assert.Throws<HelpVideoUploadException>(() => _files.CompleteUpload(s));
        }

        [Test]
        public void WrongHeader_IsRejected_AndCleanedUp()
        {
            var data = Encoding.ASCII.GetBytes("this is not a video at all, just text");
            var s = Start(data.Length);
            Append(s, data, 0, data.Length);
            Assert.Throws<HelpVideoUploadException>(() => _files.CompleteUpload(s));
            Assert.IsNull(_files.LoadSession(s.UploadId));
            Assert.IsFalse(Directory.Exists(Path.Combine(_root, "13")) && Directory.GetFiles(Path.Combine(_root, "13")).Length > 0);
        }

        [Test]
        public void StartUpload_RejectsBadInput()
        {
            Assert.Throws<HelpVideoUploadException>(() => Start(0));
            Assert.Throws<HelpVideoUploadException>(() => Start(HelpVideoRules.MaxFileBytes + 1));
            Assert.Throws<HelpVideoUploadException>(() => Start(100, ".mov"));
        }

        [Test]
        public void LoadSession_RejectsInvalidIds()
        {
            Assert.IsNull(_files.LoadSession("../../web"));
            Assert.IsNull(_files.LoadSession(Guid.NewGuid().ToString("N")));
            Assert.IsNull(_files.GetVideoPath(13, "..\\..\\Web.config"));
        }

        [Test]
        public void StaleUploads_AreRemoved()
        {
            var s = Start(100);
            var part = Path.Combine(_root, HelpVideoFiles.IncomingFolderName, s.UploadId + ".part");
            var session = Path.Combine(_root, HelpVideoFiles.IncomingFolderName, s.UploadId + ".session");
            File.SetLastWriteTimeUtc(part, DateTime.UtcNow.AddDays(-2));
            File.SetLastWriteTimeUtc(session, DateTime.UtcNow.AddDays(-2));
            var fresh = Start(100);
            Assert.IsFalse(File.Exists(part), "StartUpload clears uploads older than a day");
            Assert.IsNotNull(_files.LoadSession(fresh.UploadId));
        }

        [Test]
        public void GlobalVideos_UseTheGlobalFolder()
        {
            StringAssert.EndsWith(Path.DirectorySeparatorChar + "global", _files.FolderFor(null));
        }
    }

    [TestFixture]
    public class HelpVideoCatalogTests
    {
        private static HelpVideoInfo Video(int id, int? org, string slug, string title = "t")
        {
            return new HelpVideoInfo { Id = id, OrganizationId = org, TopicSlug = slug, Title = title, ContentType = "video/mp4", SizeBytes = 10 };
        }

        [Test]
        public void Catalog_HasUniqueSlugs_AndTheHodTopics()
        {
            var slugs = HelpVideoCatalog.BuildGroups().SelectMany(g => g.Topics).Select(t => t.Id).ToList();
            CollectionAssert.AllItemsAreUnique(slugs);
            CollectionAssert.Contains(slugs, "hod-start-requisition");
            CollectionAssert.Contains(slugs, "hod-approve-requisition");
            foreach (var slug in slugs)
            {
                Assert.AreEqual(slug, HelpVideoRules.NormalizeTopicSlug(slug), slug);
            }

            Assert.IsFalse(HelpVideoCatalog.BuildGroups().Any(g => g.Id == "platform"), "platform-operator topics are not on the tenant page");
        }

        [Test]
        public void Build_AttachesVideos_TenantWinsOverGlobal_ExtrasGoToMoreVideos()
        {
            var model = HelpVideoCatalog.Build(new List<HelpVideoInfo>
            {
                Video(1, null, "hod-start-requisition", "global"),
                Video(2, 13, "hod-start-requisition", "tenant"),
                Video(3, null, "hod-approve-requisition"),
                Video(4, 13, "custom-topic", "Custom")
            }, true);

            var topics = model.Groups.SelectMany(g => g.Topics).ToList();
            Assert.AreEqual(2, topics.Single(t => t.Id == "hod-start-requisition").Video.Id);
            Assert.AreEqual(3, topics.Single(t => t.Id == "hod-approve-requisition").Video.Id);
            var more = model.Groups.Single(g => g.Id == HelpVideoCatalog.MoreVideosGroupId);
            Assert.AreEqual(4, more.Topics.Single().Video.Id);
            Assert.AreEqual(3, model.VideoCount);
            Assert.IsTrue(model.CanManage);
            Assert.IsTrue(model.TopicOptions.Single(o => o.Slug == "hod-start-requisition").HasVideo);
            Assert.IsFalse(model.TopicOptions.Single(o => o.Slug == "sign-in").HasVideo);
        }

        [Test]
        public void Build_WithoutVideos_HasNoMoreVideosGroup()
        {
            var model = HelpVideoCatalog.Build(null, false);
            Assert.IsFalse(model.CanManage);
            Assert.IsFalse(model.Groups.Any(g => g.Id == HelpVideoCatalog.MoreVideosGroupId));
            Assert.IsTrue(model.Groups.SelectMany(g => g.Topics).All(t => !t.HasVideo));
        }

        [Test]
        public void Purge_DeletesTheTenantsHelpVideos()
        {
            CollectionAssert.Contains(OrganizationPurgeSql.GetDeletedTableNames(), "HelpVideo");
        }
    }

    [TestFixture]
    public class HelpVideoFileRuleTests
    {
        private static string Read(params string[] parts)
        {
            return QaSixRequisitionFormTests.Read(parts);
        }

        [Test]
        public void Layout_NoLongerRendersTheHelpButton()
        {
            var layout = Read("src", "AssetManagement.Web", "Views", "Shared", "_Layout.cshtml");
            var live = System.Text.RegularExpressions.Regex.Replace(layout, @"@\*.*?\*@", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);
            StringAssert.DoesNotContain("_HelpBot", live);
            StringAssert.DoesNotContain("help-bot.js", live);
            StringAssert.DoesNotContain("HelpVideos", live, "no menu link to the hidden page");
        }

        [Test]
        public void NoViewLinksToTheHiddenPage()
        {
            var offenders = Directory.GetFiles(Path.Combine(RootOf(), "src", "AssetManagement.Web", "Views"), "*.cshtml", SearchOption.AllDirectories)
                .Where(f => !f.Replace('/', '\\').Contains("\\Views\\HelpVideos\\"))
                .Where(f => File.ReadAllText(f).IndexOf("HelpVideos", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            CollectionAssert.IsEmpty(offenders, "the page is reachable by direct URL only");
        }

        [Test]
        public void Controller_ManageActions_NeedSettingsManage_AndAntiforgery()
        {
            var type = typeof(AssetManagement.Web.Controllers.HelpVideosController);
            Assert.IsTrue(typeof(AssetManagement.Web.Controllers.BaseController).IsAssignableFrom(type), "TenantAuthorize via BaseController");
            foreach (var name in new[] { "UploadStart", "UploadChunk", "UploadComplete", "UploadCancel", "Delete" })
            {
                var method = type.GetMethods().Single(m => m.Name == name);
                var permission = method.GetCustomAttributes(typeof(AssetManagement.Web.Filters.PermissionAuthorizeAttribute), true);
                Assert.AreEqual(1, permission.Length, name + " needs PermissionAuthorize");
                Assert.AreEqual(1, method.GetCustomAttributes(typeof(System.Web.Mvc.ValidateAntiForgeryTokenAttribute), true).Length, name + " antiforgery");
                Assert.AreEqual(1, method.GetCustomAttributes(typeof(System.Web.Mvc.HttpPostAttribute), true).Length, name + " POST only");
            }

            foreach (var name in new[] { "Index", "Stream" })
            {
                var method = type.GetMethods().Single(m => m.Name == name);
                Assert.AreEqual(0, method.GetCustomAttributes(typeof(AssetManagement.Web.Filters.PermissionAuthorizeAttribute), true).Length, name + " is for every signed-in user");
            }

            var controllerSource = Read("src", "AssetManagement.Web", "Controllers", "HelpVideosController.cs");
            StringAssert.Contains("\"~/App_Data/HelpVideos\"", controllerSource);
        }

        [Test]
        public void View_LoadsItsScriptAndCss_WithCacheBusters()
        {
            var view = Read("src", "AssetManagement.Web", "Views", "HelpVideos", "Index.cshtml");
            StringAssert.Contains("help-videos-upload.js\")?v=@System.IO.File.GetLastWriteTimeUtc(Server.MapPath(\"~/Scripts/app/help-videos-upload.js\")).Ticks", view);
            StringAssert.Contains("help-videos.css\")?v=@System.IO.File.GetLastWriteTimeUtc(Server.MapPath(\"~/Content/css/help-videos.css\")).Ticks", view);
            Assert.GreaterOrEqual(System.Text.RegularExpressions.Regex.Matches(view, "AntiForgeryToken").Count, 2);
            var csproj = Read("src", "AssetManagement.Web", "AssetManagement.Web.csproj");
            foreach (var entry in new[]
            {
                "<Compile Include=\"Controllers\\HelpVideosController.cs\" />",
                "<Compile Include=\"Helpers\\HelpVideoFiles.cs\" />",
                "<Compile Include=\"Helpers\\HelpVideoStreamResult.cs\" />",
                "<Compile Include=\"ViewModels\\HelpCenterViewModels.cs\" />",
                "<Content Include=\"Views\\HelpVideos\\Index.cshtml\" />",
                "<Content Include=\"Scripts\\app\\help-videos-upload.js\" />",
                "<Content Include=\"Content\\css\\help-videos.css\" />"
            })
            {
                StringAssert.Contains(entry, csproj);
            }
        }

        [Test]
        public void HttpAuditLog_SkipsStreamAndChunks()
        {
            var filter = Read("src", "AssetManagement.Web", "Filters", "AuditLogAttribute.cs");
            StringAssert.Contains("\"HelpVideos\"", filter);
            StringAssert.Contains("\"Stream\"", filter);
            StringAssert.Contains("\"UploadChunk\"", filter);
        }

        [Test]
        public void Migration109_FollowsTheMigrationPattern()
        {
            var sql = Read("database", "scripts", "004_Migrations", "109_HelpVideo.sql");
            StringAssert.Contains("CREATE TABLE [dbo].[HelpVideo]", sql);
            StringAssert.Contains("N'109_HelpVideo.sql'", sql);
            StringAssert.Contains("SET XACT_ABORT ON;", sql);
            StringAssert.Contains("COMMIT TRANSACTION;", sql);
            StringAssert.DoesNotContain("WHERE [OrganizationId] IS", sql.Substring(sql.IndexOf("CREATE UNIQUE", StringComparison.Ordinal)), "no filtered index");
            var rollback = Read("database", "scripts", "ops", "109_Rollback_HelpVideo_PREPARE_DO_NOT_RUN.sql");
            StringAssert.Contains("DROP TABLE [dbo].[HelpVideo]", rollback);
            StringAssert.Contains("ConfirmRollback", rollback);
            StringAssert.Contains("DB_NAME() <> N'$(DbName)'", rollback);
        }

        [Test]
        public void AuditLabels_CoverTheNewActions()
        {
            var helper = Read("src", "AssetManagement.Application", "Helpers", "AuditDisplayLabelHelper.cs");
            StringAssert.Contains("HelpVideoRules.AuditUpload", helper);
            StringAssert.Contains("HelpVideoRules.AuditReplace", helper);
            StringAssert.Contains("HelpVideoRules.AuditDelete", helper);
        }

        private static string RootOf()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src", "AssetManagement.Web")))
            {
                directory = directory.Parent;
            }

            if (directory == null)
            {
                Assert.Inconclusive("Repository root not found.");
            }

            return directory.FullName;
        }
    }
}

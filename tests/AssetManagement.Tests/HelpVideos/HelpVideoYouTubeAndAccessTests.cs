using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Helpers;
using AssetManagement.Tests.Requisitions;
using AssetManagement.Web.Controllers;
using AssetManagement.Web.ViewModels;
using Moq;
using NUnit.Framework;

namespace AssetManagement.Tests.HelpVideos
{
    /// <summary>Unlisted YouTube videos (Allan, 2026-10-28: "On yt, only users with the link should be able to view").</summary>
    [TestFixture]
    public class HelpVideoYouTubeTests
    {
        private const string Id = "dQw4w9WgXcQ";

        [TestCase("dQw4w9WgXcQ")]
        [TestCase("  dQw4w9WgXcQ  ")]
        [TestCase("https://youtu.be/dQw4w9WgXcQ")]
        [TestCase("https://youtu.be/dQw4w9WgXcQ?si=AbCdEf123")]
        [TestCase("youtu.be/dQw4w9WgXcQ")]
        [TestCase("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
        [TestCase("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=10s")]
        [TestCase("https://youtube.com/watch?feature=share&v=dQw4w9WgXcQ")]
        [TestCase("https://m.youtube.com/watch?v=dQw4w9WgXcQ")]
        [TestCase("http://www.youtube.com/embed/dQw4w9WgXcQ")]
        [TestCase("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?rel=0")]
        [TestCase("https://youtube.com/shorts/dQw4w9WgXcQ")]
        [TestCase("https://www.youtube.com/live/dQw4w9WgXcQ")]
        public void ParseYouTubeId_AcceptsShareWatchAndEmbedLinks(string input)
        {
            Assert.AreEqual(Id, HelpVideoRules.ParseYouTubeId(input));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("dQw4w9WgXc")]
        [TestCase("dQw4w9WgXcQQ")]
        [TestCase("dQw4w9WgXc<")]
        [TestCase("https://evil.example/watch?v=dQw4w9WgXcQ")]
        [TestCase("https://youtube.com.evil.example/watch?v=dQw4w9WgXcQ")]
        [TestCase("https://notyoutu.be/dQw4w9WgXcQ")]
        [TestCase("https://www.youtube.com/playlist?list=PL0123456789")]
        [TestCase("https://www.youtube.com/watch?v=dQw4w9WgXc%22")]
        [TestCase("https://youtu.be/dQw4w9WgXcQ/extra")]
        [TestCase("ftp://youtu.be/dQw4w9WgXcQ")]
        [TestCase("javascript:alert(1)")]
        public void ParseYouTubeId_RejectsEverythingElse(string input)
        {
            Assert.IsNull(HelpVideoRules.ParseYouTubeId(input));
        }

        [Test]
        public void EmbedUrl_IsNoCookie_WithRel0()
        {
            Assert.AreEqual("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?rel=0", HelpVideoRules.YouTubeEmbedUrl(Id));
            Assert.IsNull(HelpVideoRules.YouTubeEmbedUrl("bad\"id<x>"));
            Assert.IsNull(HelpVideoRules.YouTubeEmbedUrl(null));
        }

        [Test]
        public void Catalog_CarriesYouTubeRows()
        {
            var model = HelpVideoCatalog.Build(new List<HelpVideoInfo>
            {
                new HelpVideoInfo { Id = 7, OrganizationId = null, TopicSlug = "hod-start-requisition", Title = "t", YouTubeVideoId = Id }
            }, false);
            var topic = model.Groups.SelectMany(g => g.Topics).Single(t => t.Id == "hod-start-requisition");
            Assert.IsTrue(topic.Video.IsYouTube);
            Assert.AreEqual(1, model.VideoCount);
        }

        [Test]
        public void View_EmbedsById_WithoutApiOrOEmbedCalls()
        {
            var view = Read("src", "AssetManagement.Web", "Views", "HelpVideos", "Index.cshtml");
            StringAssert.Contains("HelpVideoRules.YouTubeEmbedUrl(video.YouTubeVideoId)", view);
            StringAssert.Contains("referrerpolicy=\"strict-origin-when-cross-origin\"", view);
            StringAssert.Contains("TenantRouteUrl(Url, \"SetYouTube\", \"HelpVideos\")", view);
            var rules = Read("src", "AssetManagement.Application", "Helpers", "HelpVideoRules.cs");
            StringAssert.Contains("\"https://www.youtube-nocookie.com/embed/\"", rules);
            StringAssert.Contains("\"?rel=0\"", rules);

            foreach (var file in Directory.GetFiles(Path.Combine(Root(), "src"), "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".cs") || f.EndsWith(".cshtml") || f.EndsWith(".js")))
            {
                var text = File.ReadAllText(file);
                StringAssert.DoesNotContain("youtube.com/oembed", text, file);
                StringAssert.DoesNotContain("googleapis.com/youtube", text, file);
            }
        }

        [Test]
        public void OnlyTheHiddenPageMentionsYouTube_AndNoRobotsOrSitemapListsIt()
        {
            var web = Path.Combine(Root(), "src", "AssetManagement.Web");
            var offenders = Directory.GetFiles(Path.Combine(web, "Views"), "*.cshtml", SearchOption.AllDirectories)
                .Where(f => !f.Replace('/', '\\').Contains("\\Views\\HelpVideos\\"))
                .Where(f => File.ReadAllText(f).IndexOf("youtube", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            CollectionAssert.IsEmpty(offenders, "video ids only on the signed-in help page");

            foreach (var file in Directory.GetFiles(web, "robots.txt", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(web, "sitemap*.xml", SearchOption.AllDirectories)))
            {
                StringAssert.DoesNotContain("HelpVideos", File.ReadAllText(file), file);
            }
        }

        [Test]
        public void Store_OnlyReturnsTheTenantsOwnAndAllTenantRows()
        {
            var store = Read("src", "AssetManagement.Infrastructure", "Queries", "HelpVideoStore.cs");
            StringAssert.Contains("WHERE v.[OrganizationId] = @Org OR v.[OrganizationId] IS NULL", store);
            StringAssert.Contains("WHERE v.[Id] = @Id AND (v.[OrganizationId] = @Org OR v.[OrganizationId] IS NULL)", store);
            StringAssert.Contains("v.[YouTubeVideoId]", store);
        }

        internal static string Read(params string[] parts)
        {
            return QaSixRequisitionFormTests.Read(parts);
        }

        internal static string Root()
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

    /// <summary>Anonymous requests to the hidden page and every endpoint that returns video ids, URLs or JSON get a 302 to login.</summary>
    [TestFixture]
    public class HelpVideoAnonymousAccessTests
    {
        private const string Tenant = "A46138179";

        private static AuthorizationContext AnonymousContext(ControllerBase controller, Type controllerType, string controllerName, ActionDescriptor action)
        {
            var rawUrl = "/" + Tenant + "/" + controllerName + "/" + action.ActionName;
            var request = new Mock<HttpRequestBase>();
            request.SetupGet(r => r.RawUrl).Returns(rawUrl);
            request.SetupGet(r => r.Url).Returns(new Uri("http://localhost" + rawUrl));
            request.SetupGet(r => r.ApplicationPath).Returns("/");
            request.SetupGet(r => r.AppRelativeCurrentExecutionFilePath).Returns("~" + rawUrl);
            request.SetupGet(r => r.HttpMethod).Returns("GET");
            request.SetupGet(r => r.Headers).Returns(new System.Collections.Specialized.NameValueCollection());
            request.SetupGet(r => r.QueryString).Returns(new System.Collections.Specialized.NameValueCollection());
            var response = new Mock<HttpResponseBase>();
            response.Setup(r => r.ApplyAppPathModifier(It.IsAny<string>())).Returns<string>(s => s);
            var http = new Mock<HttpContextBase>();
            http.SetupGet(h => h.Request).Returns(request.Object);
            http.SetupGet(h => h.Response).Returns(response.Object);
            http.SetupGet(h => h.Items).Returns(new Hashtable());
            http.SetupGet(h => h.User).Returns(new GenericPrincipal(new GenericIdentity(string.Empty), new string[0]));

            var route = new RouteData();
            route.Values["tenant"] = Tenant;
            route.Values["controller"] = controllerName;
            route.Values["action"] = action.ActionName;
            var controllerContext = new ControllerContext(http.Object, route, controller);
            return new AuthorizationContext(controllerContext, action);
        }

        /// <summary>Runs the controller-level, then the action-level authorization filters, like MVC does.</summary>
        private static ActionResult Authorize(ControllerBase controller, string controllerName, ActionDescriptor action)
        {
            var context = AnonymousContext(controller, controller.GetType(), controllerName, action);
            var filters = action.ControllerDescriptor.GetCustomAttributes(typeof(FilterAttribute), true).OfType<IAuthorizationFilter>()
                .Concat(action.GetCustomAttributes(typeof(FilterAttribute), true).OfType<IAuthorizationFilter>());
            foreach (var filter in filters)
            {
                filter.OnAuthorization(context);
                if (context.Result != null)
                {
                    break;
                }
            }

            return context.Result;
        }

        [Test]
        public void EveryHelpVideosAction_RedirectsAnonymousUsersToLogin()
        {
            var controller = new HelpVideosController(null);
            var descriptor = new ReflectedControllerDescriptor(typeof(HelpVideosController));
            var actions = descriptor.GetCanonicalActions();
            var names = actions.Select(a => a.ActionName).ToList();
            foreach (var expected in new[] { "Index", "Stream", "UploadStart", "UploadChunk", "UploadComplete", "UploadCancel", "SetYouTube", "Delete" })
            {
                CollectionAssert.Contains(names, expected);
            }

            foreach (var action in actions)
            {
                var result = Authorize(controller, "HelpVideos", action) as RedirectResult;
                Assert.IsNotNull(result, action.ActionName + " must redirect anonymous users");
                Assert.That(result.Url, Is.StringStarting("/" + Tenant + "/Account/Login").IgnoreCase, action.ActionName);
                Assert.IsFalse(result.Permanent, action.ActionName + " 302, not 301");
            }
        }

        [Test]
        public void OldHomeHelpAddress_RedirectsAnonymousUsersToLogin_WithoutRevealingThePage()
        {
            var controller = (ControllerBase)Activator.CreateInstance(typeof(HomeController));
            var descriptor = new ReflectedControllerDescriptor(typeof(HomeController));
            var help = descriptor.GetCanonicalActions().Single(a => a.ActionName == "Help");
            var result = Authorize(controller, "Home", help) as RedirectResult;
            Assert.IsNotNull(result, "Home/Help needs sign-in");
            Assert.That(result.Url, Is.StringStarting("/" + Tenant + "/Account/Login").IgnoreCase);
            StringAssert.DoesNotContain("HelpVideos", result.Url);
        }
    }
}
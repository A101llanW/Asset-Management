using System.Collections.Specialized;
using System.Web;
using AssetManagement.Web.Helpers;
using Moq;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class LiveFilterRequestTests
    {
        private static HttpRequestBase RequestWithHeader(string name, string value)
        {
            var headers = new NameValueCollection();
            if (name != null)
            {
                headers[name] = value;
            }

            var request = new Mock<HttpRequestBase>();
            request.SetupGet(r => r.Headers).Returns(headers);
            return request.Object;
        }

        [Test]
        public void IsLiveFilter_False_WhenRequestNull()
        {
            Assert.IsFalse(LiveFilterRequest.IsLiveFilter(null));
        }

        [Test]
        public void IsLiveFilter_False_WhenHeaderMissing()
        {
            Assert.IsFalse(LiveFilterRequest.IsLiveFilter(RequestWithHeader(null, null)));
        }

        [Test]
        public void IsLiveFilter_True_WhenHeaderIsOne()
        {
            Assert.IsTrue(LiveFilterRequest.IsLiveFilter(
                RequestWithHeader(LiveFilterRequest.HeaderName, LiveFilterRequest.HeaderValue)));
        }

        [Test]
        public void IsLiveFilter_True_WhenHeaderIsTrueCaseInsensitive()
        {
            Assert.IsTrue(LiveFilterRequest.IsLiveFilter(
                RequestWithHeader(LiveFilterRequest.HeaderName, "TRUE")));
        }

        [Test]
        public void ResolveLayout_UsesLiveFilterLayout_WhenHeaderPresent()
        {
            var layout = LiveFilterRequest.ResolveLayout(
                RequestWithHeader(LiveFilterRequest.HeaderName, "1"),
                "~/Views/Shared/_Layout.cshtml",
                "~/Views/Shared/_LiveFilterLayout.cshtml");

            Assert.AreEqual("~/Views/Shared/_LiveFilterLayout.cshtml", layout);
        }

        [Test]
        public void ResolveLayout_UsesDefault_WhenHeaderAbsent()
        {
            var layout = LiveFilterRequest.ResolveLayout(
                RequestWithHeader(null, null),
                "~/Views/Shared/_Layout.cshtml",
                "~/Views/Shared/_LiveFilterLayout.cshtml");

            Assert.AreEqual("~/Views/Shared/_Layout.cshtml", layout);
        }
    }

    [TestFixture]
    public class LiveFilterQueryTests
    {
        [Test]
        public void WithPageReset_SetsPageToOne()
        {
            var source = HttpUtility.ParseQueryString("Search=desk&page=3&sort=tag");
            var result = LiveFilterQuery.WithPageReset(source, true);
            Assert.AreEqual("1", result["page"]);
            Assert.AreEqual("desk", result["Search"]);
            Assert.AreEqual("tag", result["sort"]);
        }

        [Test]
        public void WithPageReset_PreservesPage_WhenNotResetting()
        {
            var source = HttpUtility.ParseQueryString("Search=desk&page=3");
            var result = LiveFilterQuery.WithPageReset(source, false);
            Assert.AreEqual("3", result["page"]);
        }

        [Test]
        public void CopyQuery_DoesNotMutateSource()
        {
            var source = HttpUtility.ParseQueryString("page=2&view=grouped");
            var copy = LiveFilterQuery.WithPageReset(source, true);
            Assert.AreEqual("2", source["page"]);
            Assert.AreEqual("1", copy["page"]);
        }

        [Test]
        public void ToQueryString_EncodesKeysAndValues()
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["Search"] = "a b";
            query["page"] = "1";
            var qs = LiveFilterQuery.ToQueryString(query);
            Assert.IsTrue(qs.Contains("Search=a%20b") || qs.Contains("Search=a+b"));
            Assert.IsTrue(qs.Contains("page=1"));
        }
    }
}
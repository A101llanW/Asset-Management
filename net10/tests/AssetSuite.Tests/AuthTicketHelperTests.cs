using AssetSuite.Web.Infrastructure;
using NUnit.Framework;

namespace AssetSuite.Tests;

[TestFixture]
public class AuthTicketHelperTests
{
    [Test]
    public void BuildUserData_IncludesOrgAndAccessTokenSegments()
    {
        var data = AuthTicketHelper.BuildUserData("user-1", 42, "token-abc", "ua-hash", false);
        Assert.That(data, Is.EqualTo("user-1|42|token-abc|ua-hash"));
    }

    [Test]
    public void BuildUserData_AppendsPasswordChangeMarker()
    {
        var data = AuthTicketHelper.BuildUserData("user-1", null, "tok", "ua", true);
        Assert.That(data, Does.EndWith("|RequirePasswordChange"));
    }
}

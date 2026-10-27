using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace AssetSuite.IntegrationTests;

[TestFixture]
public class WebApplicationFactorySmokeTests
{
    [Test]
    public async Task LoginPage_ReturnsSuccess()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Account/Login");
        Assert.That(response.IsSuccessStatusCode, Is.True);
    }

    [Test]
    public async Task TenantLoginRoute_IsReachable()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/nanosoft/Account/Login");
        Assert.That(response.IsSuccessStatusCode, Is.True);
    }
}

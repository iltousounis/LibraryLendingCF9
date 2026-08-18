using System.Net;

namespace LendingLibrary.IntegrationTests;

public class RateLimitingTests(LendingLibraryWebApplicationFactory factory)
    : IClassFixture<LendingLibraryWebApplicationFactory>
{
    [Fact]
    public async Task Login_ExceedingPermitLimit_Returns429()
    {
        // The "auth" policy allows 10 requests/minute per client; TestServer reports a fixed
        // remote IP for every request, so these all land in the same rate-limit partition.
        var client = factory.CreateClient();

        for (var i = 0; i < 10; i++)
        {
            var response = await client.GetAsync("/Account/Login");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var eleventh = await client.GetAsync("/Account/Login");

        Assert.Equal((HttpStatusCode)429, eleventh.StatusCode);
    }

    [Fact]
    public async Task NonAuthPages_AreNotRateLimited()
    {
        var client = factory.CreateClient();

        for (var i = 0; i < 15; i++)
        {
            var response = await client.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}

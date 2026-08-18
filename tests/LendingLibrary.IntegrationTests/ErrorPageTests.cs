using System.Net;

namespace LendingLibrary.IntegrationTests;

public class ErrorPageTests(LendingLibraryWebApplicationFactory factory)
    : IClassFixture<LendingLibraryWebApplicationFactory>
{
    [Fact]
    public async Task UnknownRoute_Returns404_WithFriendlyPage()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/this-route-does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Page not found", html);
    }

    [Fact]
    public async Task UnknownCatalogueItem_Returns404_WithFriendlyPage()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/Catalogue/Details/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Page not found", html);
    }
}

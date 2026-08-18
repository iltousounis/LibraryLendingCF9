using System.Net;
using LendingLibrary.Web.Domain.Enums;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace LendingLibrary.IntegrationTests;

public class CatalogueBrowsingTests(LendingLibraryWebApplicationFactory factory)
    : IClassFixture<LendingLibraryWebApplicationFactory>
{
    [Fact]
    public async Task Index_SearchNarrowsResults_ToMatchingItem()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICatalogueService>();

        var uniqueTitle = $"Findable {Guid.NewGuid():N}";
        await service.CreateAsync(new CatalogueItemInput(
            uniqueTitle, ItemType.Book, "Some Author", null, null, null, null, null, 2));
        await service.CreateAsync(new CatalogueItemInput(
            $"Other {Guid.NewGuid():N}", ItemType.Dvd, null, null, null, null, null, null, 1));

        var client = factory.CreateClient();

        var unfiltered = await client.GetAsync("/Catalogue");
        unfiltered.EnsureSuccessStatusCode();

        var filtered = await client.GetAsync($"/Catalogue?q={Uri.EscapeDataString(uniqueTitle)}");
        filtered.EnsureSuccessStatusCode();
        var html = await filtered.Content.ReadAsStringAsync();

        Assert.Contains(uniqueTitle, html);
    }

    [Fact]
    public async Task Details_UnknownId_ReturnsNotFound()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/Catalogue/Details/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Details_ExistingItem_ShowsTitleAndAvailability()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICatalogueService>();

        var title = $"Detail Item {Guid.NewGuid():N}";
        var create = await service.CreateAsync(new CatalogueItemInput(
            title, ItemType.Book, null, null, null, null, null, null, 4));

        var client = factory.CreateClient();
        var response = await client.GetAsync($"/Catalogue/Details/{create.Id}");
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(title, html);
        Assert.Contains("4 of 4 available", html);
    }

    [Fact]
    public async Task Details_DeletedItem_ReturnsNotFound()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICatalogueService>();

        var create = await service.CreateAsync(new CatalogueItemInput(
            $"Soon Deleted {Guid.NewGuid():N}", ItemType.Book, null, null, null, null, null, null, 1));
        await service.DeleteAsync(create.Id!.Value);

        var client = factory.CreateClient();
        var response = await client.GetAsync($"/Catalogue/Details/{create.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

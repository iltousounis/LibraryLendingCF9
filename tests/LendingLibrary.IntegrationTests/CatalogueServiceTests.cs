using LendingLibrary.Web.Data;
using LendingLibrary.Web.Domain.Enums;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LendingLibrary.IntegrationTests;

public class CatalogueServiceTests(LendingLibraryWebApplicationFactory factory)
    : IClassFixture<LendingLibraryWebApplicationFactory>
{
    [Fact]
    public async Task CreateAsync_ThenSearch_FindsItem()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICatalogueService>();

        var title = $"Unique Title {Guid.NewGuid():N}";
        var result = await service.CreateAsync(new CatalogueItemInput(
            title, ItemType.Book, "Author A", "Publisher A", null, 2020, null, null, 3));

        Assert.True(result.Succeeded);

        var found = await service.SearchAsync(new CatalogueSearchQuery(SearchText: title), page: 1, pageSize: 10);

        Assert.Single(found.Items);
        Assert.Equal(title, found.Items[0].Title);
        Assert.Equal(3, found.Items[0].AvailableUnits);
    }

    [Fact]
    public async Task CreateAsync_DuplicateIsbn_ReturnsDuplicateIsbnOutcome()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICatalogueService>();
        var isbn = $"ISBN-{Guid.NewGuid():N}"[..20];

        var first = await service.CreateAsync(new CatalogueItemInput(
            "Book A", ItemType.Book, null, null, isbn, null, null, null, 1));
        Assert.True(first.Succeeded);

        var second = await service.CreateAsync(new CatalogueItemInput(
            "Book B", ItemType.Book, null, null, isbn, null, null, null, 1));

        Assert.False(second.Succeeded);
        Assert.Equal(CatalogueOperationOutcome.DuplicateIsbn, second.Outcome);
    }

    [Fact]
    public async Task UpdateAsync_PreservesUnitsOnLoan_WhenTotalUnitsIncrease()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICatalogueService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var create = await service.CreateAsync(new CatalogueItemInput(
            "Loan Test", ItemType.Book, null, null, null, null, null, null, 5));
        Assert.True(create.Succeeded);

        var entity = await db.CatalogueItems.SingleAsync(i => i.Id == create.Id);
        entity.AvailableUnits = 3; // simulate 2 units currently on loan
        await db.SaveChangesAsync();

        var updated = await service.UpdateAsync(
            create.Id!.Value,
            new CatalogueItemInput("Loan Test", ItemType.Book, null, null, null, null, null, null, 8),
            entity.RowVersion);

        Assert.True(updated.Succeeded);

        var reloaded = await service.GetByIdAsync(create.Id!.Value);
        Assert.Equal(8, reloaded!.TotalUnits);
        Assert.Equal(6, reloaded.AvailableUnits); // 2 still on loan out of the new total of 8
    }

    [Fact]
    public async Task UpdateAsync_BelowUnitsOnLoan_ReturnsValidationFailed()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICatalogueService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var create = await service.CreateAsync(new CatalogueItemInput(
            "Loan Test 2", ItemType.Book, null, null, null, null, null, null, 5));
        var entity = await db.CatalogueItems.SingleAsync(i => i.Id == create.Id);
        entity.AvailableUnits = 1; // simulate 4 units currently on loan
        await db.SaveChangesAsync();

        var updated = await service.UpdateAsync(
            create.Id!.Value,
            new CatalogueItemInput("Loan Test 2", ItemType.Book, null, null, null, null, null, null, 2),
            entity.RowVersion);

        Assert.False(updated.Succeeded);
        Assert.Equal(CatalogueOperationOutcome.ValidationFailed, updated.Outcome);
    }

    [Fact]
    public async Task UpdateAsync_StaleRowVersion_ReturnsConcurrencyConflict()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICatalogueService>();

        var create = await service.CreateAsync(new CatalogueItemInput(
            "Concurrency Test", ItemType.Book, null, null, null, null, null, null, 2));
        var item = await service.GetByIdAsync(create.Id!.Value);
        var staleRowVersion = item!.RowVersion;

        var firstUpdate = await service.UpdateAsync(
            create.Id!.Value,
            new CatalogueItemInput("Concurrency Test v2", ItemType.Book, null, null, null, null, null, null, 2),
            staleRowVersion);
        Assert.True(firstUpdate.Succeeded);

        var secondUpdate = await service.UpdateAsync(
            create.Id!.Value,
            new CatalogueItemInput("Concurrency Test v3", ItemType.Book, null, null, null, null, null, null, 2),
            staleRowVersion);

        Assert.False(secondUpdate.Succeeded);
        Assert.Equal(CatalogueOperationOutcome.ConcurrencyConflict, secondUpdate.Outcome);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes_ItemNoLongerFoundBySearchOrGetById()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICatalogueService>();

        var title = $"Delete Me {Guid.NewGuid():N}";
        var create = await service.CreateAsync(new CatalogueItemInput(
            title, ItemType.Book, null, null, null, null, null, null, 1));

        var delete = await service.DeleteAsync(create.Id!.Value);
        Assert.True(delete.Succeeded);

        var found = await service.SearchAsync(new CatalogueSearchQuery(SearchText: title), page: 1, pageSize: 10);
        Assert.Empty(found.Items);

        var byId = await service.GetByIdAsync(create.Id!.Value);
        Assert.Null(byId);
    }
}

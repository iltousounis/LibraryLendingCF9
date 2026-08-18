using LendingLibrary.Web.Data;
using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Domain.Enums;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LendingLibrary.IntegrationTests;

public class LendingServiceTests(LendingLibraryWebApplicationFactory factory)
    : IClassFixture<LendingLibraryWebApplicationFactory>
{
    private async Task<Guid> SeedItemAsync(int totalUnits)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICatalogueService>();
        var result = await service.CreateAsync(new CatalogueItemInput(
            $"Lending Test {Guid.NewGuid():N}", ItemType.Book, null, null, null, null, null, null, totalUnits));
        return result.Id!.Value;
    }

    private async Task<Guid> SeedUserAsync()
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var email = $"lending-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Lending Tester",
            CreatedAtUtc = timeProvider.GetUtcNow()
        };
        await userManager.CreateAsync(user, TestHelpers.DefaultPassword);
        await userManager.AddToRoleAsync(user, "User");
        return user.Id;
    }

    [Fact]
    public async Task BorrowAsync_Succeeds_DecrementsStockAndCreatesActiveLoan()
    {
        var itemId = await SeedItemAsync(totalUnits: 2);
        var userId = await SeedUserAsync();

        using var scope = factory.Services.CreateScope();
        var lending = scope.ServiceProvider.GetRequiredService<ILendingService>();
        var catalogue = scope.ServiceProvider.GetRequiredService<ICatalogueService>();

        var result = await lending.BorrowAsync(userId, itemId);

        Assert.True(result.Succeeded);
        var item = await catalogue.GetByIdAsync(itemId);
        Assert.Equal(1, item!.AvailableUnits);

        var current = await lending.GetUserLoansAsync(userId, LoanView.Current, 1, 10);
        Assert.Single(current.Items);
        Assert.Equal(itemId, current.Items[0].CatalogueItemId);
    }

    [Fact]
    public async Task BorrowAsync_OutOfStock_ReturnsOutOfStock()
    {
        var itemId = await SeedItemAsync(totalUnits: 0);
        var userId = await SeedUserAsync();

        using var scope = factory.Services.CreateScope();
        var lending = scope.ServiceProvider.GetRequiredService<ILendingService>();

        var result = await lending.BorrowAsync(userId, itemId);

        Assert.False(result.Succeeded);
        Assert.Equal(LendingOperationOutcome.OutOfStock, result.Outcome);
    }

    [Fact]
    public async Task BorrowAsync_AtLoanCap_ReturnsLoanLimitReached()
    {
        var userId = await SeedUserAsync();

        using var scope = factory.Services.CreateScope();
        var lending = scope.ServiceProvider.GetRequiredService<ILendingService>();

        for (var i = 0; i < 3; i++)
        {
            var itemId = await SeedItemAsync(totalUnits: 1);
            var r = await lending.BorrowAsync(userId, itemId);
            Assert.True(r.Succeeded);
        }

        var extraItemId = await SeedItemAsync(totalUnits: 1);
        var blocked = await lending.BorrowAsync(userId, extraItemId);

        Assert.False(blocked.Succeeded);
        Assert.Equal(LendingOperationOutcome.LoanLimitReached, blocked.Outcome);
    }

    [Fact]
    public async Task BorrowAsync_ConcurrentRequestsForLastUnit_OnlyOneSucceeds()
    {
        var itemId = await SeedItemAsync(totalUnits: 1);
        var userA = await SeedUserAsync();
        var userB = await SeedUserAsync();

        // Each concurrent call gets its own DI scope/DbContext — DbContext is not thread-safe.
        async Task<LendingOperationResult> BorrowInNewScopeAsync(Guid userId)
        {
            using var scope = factory.Services.CreateScope();
            var lending = scope.ServiceProvider.GetRequiredService<ILendingService>();
            return await lending.BorrowAsync(userId, itemId);
        }

        var results = await Task.WhenAll(BorrowInNewScopeAsync(userA), BorrowInNewScopeAsync(userB));

        Assert.Single(results, r => r.Succeeded);
        Assert.Single(results, r => r.Outcome == LendingOperationOutcome.OutOfStock);

        using var verifyScope = factory.Services.CreateScope();
        var catalogue = verifyScope.ServiceProvider.GetRequiredService<ICatalogueService>();
        var item = await catalogue.GetByIdAsync(itemId);
        Assert.Equal(0, item!.AvailableUnits);
    }

    [Fact]
    public async Task ReturnAsync_IncrementsStockAndSetsReturned()
    {
        var itemId = await SeedItemAsync(totalUnits: 1);
        var userId = await SeedUserAsync();

        using var scope = factory.Services.CreateScope();
        var lending = scope.ServiceProvider.GetRequiredService<ILendingService>();
        var catalogue = scope.ServiceProvider.GetRequiredService<ICatalogueService>();

        var borrow = await lending.BorrowAsync(userId, itemId);
        var afterBorrow = await catalogue.GetByIdAsync(itemId);
        Assert.Equal(0, afterBorrow!.AvailableUnits);

        var returnResult = await lending.ReturnAsync(borrow.LoanId!.Value);
        Assert.True(returnResult.Succeeded);

        var afterReturn = await catalogue.GetByIdAsync(itemId);
        Assert.Equal(1, afterReturn!.AvailableUnits);

        var history = await lending.GetUserLoansAsync(userId, LoanView.History, 1, 10);
        Assert.Single(history.Items);
        Assert.NotNull(history.Items[0].ReturnedAtUtc);
    }

    [Fact]
    public async Task ReturnAsync_AlreadyReturned_ReturnsAlreadyReturned()
    {
        var itemId = await SeedItemAsync(totalUnits: 1);
        var userId = await SeedUserAsync();

        using var scope = factory.Services.CreateScope();
        var lending = scope.ServiceProvider.GetRequiredService<ILendingService>();

        var borrow = await lending.BorrowAsync(userId, itemId);
        await lending.ReturnAsync(borrow.LoanId!.Value);

        var secondReturn = await lending.ReturnAsync(borrow.LoanId!.Value);

        Assert.False(secondReturn.Succeeded);
        Assert.Equal(LendingOperationOutcome.AlreadyReturned, secondReturn.Outcome);
    }

    [Fact]
    public async Task GetUserLoansAsync_Overdue_OnlyReturnsPastDueActiveLoans()
    {
        var itemId = await SeedItemAsync(totalUnits: 2);
        var userId = await SeedUserAsync();

        using var scope = factory.Services.CreateScope();
        var lending = scope.ServiceProvider.GetRequiredService<ILendingService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var borrow = await lending.BorrowAsync(userId, itemId);

        var loan = await db.Loans.SingleAsync(l => l.Id == borrow.LoanId);
        loan.DueAtUtc = DateTimeOffset.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        var overdue = await lending.GetUserLoansAsync(userId, LoanView.Overdue, 1, 10);
        var current = await lending.GetUserLoansAsync(userId, LoanView.Current, 1, 10);

        Assert.Single(overdue.Items);
        Assert.Equal(loan.Id, overdue.Items[0].Id);
        Assert.Single(current.Items);
    }
}

using LendingLibrary.Web.Data;
using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Domain.Enums;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LendingLibrary.IntegrationTests;

public class ReservationServiceTests(LendingLibraryWebApplicationFactory factory)
    : IClassFixture<LendingLibraryWebApplicationFactory>
{
    private async Task<Guid> SeedItemAsync(int totalUnits)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICatalogueService>();
        var result = await service.CreateAsync(new CatalogueItemInput(
            $"Reservation Test {Guid.NewGuid():N}", ItemType.Book, null, null, null, null, null, null, totalUnits));
        return result.Id!.Value;
    }

    private async Task<Guid> SeedUserAsync()
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var email = $"reserver-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Reservation Tester",
            CreatedAtUtc = timeProvider.GetUtcNow()
        };
        await userManager.CreateAsync(user, TestHelpers.DefaultPassword);
        await userManager.AddToRoleAsync(user, "User");
        return user.Id;
    }

    [Fact]
    public async Task ReserveAsync_Succeeds_DecrementsStockAndCreatesPending()
    {
        var itemId = await SeedItemAsync(totalUnits: 1);
        var userId = await SeedUserAsync();

        using var scope = factory.Services.CreateScope();
        var reservations = scope.ServiceProvider.GetRequiredService<IReservationService>();
        var catalogue = scope.ServiceProvider.GetRequiredService<ICatalogueService>();

        var result = await reservations.ReserveAsync(userId, itemId);

        Assert.True(result.Succeeded);
        var item = await catalogue.GetByIdAsync(itemId);
        Assert.Equal(0, item!.AvailableUnits);

        var pending = await reservations.GetUserReservationsAsync(userId, 1, 10);
        Assert.Single(pending.Items);
        Assert.Equal(ReservationStatus.Pending, pending.Items[0].Status);
    }

    [Fact]
    public async Task ReserveAsync_OutOfStock_ReturnsOutOfStock()
    {
        var itemId = await SeedItemAsync(totalUnits: 0);
        var userId = await SeedUserAsync();

        using var scope = factory.Services.CreateScope();
        var reservations = scope.ServiceProvider.GetRequiredService<IReservationService>();

        var result = await reservations.ReserveAsync(userId, itemId);

        Assert.False(result.Succeeded);
        Assert.Equal(ReservationOperationOutcome.OutOfStock, result.Outcome);
    }

    [Fact]
    public async Task ReserveAsync_ConcurrentRequestsForLastUnit_OnlyOneSucceeds()
    {
        var itemId = await SeedItemAsync(totalUnits: 1);
        var userA = await SeedUserAsync();
        var userB = await SeedUserAsync();

        async Task<ReservationOperationResult> ReserveInNewScopeAsync(Guid userId)
        {
            using var scope = factory.Services.CreateScope();
            var reservations = scope.ServiceProvider.GetRequiredService<IReservationService>();
            return await reservations.ReserveAsync(userId, itemId);
        }

        var results = await Task.WhenAll(ReserveInNewScopeAsync(userA), ReserveInNewScopeAsync(userB));

        Assert.Single(results, r => r.Succeeded);
        Assert.Single(results, r => r.Outcome == ReservationOperationOutcome.OutOfStock);

        using var verifyScope = factory.Services.CreateScope();
        var catalogue = verifyScope.ServiceProvider.GetRequiredService<ICatalogueService>();
        var item = await catalogue.GetByIdAsync(itemId);
        Assert.Equal(0, item!.AvailableUnits);
    }

    [Fact]
    public async Task CancelAsync_Owner_Succeeds_ReleasesStock()
    {
        var itemId = await SeedItemAsync(totalUnits: 1);
        var userId = await SeedUserAsync();

        using var scope = factory.Services.CreateScope();
        var reservations = scope.ServiceProvider.GetRequiredService<IReservationService>();
        var catalogue = scope.ServiceProvider.GetRequiredService<ICatalogueService>();

        var reserve = await reservations.ReserveAsync(userId, itemId);
        var cancel = await reservations.CancelAsync(reserve.ReservationId!.Value, userId);

        Assert.True(cancel.Succeeded);
        var item = await catalogue.GetByIdAsync(itemId);
        Assert.Equal(1, item!.AvailableUnits);
    }

    [Fact]
    public async Task CancelAsync_NotOwner_ReturnsNotOwner()
    {
        var itemId = await SeedItemAsync(totalUnits: 1);
        var owner = await SeedUserAsync();
        var stranger = await SeedUserAsync();

        using var scope = factory.Services.CreateScope();
        var reservations = scope.ServiceProvider.GetRequiredService<IReservationService>();

        var reserve = await reservations.ReserveAsync(owner, itemId);
        var cancel = await reservations.CancelAsync(reserve.ReservationId!.Value, stranger);

        Assert.False(cancel.Succeeded);
        Assert.Equal(ReservationOperationOutcome.NotOwner, cancel.Outcome);
    }

    [Fact]
    public async Task FulfilAsync_Succeeds_CreatesLoan_DoesNotDoubleDecrementStock()
    {
        var itemId = await SeedItemAsync(totalUnits: 1);
        var userId = await SeedUserAsync();

        using var scope = factory.Services.CreateScope();
        var reservations = scope.ServiceProvider.GetRequiredService<IReservationService>();
        var lending = scope.ServiceProvider.GetRequiredService<ILendingService>();
        var catalogue = scope.ServiceProvider.GetRequiredService<ICatalogueService>();

        var reserve = await reservations.ReserveAsync(userId, itemId);
        var afterReserve = await catalogue.GetByIdAsync(itemId);
        Assert.Equal(0, afterReserve!.AvailableUnits);

        var fulfil = await reservations.FulfilAsync(reserve.ReservationId!.Value);

        Assert.True(fulfil.Succeeded);
        Assert.NotNull(fulfil.LoanId);

        var afterFulfil = await catalogue.GetByIdAsync(itemId);
        Assert.Equal(0, afterFulfil!.AvailableUnits); // still 0 — the loan reuses the existing hold

        var current = await lending.GetUserLoansAsync(userId, LoanView.Current, 1, 10);
        Assert.Single(current.Items);
        Assert.Equal(fulfil.LoanId, current.Items[0].Id);
    }

    [Fact]
    public async Task FulfilAsync_AtLoanCap_ReturnsLoanLimitReached()
    {
        var userId = await SeedUserAsync();

        using var scope = factory.Services.CreateScope();
        var lending = scope.ServiceProvider.GetRequiredService<ILendingService>();
        var reservations = scope.ServiceProvider.GetRequiredService<IReservationService>();

        for (var i = 0; i < 3; i++)
        {
            var itemId = await SeedItemAsync(totalUnits: 1);
            var borrow = await lending.BorrowAsync(userId, itemId);
            Assert.True(borrow.Succeeded);
        }

        var reservedItemId = await SeedItemAsync(totalUnits: 1);
        var reserve = await reservations.ReserveAsync(userId, reservedItemId);
        Assert.True(reserve.Succeeded);

        var fulfil = await reservations.FulfilAsync(reserve.ReservationId!.Value);

        Assert.False(fulfil.Succeeded);
        Assert.Equal(ReservationOperationOutcome.LoanLimitReached, fulfil.Outcome);
    }

    [Fact]
    public async Task ExpireStaleReservationsAsync_ExpiresPastDue_ReleasesStock()
    {
        var itemId = await SeedItemAsync(totalUnits: 1);
        var userId = await SeedUserAsync();

        using var scope = factory.Services.CreateScope();
        var reservations = scope.ServiceProvider.GetRequiredService<IReservationService>();
        var catalogue = scope.ServiceProvider.GetRequiredService<ICatalogueService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var reserve = await reservations.ReserveAsync(userId, itemId);

        var reservation = await db.Reservations.SingleAsync(r => r.Id == reserve.ReservationId);
        reservation.ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        var expiredCount = await reservations.ExpireStaleReservationsAsync();

        Assert.True(expiredCount >= 1);
        var reloaded = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reserve.ReservationId);
        Assert.Equal(ReservationStatus.Expired, reloaded.Status);

        var item = await catalogue.GetByIdAsync(itemId);
        Assert.Equal(1, item!.AvailableUnits);
    }
}

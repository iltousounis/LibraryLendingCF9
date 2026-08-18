using LendingLibrary.Web.Data;
using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Domain.Enums;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LendingLibrary.IntegrationTests;

public class ReservationFlowTests(LendingLibraryWebApplicationFactory factory)
    : IClassFixture<LendingLibraryWebApplicationFactory>
{
    private async Task<Guid> SeedItemAsync(int totalUnits)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICatalogueService>();
        var result = await service.CreateAsync(new CatalogueItemInput(
            $"Reservation Flow Item {Guid.NewGuid():N}", ItemType.Book, null, null, null, null, null, null, totalUnits));
        return result.Id!.Value;
    }

    private async Task<HttpClient> CreateLoggedInUserClientAsync()
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
            DisplayName = "Reserver",
            CreatedAtUtc = timeProvider.GetUtcNow()
        };
        await userManager.CreateAsync(user, TestHelpers.DefaultPassword);
        await userManager.AddToRoleAsync(user, "User");

        var client = factory.CreateClient();
        await TestHelpers.LogInAsync(client, email);
        return client;
    }

    [Fact]
    public async Task Reserve_WhenInStock_HoldsUnit_ShowsInMyReservations_ThenCancelReleasesStock()
    {
        // Reserve holds a unit at the desk instead of taking it home — it requires stock,
        // same as Borrow; it isn't a wishlist for out-of-stock items (see Details_WhenOutOfStock).
        var itemId = await SeedItemAsync(totalUnits: 1);
        var client = await CreateLoggedInUserClientAsync();

        var detailsPage = await client.GetAsync($"/Catalogue/Details/{itemId}");
        var token = await TestHelpers.ExtractAntiforgeryTokenAsync(detailsPage);

        var reserveResponse = await client.PostAsync(
            $"/Catalogue/Details/{itemId}?handler=Reserve",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        reserveResponse.EnsureSuccessStatusCode();
        var reserveHtml = await reserveResponse.Content.ReadAsStringAsync();
        Assert.Contains("Reserved for pickup!", reserveHtml);

        var myReservations = await client.GetAsync("/Reservations/Index");
        myReservations.EnsureSuccessStatusCode();
        var myReservationsHtml = await myReservations.Content.ReadAsStringAsync();
        Assert.Contains("Reservation Flow Item", myReservationsHtml);

        var cancelToken = await TestHelpers.ExtractAntiforgeryTokenAsync(myReservations);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reservation = await db.Reservations.AsNoTracking().SingleAsync(r => r.CatalogueItemId == itemId);

        var cancelResponse = await client.PostAsync(
            "/Reservations/Index?handler=Cancel",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["reservationId"] = reservation.Id.ToString(),
                ["__RequestVerificationToken"] = cancelToken
            }));
        cancelResponse.EnsureSuccessStatusCode();
        var cancelHtml = await cancelResponse.Content.ReadAsStringAsync();
        Assert.Contains("Reservation cancelled", cancelHtml);

        var catalogue = scope.ServiceProvider.GetRequiredService<ICatalogueService>();
        var item = await catalogue.GetByIdAsync(itemId);
        Assert.Equal(1, item!.AvailableUnits);

        var reloaded = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservation.Id);
        Assert.Equal(ReservationStatus.Cancelled, reloaded.Status);
    }

    [Fact]
    public async Task Details_WhenInStock_ShowsBothBorrowAndReserve()
    {
        var itemId = await SeedItemAsync(totalUnits: 1);
        var client = await CreateLoggedInUserClientAsync();

        var response = await client.GetAsync($"/Catalogue/Details/{itemId}");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Borrow", html);
        Assert.Contains("Reserve for pickup", html);
    }

    [Fact]
    public async Task Details_WhenOutOfStock_ShowsNeitherBorrowNorReserve()
    {
        var itemId = await SeedItemAsync(totalUnits: 0);
        var client = await CreateLoggedInUserClientAsync();

        var response = await client.GetAsync($"/Catalogue/Details/{itemId}");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Reserve for pickup", html);
        Assert.DoesNotContain(">Borrow<", html);
    }
}

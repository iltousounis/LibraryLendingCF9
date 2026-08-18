using LendingLibrary.Web.Data;
using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Domain.Enums;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LendingLibrary.IntegrationTests;

public class AdminReservationsTests(LendingLibraryWebApplicationFactory factory)
    : IClassFixture<LendingLibraryWebApplicationFactory>
{
    private async Task<ApplicationUser> SeedUserInRoleAsync(string role, string label)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var email = $"{label}-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = label,
            CreatedAtUtc = timeProvider.GetUtcNow()
        };
        await userManager.CreateAsync(user, TestHelpers.DefaultPassword);
        await userManager.AddToRoleAsync(user, role);
        return user;
    }

    [Fact]
    public async Task Fulfil_ByAdmin_CreatesLoan_RemovesFromPending()
    {
        using var seedScope = factory.Services.CreateScope();
        var catalogue = seedScope.ServiceProvider.GetRequiredService<ICatalogueService>();
        var reservationService = seedScope.ServiceProvider.GetRequiredService<IReservationService>();

        var itemResult = await catalogue.CreateAsync(new CatalogueItemInput(
            $"Admin Fulfil Item {Guid.NewGuid():N}", ItemType.Book, null, null, null, null, null, null, 1));
        var itemId = itemResult.Id!.Value;

        var borrower = await SeedUserInRoleAsync("User", "borrower");
        var reserve = await reservationService.ReserveAsync(borrower.Id, itemId);
        Assert.True(reserve.Succeeded);

        var admin = await SeedUserInRoleAsync("Admin", "admin");
        var client = factory.CreateClient();
        await TestHelpers.LogInAsync(client, admin.Email!);

        var indexPage = await client.GetAsync("/Admin/Reservations");
        indexPage.EnsureSuccessStatusCode();
        var indexHtml = await indexPage.Content.ReadAsStringAsync();
        Assert.Contains("Admin Fulfil Item", indexHtml);

        var token = await TestHelpers.ExtractAntiforgeryTokenAsync(indexPage);
        var form = new Dictionary<string, string>
        {
            ["reservationId"] = reserve.ReservationId!.Value.ToString(),
            ["__RequestVerificationToken"] = token
        };

        var fulfilResponse = await client.PostAsync("/Admin/Reservations?handler=Fulfil", new FormUrlEncodedContent(form));
        fulfilResponse.EnsureSuccessStatusCode();
        var fulfilHtml = await fulfilResponse.Content.ReadAsStringAsync();
        Assert.Contains("Reservation fulfilled", fulfilHtml);
        Assert.DoesNotContain("Admin Fulfil Item", fulfilHtml);

        using var verifyScope = factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reservation = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reserve.ReservationId);
        Assert.Equal(ReservationStatus.Fulfilled, reservation.Status);

        var loan = await db.Loans.AsNoTracking().SingleAsync(l => l.UserId == borrower.Id && l.CatalogueItemId == itemId);
        Assert.Equal(LoanStatus.Active, loan.Status);
    }

    [Fact]
    public async Task AdminCancel_ReleasesStock_RemovesFromPending()
    {
        using var seedScope = factory.Services.CreateScope();
        var catalogue = seedScope.ServiceProvider.GetRequiredService<ICatalogueService>();
        var reservationService = seedScope.ServiceProvider.GetRequiredService<IReservationService>();

        var itemResult = await catalogue.CreateAsync(new CatalogueItemInput(
            $"Admin Cancel Item {Guid.NewGuid():N}", ItemType.Book, null, null, null, null, null, null, 1));
        var itemId = itemResult.Id!.Value;

        var borrower = await SeedUserInRoleAsync("User", "noshow");
        var reserve = await reservationService.ReserveAsync(borrower.Id, itemId);
        Assert.True(reserve.Succeeded);

        var admin = await SeedUserInRoleAsync("Admin", "admin2");
        var client = factory.CreateClient();
        await TestHelpers.LogInAsync(client, admin.Email!);

        var indexPage = await client.GetAsync("/Admin/Reservations");
        var token = await TestHelpers.ExtractAntiforgeryTokenAsync(indexPage);
        var form = new Dictionary<string, string>
        {
            ["reservationId"] = reserve.ReservationId!.Value.ToString(),
            ["__RequestVerificationToken"] = token
        };

        var cancelResponse = await client.PostAsync("/Admin/Reservations?handler=Cancel", new FormUrlEncodedContent(form));
        cancelResponse.EnsureSuccessStatusCode();

        var item = await catalogue.GetByIdAsync(itemId);
        Assert.Equal(1, item!.AvailableUnits);
    }

    [Fact]
    public async Task NonAdmin_CannotReachAdminReservations()
    {
        var user = await SeedUserInRoleAsync("User", "regular");
        var client = factory.CreateClient();
        await TestHelpers.LogInAsync(client, user.Email!);

        var response = await client.GetAsync("/Admin/Reservations");

        Assert.Equal("/Account/AccessDenied", response.RequestMessage!.RequestUri!.AbsolutePath);
    }
}

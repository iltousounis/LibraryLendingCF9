using LendingLibrary.Web.Data;
using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Domain.Enums;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LendingLibrary.IntegrationTests;

public class BorrowFlowTests(LendingLibraryWebApplicationFactory factory)
    : IClassFixture<LendingLibraryWebApplicationFactory>
{
    private async Task<Guid> SeedItemAsync(int totalUnits)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICatalogueService>();
        var result = await service.CreateAsync(new CatalogueItemInput(
            $"Borrow Flow Item {Guid.NewGuid():N}", ItemType.Book, null, null, null, null, null, null, totalUnits));
        return result.Id!.Value;
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateLoggedInUserClientAsync()
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var email = $"borrower-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Borrower",
            CreatedAtUtc = timeProvider.GetUtcNow()
        };
        await userManager.CreateAsync(user, TestHelpers.DefaultPassword);
        await userManager.AddToRoleAsync(user, "User");

        var client = factory.CreateClient();
        await TestHelpers.LogInAsync(client, email);
        return (client, user.Id);
    }

    [Fact]
    public async Task Borrow_Anonymous_RedirectsToLogin()
    {
        var itemId = await SeedItemAsync(totalUnits: 1);
        var client = factory.CreateClient();

        // The Details page renders no Borrow form (and so no antiforgery token) for anonymous
        // visitors, so grab a valid token from any other anonymous page — antiforgery
        // cookie/token pairing is site-wide, not tied to a specific page.
        var loginPage = await client.GetAsync("/Account/Login");
        var token = await TestHelpers.ExtractAntiforgeryTokenAsync(loginPage);

        var response = await client.PostAsync(
            $"/Catalogue/Details/{itemId}?handler=Borrow",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

        Assert.Equal("/Account/Login", response.RequestMessage!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Borrow_Authenticated_DecrementsStockAndShowsInCurrentLoans()
    {
        var itemId = await SeedItemAsync(totalUnits: 1);
        var (client, userId) = await CreateLoggedInUserClientAsync();

        var detailsPage = await client.GetAsync($"/Catalogue/Details/{itemId}");
        var token = await TestHelpers.ExtractAntiforgeryTokenAsync(detailsPage);

        var response = await client.PostAsync(
            $"/Catalogue/Details/{itemId}?handler=Borrow",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Borrowed!", html);
        Assert.Contains("Out of stock", html);

        var currentLoans = await client.GetAsync("/Loans/Current");
        currentLoans.EnsureSuccessStatusCode();
        var loansHtml = await currentLoans.Content.ReadAsStringAsync();
        Assert.Contains("Borrow Flow Item", loansHtml);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var loan = await db.Loans.SingleAsync(l => l.UserId == userId && l.CatalogueItemId == itemId);
        Assert.Equal(LoanStatus.Active, loan.Status);
    }

    [Fact]
    public async Task Borrow_OutOfStock_ShowsErrorMessage()
    {
        var itemId = await SeedItemAsync(totalUnits: 0);
        var (client, _) = await CreateLoggedInUserClientAsync();

        var detailsPage = await client.GetAsync($"/Catalogue/Details/{itemId}");
        var token = await TestHelpers.ExtractAntiforgeryTokenAsync(detailsPage);

        var response = await client.PostAsync(
            $"/Catalogue/Details/{itemId}?handler=Borrow",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("out of stock", html, StringComparison.OrdinalIgnoreCase);
    }
}

using LendingLibrary.Web.Data;
using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Domain.Enums;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LendingLibrary.IntegrationTests;

public class AdminLoansTests(LendingLibraryWebApplicationFactory factory)
    : IClassFixture<LendingLibraryWebApplicationFactory>
{
    [Fact]
    public async Task MarkReturned_ByAdmin_UpdatesLoanAndReleasesStock()
    {
        using var seedScope = factory.Services.CreateScope();
        var catalogue = seedScope.ServiceProvider.GetRequiredService<ICatalogueService>();
        var lending = seedScope.ServiceProvider.GetRequiredService<ILendingService>();
        var userManager = seedScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var timeProvider = seedScope.ServiceProvider.GetRequiredService<TimeProvider>();

        var itemResult = await catalogue.CreateAsync(new CatalogueItemInput(
            $"Admin Return Item {Guid.NewGuid():N}", ItemType.Book, null, null, null, null, null, null, 1));
        var itemId = itemResult.Id!.Value;

        var borrowerEmail = $"borrower-{Guid.NewGuid():N}@example.com";
        var borrower = new ApplicationUser
        {
            UserName = borrowerEmail,
            Email = borrowerEmail,
            EmailConfirmed = true,
            DisplayName = "Borrower",
            CreatedAtUtc = timeProvider.GetUtcNow()
        };
        await userManager.CreateAsync(borrower, TestHelpers.DefaultPassword);
        await userManager.AddToRoleAsync(borrower, "User");

        var borrowResult = await lending.BorrowAsync(borrower.Id, itemId);
        Assert.True(borrowResult.Succeeded);

        var adminEmail = $"admin-{Guid.NewGuid():N}@example.com";
        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            DisplayName = "Test Admin",
            CreatedAtUtc = timeProvider.GetUtcNow()
        };
        await userManager.CreateAsync(admin, TestHelpers.DefaultPassword);
        await userManager.AddToRoleAsync(admin, "Admin");

        var client = factory.CreateClient();
        await TestHelpers.LogInAsync(client, adminEmail);

        var indexPage = await client.GetAsync("/Admin/Loans");
        indexPage.EnsureSuccessStatusCode();
        var indexHtml = await indexPage.Content.ReadAsStringAsync();
        Assert.Contains("Admin Return Item", indexHtml);

        var token = await TestHelpers.ExtractAntiforgeryTokenAsync(indexPage);
        var form = new Dictionary<string, string>
        {
            ["loanId"] = borrowResult.LoanId!.Value.ToString(),
            ["__RequestVerificationToken"] = token
        };

        var returnResponse = await client.PostAsync("/Admin/Loans?handler=Return", new FormUrlEncodedContent(form));
        returnResponse.EnsureSuccessStatusCode();
        var returnHtml = await returnResponse.Content.ReadAsStringAsync();
        Assert.Contains("Marked as returned", returnHtml);

        using var verifyScope = factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var loan = await db.Loans.SingleAsync(l => l.Id == borrowResult.LoanId);
        Assert.Equal(LoanStatus.Returned, loan.Status);
        Assert.NotNull(loan.ReturnedAtUtc);

        var item = await catalogue.GetByIdAsync(itemId);
        Assert.Equal(1, item!.AvailableUnits);
    }

    [Fact]
    public async Task NonAdmin_CannotReachAdminLoans()
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var email = $"regular-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Regular User",
            CreatedAtUtc = timeProvider.GetUtcNow()
        };
        await userManager.CreateAsync(user, TestHelpers.DefaultPassword);
        await userManager.AddToRoleAsync(user, "User");

        var client = factory.CreateClient();
        await TestHelpers.LogInAsync(client, email);

        var response = await client.GetAsync("/Admin/Loans");

        Assert.Equal("/Account/AccessDenied", response.RequestMessage!.RequestUri!.AbsolutePath);
    }
}

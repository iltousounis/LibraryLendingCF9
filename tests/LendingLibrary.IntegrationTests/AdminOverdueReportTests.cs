using LendingLibrary.Web.Data;
using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Domain.Enums;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LendingLibrary.IntegrationTests;

public class AdminOverdueReportTests(LendingLibraryWebApplicationFactory factory)
    : IClassFixture<LendingLibraryWebApplicationFactory>
{
    [Fact]
    public async Task OverdueReport_ShowsOnlyOverdueLoans_AndMarkReturnedWorks()
    {
        using var seedScope = factory.Services.CreateScope();
        var catalogue = seedScope.ServiceProvider.GetRequiredService<ICatalogueService>();
        var lending = seedScope.ServiceProvider.GetRequiredService<ILendingService>();
        var userManager = seedScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var timeProvider = seedScope.ServiceProvider.GetRequiredService<TimeProvider>();
        var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var overdueItem = await catalogue.CreateAsync(new CatalogueItemInput(
            $"Overdue Item {Guid.NewGuid():N}", ItemType.Book, null, null, null, null, null, null, 1));
        var onTimeItem = await catalogue.CreateAsync(new CatalogueItemInput(
            $"OnTime Item {Guid.NewGuid():N}", ItemType.Book, null, null, null, null, null, null, 1));

        var borrowerEmail = $"overdue-borrower-{Guid.NewGuid():N}@example.com";
        var borrower = new ApplicationUser
        {
            UserName = borrowerEmail,
            Email = borrowerEmail,
            EmailConfirmed = true,
            DisplayName = "Overdue Borrower",
            CreatedAtUtc = timeProvider.GetUtcNow()
        };
        await userManager.CreateAsync(borrower, TestHelpers.DefaultPassword);
        await userManager.AddToRoleAsync(borrower, "User");

        var overdueLoan = await lending.BorrowAsync(borrower.Id, overdueItem.Id!.Value);
        var onTimeLoan = await lending.BorrowAsync(borrower.Id, onTimeItem.Id!.Value);
        Assert.True(overdueLoan.Succeeded);
        Assert.True(onTimeLoan.Succeeded);

        var loanToMakeOverdue = await db.Loans.SingleAsync(l => l.Id == overdueLoan.LoanId);
        loanToMakeOverdue.DueAtUtc = DateTimeOffset.UtcNow.AddDays(-2);
        await db.SaveChangesAsync();

        var adminEmail = $"admin-{Guid.NewGuid():N}@example.com";
        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            DisplayName = "Report Admin",
            CreatedAtUtc = timeProvider.GetUtcNow()
        };
        await userManager.CreateAsync(admin, TestHelpers.DefaultPassword);
        await userManager.AddToRoleAsync(admin, "Admin");

        var client = factory.CreateClient();
        await TestHelpers.LogInAsync(client, adminEmail);

        var reportPage = await client.GetAsync("/Admin/Loans/Overdue");
        reportPage.EnsureSuccessStatusCode();
        var reportHtml = await reportPage.Content.ReadAsStringAsync();

        Assert.Contains("Overdue Item", reportHtml);
        Assert.DoesNotContain("OnTime Item", reportHtml);

        var token = await TestHelpers.ExtractAntiforgeryTokenAsync(reportPage);
        var form = new Dictionary<string, string>
        {
            ["loanId"] = overdueLoan.LoanId!.Value.ToString(),
            ["__RequestVerificationToken"] = token
        };
        var returnResponse = await client.PostAsync("/Admin/Loans/Overdue?handler=Return", new FormUrlEncodedContent(form));
        returnResponse.EnsureSuccessStatusCode();
        var returnHtml = await returnResponse.Content.ReadAsStringAsync();

        Assert.Contains("Marked as returned", returnHtml);
        Assert.DoesNotContain("Overdue Item", returnHtml);

        var reloaded = await db.Loans.AsNoTracking().SingleAsync(l => l.Id == overdueLoan.LoanId);
        Assert.Equal(LoanStatus.Returned, reloaded.Status);
    }
}

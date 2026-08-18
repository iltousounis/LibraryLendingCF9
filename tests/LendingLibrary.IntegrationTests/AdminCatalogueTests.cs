using LendingLibrary.Web.Data;
using LendingLibrary.Web.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LendingLibrary.IntegrationTests;

public class AdminCatalogueTests(LendingLibraryWebApplicationFactory factory)
    : IClassFixture<LendingLibraryWebApplicationFactory>
{
    [Fact]
    public async Task Create_Edit_Delete_RoundTrip()
    {
        var client = await CreateLoggedInAdminClientAsync();

        var createPage = await client.GetAsync("/Admin/Catalogue/Create");
        createPage.EnsureSuccessStatusCode();
        var createToken = await TestHelpers.ExtractAntiforgeryTokenAsync(createPage);

        var title = $"Admin Created {Guid.NewGuid():N}";
        var createForm = new Dictionary<string, string>
        {
            ["Input.Title"] = title,
            ["Input.ItemType"] = "Book",
            ["Input.TotalUnits"] = "4",
            ["__RequestVerificationToken"] = createToken
        };
        var createResponse = await client.PostAsync("/Admin/Catalogue/Create", new FormUrlEncodedContent(createForm));
        createResponse.EnsureSuccessStatusCode();

        var indexHtml = await (await client.GetAsync("/Admin/Catalogue")).Content.ReadAsStringAsync();
        Assert.Contains(title, indexHtml);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var created = await db.CatalogueItems.AsNoTracking().SingleAsync(i => i.Title == title);

        // Edit
        var editPage = await client.GetAsync($"/Admin/Catalogue/Edit/{created.Id}");
        editPage.EnsureSuccessStatusCode();
        var editToken = await TestHelpers.ExtractAntiforgeryTokenAsync(editPage);
        var editHtml = await editPage.Content.ReadAsStringAsync();
        var rowVersion = ExtractHiddenValue(editHtml, "Input.RowVersion");

        var updatedTitle = title + " (Updated)";
        var editForm = new Dictionary<string, string>
        {
            ["Input.Id"] = created.Id.ToString(),
            ["Input.Title"] = updatedTitle,
            ["Input.ItemType"] = "Book",
            ["Input.TotalUnits"] = "6",
            ["Input.RowVersion"] = rowVersion,
            ["__RequestVerificationToken"] = editToken
        };
        var editResponse = await client.PostAsync($"/Admin/Catalogue/Edit/{created.Id}", new FormUrlEncodedContent(editForm));
        editResponse.EnsureSuccessStatusCode();

        var afterEdit = await db.CatalogueItems.AsNoTracking().SingleAsync(i => i.Id == created.Id);
        Assert.Equal(updatedTitle, afterEdit.Title);
        Assert.Equal(6, afterEdit.TotalUnits);
        Assert.Equal(6, afterEdit.AvailableUnits);

        // Delete
        var deletePage = await client.GetAsync($"/Admin/Catalogue/Delete/{created.Id}");
        deletePage.EnsureSuccessStatusCode();
        var deleteToken = await TestHelpers.ExtractAntiforgeryTokenAsync(deletePage);
        var deleteForm = new Dictionary<string, string> { ["__RequestVerificationToken"] = deleteToken };
        var deleteResponse = await client.PostAsync($"/Admin/Catalogue/Delete/{created.Id}", new FormUrlEncodedContent(deleteForm));
        deleteResponse.EnsureSuccessStatusCode();

        var stillVisible = await db.CatalogueItems.AnyAsync(i => i.Id == created.Id);
        Assert.False(stillVisible);
    }

    [Fact]
    public async Task NonAdminUser_CannotReachAdminCatalogue()
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

        var response = await client.GetAsync("/Admin/Catalogue");

        Assert.Equal("/Account/AccessDenied", response.RequestMessage!.RequestUri!.AbsolutePath);
    }

    private async Task<HttpClient> CreateLoggedInAdminClientAsync()
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var email = $"admin-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Test Admin",
            CreatedAtUtc = timeProvider.GetUtcNow()
        };

        var createResult = await userManager.CreateAsync(user, TestHelpers.DefaultPassword);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", createResult.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(user, "Admin");

        var client = factory.CreateClient();
        await TestHelpers.LogInAsync(client, email);
        return client;
    }

    private static string ExtractHiddenValue(string html, string inputName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            html, $@"<input[^>]*name=""{System.Text.RegularExpressions.Regex.Escape(inputName)}""[^>]*>");
        if (!match.Success)
        {
            throw new InvalidOperationException($"Hidden input '{inputName}' not found.");
        }

        var value = System.Text.RegularExpressions.Regex.Match(match.Value, @"value=""([^""]*)""");
        return value.Success ? value.Groups[1].Value : string.Empty;
    }
}

using System.Net;
using LendingLibrary.Web.Data;
using LendingLibrary.Web.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LendingLibrary.IntegrationTests;

public class AdminUsersTests(LendingLibraryWebApplicationFactory factory)
    : IClassFixture<LendingLibraryWebApplicationFactory>
{
    private async Task<ApplicationUser> SeedAdminAsync()
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var email = $"admin-{Guid.NewGuid():N}@example.com";
        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Test Admin",
            CreatedAtUtc = timeProvider.GetUtcNow()
        };
        await userManager.CreateAsync(admin, TestHelpers.DefaultPassword);
        await userManager.AddToRoleAsync(admin, "Admin");
        return admin;
    }

    [Fact]
    public async Task CreateUser_ByAdmin_NewUserCanLogIn()
    {
        var admin = await SeedAdminAsync();
        var client = factory.CreateClient();
        await TestHelpers.LogInAsync(client, admin.Email!);

        var createPage = await client.GetAsync("/Admin/Users/Create");
        createPage.EnsureSuccessStatusCode();
        var token = await TestHelpers.ExtractAntiforgeryTokenAsync(createPage);

        var newEmail = $"newuser-{Guid.NewGuid():N}@example.com";
        var form = new Dictionary<string, string>
        {
            ["Input.DisplayName"] = "Brand New User",
            ["Input.Email"] = newEmail,
            ["Input.Password"] = TestHelpers.DefaultPassword,
            ["Input.ConfirmPassword"] = TestHelpers.DefaultPassword,
            ["Input.Role"] = "User",
            ["__RequestVerificationToken"] = token
        };
        var createResponse = await client.PostAsync("/Admin/Users/Create", new FormUrlEncodedContent(form));
        createResponse.EnsureSuccessStatusCode();
        var indexHtml = await createResponse.Content.ReadAsStringAsync();
        Assert.Contains("Brand New User", indexHtml);

        // The new user can log in with a fresh, unrelated client.
        var newUserClient = factory.CreateClient();
        await TestHelpers.LogInAsync(newUserClient, newEmail);
        var home = await newUserClient.GetAsync("/");
        home.EnsureSuccessStatusCode();
        var homeHtml = await home.Content.ReadAsStringAsync();
        Assert.Contains(newEmail, homeHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DisableUser_PreventsSubsequentLogin()
    {
        var admin = await SeedAdminAsync();

        using var seedScope = factory.Services.CreateScope();
        var userManager = seedScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var timeProvider = seedScope.ServiceProvider.GetRequiredService<TimeProvider>();
        var targetEmail = $"target-{Guid.NewGuid():N}@example.com";
        var target = new ApplicationUser
        {
            UserName = targetEmail,
            Email = targetEmail,
            EmailConfirmed = true,
            DisplayName = "Target User",
            CreatedAtUtc = timeProvider.GetUtcNow()
        };
        await userManager.CreateAsync(target, TestHelpers.DefaultPassword);
        await userManager.AddToRoleAsync(target, "User");

        var client = factory.CreateClient();
        await TestHelpers.LogInAsync(client, admin.Email!);

        var indexPage = await client.GetAsync($"/Admin/Users?q={Uri.EscapeDataString(targetEmail)}");
        indexPage.EnsureSuccessStatusCode();
        var indexHtml = await indexPage.Content.ReadAsStringAsync();
        Assert.Contains("Target User", indexHtml);
        Assert.Contains(">Disable<", indexHtml);

        var token = await TestHelpers.ExtractAntiforgeryTokenAsync(indexPage);
        var form = new Dictionary<string, string>
        {
            ["userId"] = target.Id.ToString(),
            ["locked"] = "true",
            ["__RequestVerificationToken"] = token
        };
        var disableResponse = await client.PostAsync("/Admin/Users?handler=ToggleLock", new FormUrlEncodedContent(form));
        disableResponse.EnsureSuccessStatusCode();
        var disableHtml = await disableResponse.Content.ReadAsStringAsync();
        Assert.Contains("User disabled", disableHtml);

        // The disabled user can no longer log in.
        var targetClient = factory.CreateClient();
        var loginPage = await targetClient.GetAsync("/Account/Login");
        var loginToken = await TestHelpers.ExtractAntiforgeryTokenAsync(loginPage);
        var loginForm = new Dictionary<string, string>
        {
            ["Input.Email"] = targetEmail,
            ["Input.Password"] = TestHelpers.DefaultPassword,
            ["__RequestVerificationToken"] = loginToken
        };
        var loginResponse = await targetClient.PostAsync("/Account/Login", new FormUrlEncodedContent(loginForm));
        loginResponse.EnsureSuccessStatusCode();
        var loginHtml = await loginResponse.Content.ReadAsStringAsync();
        Assert.Contains("locked", loginHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NonAdmin_CannotReachAdminUsers()
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
            DisplayName = "Regular",
            CreatedAtUtc = timeProvider.GetUtcNow()
        };
        await userManager.CreateAsync(user, TestHelpers.DefaultPassword);
        await userManager.AddToRoleAsync(user, "User");

        var client = factory.CreateClient();
        await TestHelpers.LogInAsync(client, email);

        var response = await client.GetAsync("/Admin/Users");

        Assert.Equal("/Account/AccessDenied", response.RequestMessage!.RequestUri!.AbsolutePath);
    }
}

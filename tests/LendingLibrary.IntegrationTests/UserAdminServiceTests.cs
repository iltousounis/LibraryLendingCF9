using LendingLibrary.Web.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace LendingLibrary.IntegrationTests;

public class UserAdminServiceTests(LendingLibraryWebApplicationFactory factory)
    : IClassFixture<LendingLibraryWebApplicationFactory>
{
    [Fact]
    public async Task CreateUserAsync_Succeeds_AssignsRole_AndIsFindableBySearch()
    {
        using var scope = factory.Services.CreateScope();
        var userAdmin = scope.ServiceProvider.GetRequiredService<IUserAdminService>();

        var email = $"created-{Guid.NewGuid():N}@example.com";
        var result = await userAdmin.CreateUserAsync(email, "Created User", TestHelpers.DefaultPassword, "Admin");

        Assert.True(result.Succeeded);

        var found = await userAdmin.SearchUsersAsync(email, 1, 10);
        Assert.Single(found.Items);
        var roles = await userAdmin.GetRolesAsync(found.Items[0]);
        Assert.Contains("Admin", roles);
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateEmail_Fails()
    {
        using var scope = factory.Services.CreateScope();
        var userAdmin = scope.ServiceProvider.GetRequiredService<IUserAdminService>();

        var email = $"dup-{Guid.NewGuid():N}@example.com";
        var first = await userAdmin.CreateUserAsync(email, "First", TestHelpers.DefaultPassword, "User");
        Assert.True(first.Succeeded);

        var second = await userAdmin.CreateUserAsync(email, "Second", TestHelpers.DefaultPassword, "User");

        Assert.False(second.Succeeded);
        Assert.Equal(UserAdminOperationOutcome.ValidationFailed, second.Outcome);
    }

    [Fact]
    public async Task CreateUserAsync_WeakPassword_Fails()
    {
        using var scope = factory.Services.CreateScope();
        var userAdmin = scope.ServiceProvider.GetRequiredService<IUserAdminService>();

        var email = $"weak-{Guid.NewGuid():N}@example.com";
        var result = await userAdmin.CreateUserAsync(email, "Weak Password", "password123", "User");

        Assert.False(result.Succeeded);
        Assert.Equal(UserAdminOperationOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task SetLockedOutAsync_LocksAndUnlocks()
    {
        using var scope = factory.Services.CreateScope();
        var userAdmin = scope.ServiceProvider.GetRequiredService<IUserAdminService>();

        var email = $"lockable-{Guid.NewGuid():N}@example.com";
        var create = await userAdmin.CreateUserAsync(email, "Lockable", TestHelpers.DefaultPassword, "User");
        Assert.True(create.Succeeded);

        var found = (await userAdmin.SearchUsersAsync(email, 1, 10)).Items[0];
        Assert.False(await userAdmin.IsLockedOutAsync(found));

        var lockResult = await userAdmin.SetLockedOutAsync(create.UserId!.Value, locked: true);
        Assert.True(lockResult.Succeeded);
        var afterLock = (await userAdmin.SearchUsersAsync(email, 1, 10)).Items[0];
        Assert.True(await userAdmin.IsLockedOutAsync(afterLock));

        var unlockResult = await userAdmin.SetLockedOutAsync(create.UserId!.Value, locked: false);
        Assert.True(unlockResult.Succeeded);
        var afterUnlock = (await userAdmin.SearchUsersAsync(email, 1, 10)).Items[0];
        Assert.False(await userAdmin.IsLockedOutAsync(afterUnlock));
    }
}

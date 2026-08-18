using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Infrastructure;

namespace LendingLibrary.UnitTests;

public class CommonPasswordValidatorTests
{
    private static readonly ApplicationUser DummyUser = new()
    {
        DisplayName = "Test User",
        UserName = "test@example.com",
        Email = "test@example.com"
    };

    [Theory]
    [InlineData("password123")]
    [InlineData("qwerty123")]
    [InlineData("iloveyou")]
    [InlineData("PASSWORD123")]
    public async Task ValidateAsync_RejectsCommonPasswords(string password)
    {
        var validator = new CommonPasswordValidator();

        var result = await validator.ValidateAsync(manager: null!, DummyUser, password);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ValidateAsync_AcceptsUncommonPassword()
    {
        var validator = new CommonPasswordValidator();

        var result = await validator.ValidateAsync(manager: null!, DummyUser, "Xk9$vTq2#mZp7Lw!");

        Assert.True(result.Succeeded);
    }
}

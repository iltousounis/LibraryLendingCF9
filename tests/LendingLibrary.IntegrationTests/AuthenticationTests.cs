using System.Net;

namespace LendingLibrary.IntegrationTests;

public class AuthenticationTests(LendingLibraryWebApplicationFactory factory)
    : IClassFixture<LendingLibraryWebApplicationFactory>
{
    [Theory]
    [InlineData("/")]
    [InlineData("/Account/Register")]
    [InlineData("/Account/Login")]
    public async Task PublicPages_ReturnOk(string path)
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminArea_Anonymous_RedirectsToLogin()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/Account/Login", response.RequestMessage!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Register_SignsUserIn_AndAssignsUserRole()
    {
        var client = factory.CreateClient();
        var email = $"test-{Guid.NewGuid():N}@example.com";

        var registerPage = await client.GetAsync("/Account/Register");
        var token = await TestHelpers.ExtractAntiforgeryTokenAsync(registerPage);

        var form = new Dictionary<string, string>
        {
            ["Input.DisplayName"] = "Integration Test",
            ["Input.Email"] = email,
            ["Input.Password"] = TestHelpers.DefaultPassword,
            ["Input.ConfirmPassword"] = TestHelpers.DefaultPassword,
            ["__RequestVerificationToken"] = token
        };

        var registerResponse = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(form));
        registerResponse.EnsureSuccessStatusCode();

        var homeHtml = await registerResponse.Content.ReadAsStringAsync();
        Assert.Contains(email, homeHtml, StringComparison.OrdinalIgnoreCase);

        var adminAttempt = await client.GetAsync("/Admin");
        Assert.Equal("/Account/AccessDenied", adminAttempt.RequestMessage!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShowsError()
    {
        var client = factory.CreateClient();

        var loginPage = await client.GetAsync("/Account/Login");
        var token = await TestHelpers.ExtractAntiforgeryTokenAsync(loginPage);

        var form = new Dictionary<string, string>
        {
            ["Input.Email"] = "nobody@example.com",
            ["Input.Password"] = "WrongPassword123!",
            ["__RequestVerificationToken"] = token
        };

        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid email or password", html);
    }
}

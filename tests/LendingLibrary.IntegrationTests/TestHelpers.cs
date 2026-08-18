using System.Text.RegularExpressions;

namespace LendingLibrary.IntegrationTests;

internal static class TestHelpers
{
    public const string DefaultPassword = "Xk9$vTq2#mZp7Lw!";

    public static async Task<string> ExtractAntiforgeryTokenAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        return ExtractAntiforgeryToken(html);
    }

    public static string ExtractAntiforgeryToken(string html)
    {
        var inputTag = Regex.Match(html, @"<input[^>]*name=""__RequestVerificationToken""[^>]*>");
        if (!inputTag.Success)
        {
            throw new InvalidOperationException("Antiforgery input not found on page.");
        }

        var value = Regex.Match(inputTag.Value, @"value=""([^""]+)""");
        if (!value.Success)
        {
            throw new InvalidOperationException("Antiforgery token value not found.");
        }

        return value.Groups[1].Value;
    }

    public static async Task LogInAsync(HttpClient client, string email, string password = DefaultPassword)
    {
        var loginPage = await client.GetAsync("/Account/Login");
        var token = await ExtractAntiforgeryTokenAsync(loginPage);

        var form = new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["__RequestVerificationToken"] = token
        };

        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(form));
        response.EnsureSuccessStatusCode();
    }
}

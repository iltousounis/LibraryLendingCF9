using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LendingLibrary.Web.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public int DisplayStatusCode { get; private set; }

    public string Heading { get; private set; } = "Something went wrong";

    public string Message { get; private set; } = "An unexpected error occurred while processing your request.";

    public void OnGet(int? statusCode)
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        DisplayStatusCode = statusCode ?? 500;

        (Heading, Message) = DisplayStatusCode switch
        {
            404 => ("Page not found", "The page you're looking for doesn't exist or may have been moved."),
            403 => ("Access denied", "You don't have permission to view that page."),
            _ => ("Something went wrong", "An unexpected error occurred while processing your request.")
        };
    }
}

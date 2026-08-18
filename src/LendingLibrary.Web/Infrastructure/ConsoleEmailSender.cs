namespace LendingLibrary.Web.Infrastructure;

/// <summary>Dev-only sink: logs instead of sending. Swap for a real provider outside v1 scope.</summary>
public class ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Email to {ToEmail}: {Subject}\n{Body}", toEmail, subject, htmlBody);
        return Task.CompletedTask;
    }
}

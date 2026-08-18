using LendingLibrary.Web.Services.Abstractions;

namespace LendingLibrary.Web.Infrastructure;

/// <summary>Periodically moves past-expiry Pending reservations to Expired and releases their held units.</summary>
public class ReservationExpiryService(IServiceScopeFactory scopeFactory, ILogger<ReservationExpiryService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var reservationService = scope.ServiceProvider.GetRequiredService<IReservationService>();
                var expiredCount = await reservationService.ExpireStaleReservationsAsync(stoppingToken);
                if (expiredCount > 0)
                {
                    logger.LogInformation("Expired {Count} stale reservation(s).", expiredCount);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to expire stale reservations.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

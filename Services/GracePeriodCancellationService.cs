using EZKLaundrySystem.Data;

namespace EZKLaundrySystem.Services;

/// <summary>
/// Runs every minute while the application is running and cancels online bookings whose
/// customer did not arrive within the 15-minute grace period, freeing the time slot.
/// </summary>
public class GracePeriodCancellationService(ILogger<GracePeriodCancellationService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var connection = new DbConnection().GetConnection();
                await connection.OpenAsync(stoppingToken);
                int cancelled = BookingSchedule.CancelNoShows(connection, DateTime.Now);
                if (cancelled > 0)
                    logger.LogInformation("Grace period: {Count} no-show booking(s) cancelled.", cancelled);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                // A database hiccup (e.g. MySQL stopped) must not stop the website; retry next minute.
                logger.LogWarning(error, "Grace period check failed; retrying in one minute.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

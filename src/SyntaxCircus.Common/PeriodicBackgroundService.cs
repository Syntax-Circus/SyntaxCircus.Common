namespace SyntaxCircus.Common;

/// <summary>
/// A <see cref="BackgroundService"/> that runs <see cref="ExecuteTickAsync"/> on a fixed interval.
/// One failing tick is caught and logged rather than crashing the whole service, and the delay is
/// between ticks (not between tick starts), so a slow tick can't overlap the next one.
/// </summary>
public abstract class PeriodicBackgroundService(TimeSpan interval, ILogger logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteTickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception in {ServiceType} tick.", GetType().Name);
            }

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    protected abstract Task ExecuteTickAsync(CancellationToken cancellationToken);
}

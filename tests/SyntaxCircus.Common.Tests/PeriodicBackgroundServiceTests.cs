namespace SyntaxCircus.Common.Tests;

public class PeriodicBackgroundServiceTests
{
    private sealed class RecordingService(TimeSpan interval, ILogger logger, Func<CancellationToken, Task> onTick)
        : PeriodicBackgroundService(interval, logger)
    {
        protected override Task ExecuteTickAsync(CancellationToken cancellationToken) => onTick(cancellationToken);

        public Task RunAsync(CancellationToken ct) => StartAsync(ct);

        public Task StopRunningAsync(CancellationToken ct) => StopAsync(ct);
    }

    [Fact]
    public async Task ExecuteAsync_TicksRepeatedlyUntilCancelled()
    {
        var tickCount = 0;
        var logger = Substitute.For<ILogger>();
        var service = new RecordingService(
            TimeSpan.FromMilliseconds(10),
            logger,
            _ => { Interlocked.Increment(ref tickCount); return Task.CompletedTask; });

        using var cts = new CancellationTokenSource();
        await service.RunAsync(TestContext.Current.CancellationToken);

        await WaitUntilAsync(() => Volatile.Read(ref tickCount) >= 2);

        await service.StopRunningAsync(TestContext.Current.CancellationToken);

        Volatile.Read(ref tickCount).ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ExecuteAsync_TickThrows_LoopContinuesToNextTick()
    {
        var tickCount = 0;
        var logger = Substitute.For<ILogger>();
        var service = new RecordingService(
            TimeSpan.FromMilliseconds(10),
            logger,
            _ =>
            {
                Interlocked.Increment(ref tickCount);
                throw new InvalidOperationException("tick failed");
            });

        await service.RunAsync(TestContext.Current.CancellationToken);

        await WaitUntilAsync(() => Volatile.Read(ref tickCount) >= 2);

        await service.StopRunningAsync(TestContext.Current.CancellationToken);

        Volatile.Read(ref tickCount).ShouldBeGreaterThanOrEqualTo(2);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }
}

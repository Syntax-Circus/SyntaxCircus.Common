namespace SyntaxCircus.Common.Tests;

public class SlidingWindowRateLimiterTests
{
    [Fact]
    public void TryAcquire_UnderLimit_ReturnsTrue()
    {
        var limiter = new SlidingWindowRateLimiter(permitLimit: 3, window: TimeSpan.FromMinutes(1));

        limiter.TryAcquire("key").ShouldBeTrue();
        limiter.TryAcquire("key").ShouldBeTrue();
    }

    [Fact]
    public void TryAcquire_AtLimit_ReturnsFalse()
    {
        var limiter = new SlidingWindowRateLimiter(permitLimit: 2, window: TimeSpan.FromMinutes(1));

        limiter.TryAcquire("key").ShouldBeTrue();
        limiter.TryAcquire("key").ShouldBeTrue();
        limiter.TryAcquire("key").ShouldBeFalse();
    }

    [Fact]
    public void TryAcquire_ExpiredHitsFreeCapacity()
    {
        var limiter = new SlidingWindowRateLimiter(permitLimit: 1, window: TimeSpan.FromMilliseconds(50));

        limiter.TryAcquire("key").ShouldBeTrue();
        limiter.TryAcquire("key").ShouldBeFalse();

        Thread.Sleep(100);

        limiter.TryAcquire("key").ShouldBeTrue();
    }

    [Fact]
    public void TryAcquire_IndependentKeys_TrackedSeparately()
    {
        var limiter = new SlidingWindowRateLimiter(permitLimit: 1, window: TimeSpan.FromMinutes(1));

        limiter.TryAcquire("a").ShouldBeTrue();
        limiter.TryAcquire("b").ShouldBeTrue();
        limiter.TryAcquire("a").ShouldBeFalse();
        limiter.TryAcquire("b").ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryAcquire_BlankKey_ThrowsArgumentException(string key)
    {
        var limiter = new SlidingWindowRateLimiter(permitLimit: 1, window: TimeSpan.FromMinutes(1));

        Should.Throw<ArgumentException>(() => limiter.TryAcquire(key));
    }
}

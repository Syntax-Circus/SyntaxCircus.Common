using System.Collections.Concurrent;

namespace SyntaxCircus.Common;

/// <summary>
/// A minimal, HttpContext-free sliding-window rate limiter for hosts that aren't behind a normal
/// ASP.NET Core request pipeline (an embedded server, a SignalR hub, a background worker) — where
/// <c>System.Threading.RateLimiting</c>'s middleware integration doesn't apply.
/// </summary>
public sealed class SlidingWindowRateLimiter(int permitLimit, TimeSpan window)
{
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _hits = new(StringComparer.Ordinal);

    /// <summary>Returns true and records a hit if <paramref name="key"/> is under its limit for the current window; false otherwise.</summary>
    public bool TryAcquire(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var queue = _hits.GetOrAdd(key, static _ => new Queue<DateTimeOffset>());
        var now = DateTimeOffset.UtcNow;

        lock (queue)
        {
            while (queue.Count > 0 && now - queue.Peek() > window)
            {
                queue.Dequeue();
            }

            if (queue.Count >= permitLimit)
            {
                return false;
            }

            queue.Enqueue(now);
            return true;
        }
    }
}

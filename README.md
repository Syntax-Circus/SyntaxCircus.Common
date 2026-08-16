# SyntaxCircus.Common

[![Build](https://github.com/Syntax-Circus/SyntaxCircus.Common/actions/workflows/build.yml/badge.svg)](https://github.com/Syntax-Circus/SyntaxCircus.Common/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/SyntaxCircus.Common.svg)](https://www.nuget.org/packages/SyntaxCircus.Common)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

The handful of contract types and dependency-free helpers that keep getting reinvented per product: a pagination result, `ClaimsPrincipal` claim resolution, a minimal current-user abstraction, a periodic background service base, and a standalone sliding-window rate limiter for hosts that aren't a normal ASP.NET Core pipeline.

> **No support guaranteed.** Published as-is and maintained on a best-effort basis. Issues and PRs are welcome, but there's no SLA — fork it or vendor what you need if that's not enough.

## PagedResult&lt;T&gt;

```csharp
new PagedResult<Widget>(items, page: 1, pageSize: 25, totalCount: 142);
// .TotalPages, .HasPreviousPage, .HasNextPage are computed
```

## ClaimsPrincipalExtensions

```csharp
user.GetSubject();     // "sub" claim, falling back to ClaimTypes.NameIdentifier
user.GetEmail();       // "email" claim, falling back to ClaimTypes.Email
user.GetDisplayName(); // "name" claim, falling back to "preferred_username"
```

## ICurrentUserService

```csharp
builder.Services.AddCurrentUserService();
```

```csharp
public sealed class MyService(ICurrentUserService currentUser)
{
    public void DoSomething()
    {
        if (!currentUser.IsAuthenticated) return;
        var userId = currentUser.UserId;
    }
}
```

A thin scoped wrapper over `IHttpContextAccessor` exposing `IsAuthenticated`, `UserId`, `Email`, `DisplayName`, and the raw `Principal`, built on `ClaimsPrincipalExtensions`.

## PeriodicBackgroundService

```csharp
public sealed class CleanupWorker(ILogger<CleanupWorker> logger)
    : PeriodicBackgroundService(TimeSpan.FromMinutes(5), logger)
{
    protected override async Task ExecuteTickAsync(CancellationToken cancellationToken)
    {
        // do the periodic work
    }
}
```

A `BackgroundService` base that runs `ExecuteTickAsync` on a fixed interval — one failing tick is caught and logged rather than crashing the whole service, and the delay is between ticks (not tick starts), so a slow tick can't overlap the next one.

## SlidingWindowRateLimiter

```csharp
var limiter = new SlidingWindowRateLimiter(permitLimit: 5, window: TimeSpan.FromMinutes(1));

if (!limiter.TryAcquire(key: remoteIpAddress))
{
    // reject
}
```

A plain, key-based sliding-window limiter with no HttpContext or middleware dependency — for hosts that aren't a normal ASP.NET Core request pipeline (an embedded server, a SignalR hub, a background worker) where `System.Threading.RateLimiting`'s middleware integration doesn't apply.

## Contributing

Issues and pull requests are welcome:
- Keep changes focused, with a clear description of the behavior change.
- Match the existing code style (see `.editorconfig`).
- Call out any breaking changes to the public API in your PR description.

## License

MIT — see [LICENSE.txt](LICENSE.txt).

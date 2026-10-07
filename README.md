# SyntaxCircus.Common

[![Build](https://github.com/Syntax-Circus/SyntaxCircus.Common/actions/workflows/build.yml/badge.svg)](https://github.com/Syntax-Circus/SyntaxCircus.Common/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/SyntaxCircus.Common.svg)](https://www.nuget.org/packages/SyntaxCircus.Common)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

The handful of contract types and dependency-free helpers that keep getting reinvented per product: operation results, a pagination result, `ClaimsPrincipal` claim resolution, a periodic background service base, and a standalone sliding-window rate limiter for hosts that aren't a normal ASP.NET Core pipeline.

Since 0.2.0 the package has **no web framework dependency**: it references only `Microsoft.Extensions.*` abstractions, so console, worker, SDK and MAUI consumers do not inherit `Microsoft.AspNetCore.App`.

> **No support guaranteed.** Published as-is and maintained on a best-effort basis. Issues and PRs are welcome, but there's no SLA — fork it or vendor what you need if that's not enough.

## Result and Result&lt;T&gt;

Use transport-neutral results across application boundaries. Errors carry a stable code, a client-safe message, a semantic kind, and an optional validation target; `Result`/`Result<T>` themselves do not carry HTTP status codes — see `ApiResult`/`ApiResult<T>` below for the one case that needs to.

```csharp
public async Task<Result<Widget>> HandleAsync(CreateWidgetRequest request)
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Result<Widget>.Failure(new ResultError(
            "name-required",
            "A name is required.",
            ResultErrorKind.Validation,
            "name"));
    }

    var widget = await CreateAsync(request);
    return Result<Widget>.Success(widget);
}
```

Failures contain at least one error. Multiple errors are reserved for validation failures, and all errors in a result have the same kind. Accessing `Value` on a failed `Result<T>` throws.

## ApiResult and ApiResult&lt;T&gt;

For the rare case where a failure needs to carry an exact upstream HTTP
status code — proxying a third-party API's 429/502/503 rather than
collapsing it into one of `ResultErrorKind`'s fixed kinds — `ApiResult`
and `ApiResult<T>` extend `Result`/`Result<T>` with a `StatusCode`:

```csharp
public async Task<ApiResult<Widget>> HandleAsync(CreateWidgetRequest request)
{
    var upstream = await CallUpstreamApiAsync(request);
    if (!upstream.IsSuccess)
    {
        return ApiResult<Widget>.Failure(
            upstream.StatusCode,
            "upstream-widget-error",
            "The upstream widget service returned an error.");
    }

    return ApiResult<Widget>.Success(upstream.Widget);
}
```

`StatusCode` is `null` on success — success-side status selection stays
the caller's responsibility, same as `Result`/`Result<T>`. `Failure`
requires a status in the 400–599 range. The constructed error always has
`ResultErrorKind.Passthrough` and no validation target.

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

## Moved: ICurrentUserService

`ICurrentUserService`, its implementation and `AddCurrentUserService()` live in `SyntaxCircus.AspNetCore.Common` 0.1.16+ (namespace `SyntaxCircus.AspNetCore.Common`) since 0.2.0.

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

Since 0.2.0 this package has no web framework dependency, and `ICurrentUserService` moved to `SyntaxCircus.AspNetCore.Common`. The [web-neutral contracts](docs/enhancements/web-neutral-contracts.md) proposal is kept as history.

Issues and pull requests are welcome:
- Keep changes focused, with a clear description of the behavior change.
- Match the existing code style (see `.editorconfig`).
- Call out any breaking changes to the public API in your PR description.

## License

MIT — see [LICENSE.txt](LICENSE.txt).

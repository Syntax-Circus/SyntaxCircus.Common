# SyntaxCircus.Common

[![Build](https://github.com/Syntax-Circus/SyntaxCircus.Common/actions/workflows/build.yml/badge.svg)](https://github.com/Syntax-Circus/SyntaxCircus.Common/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/SyntaxCircus.Common.svg)](https://www.nuget.org/packages/SyntaxCircus.Common)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

The handful of contract types and dependency-free helpers that keep getting reinvented per product: operation results, a pagination result, `ClaimsPrincipal` claim resolution, a periodic background service base, lazy one-line `LogIf` logging, and a standalone sliding-window rate limiter for hosts that aren't a normal ASP.NET Core pipeline.

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

## LogIf

```csharp
logger.LogIf(LogLevel.Information,
    "Access token refused ({Reason}): {Detail}",
    $"{reason}{result.Exception?.Message}");
```

A replacement for wrapping a log call in `if (logger.IsEnabled(...))`. The message is an ordinary logging template, the same one you would pass to `LogInformation`, so field names, formats (`{Total:N2}`), alignment and destructuring (`{@Request}`) work as they always do. Overloads also accept an `Exception`, an `EventId`, or both, before the template.

The values follow in a `$"..."`, one `{}` for each placeholder, in order. No string is built: nothing inside `{}` is evaluated unless the level is enabled, and each value reaches the logger with its original type. That includes side effects, so don't put work in a value that must always happen.

- **Put nothing but `{}` values in the `$"..."`.** Text between them, formats (`$"{total:N2}"`), alignment and a string built beforehand do not compile. Formatting belongs in the template.
- **Keep the expensive expression inside the `{}`.** Work assigned to a local on an earlier line always runs.
- **Keep the template constant.** `LogIf(level, $"User {name}", $"")` is the easy mistake: the template is built even when the level is off, and no field is recorded. Write `LogIf(level, "User {Name}", $"{name}")`.
- **Match the value count.** At run time `LogIf` behaves like `LogInformation`. With at least one value, too few throws `FormatException` when the entry is formatted, and extra values are ignored. With no values (`$""`), nothing throws and the template is logged as written, placeholders included.

Requires C# 10 or later. For a message with no values, use `LogInformation` and friends.

### Build checks

The package includes an analyzer for `LogIf` calls:

| Rule | Warns when |
|---|---|
| `SCLOG001` | The number of values differs from the number of placeholders (what CA2017 does for `LogInformation`) |
| `SCLOG002` | The message template is not a constant |
| `SCLOG003` | The values are not an interpolated string written in the call. With `default`, for example, nothing is ever logged |
| `SCLOG004` | The logger cannot parse the template, for example because of an unmatched brace |

```csharp
logger.LogIf(LogLevel.Information, "{Reason}: {Detail}", $"{reason}");
// warning SCLOG001: Message template placeholders: 2; values supplied: 1
```

It needs no setup. Change a rule's severity in `.editorconfig`, for example `dotnet_diagnostic.SCLOG001.severity = error`. It checks the number of values, not their order. It is built against Roslyn 4.8, so it needs the .NET 8 SDK or Visual Studio 17.8, or later.

## Contributing

Since 0.2.0 this package has no web framework dependency, and `ICurrentUserService` moved to `SyntaxCircus.AspNetCore.Common`. The [web-neutral contracts](docs/enhancements/web-neutral-contracts.md) proposal is kept as history.

Issues and pull requests are welcome:
- Keep changes focused, with a clear description of the behavior change.
- Match the existing code style (see `.editorconfig`).
- Call out any breaking changes to the public API in your PR description.

## License

MIT — see [LICENSE.txt](LICENSE.txt).

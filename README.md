# SyntaxCircus.Common

[![Build](https://github.com/Syntax-Circus/SyntaxCircus.Common/actions/workflows/build.yml/badge.svg)](https://github.com/Syntax-Circus/SyntaxCircus.Common/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

The handful of contract types that keep getting reinvented per product: a pagination result, `ClaimsPrincipal` claim resolution, and a minimal current-user abstraction built on top of it.

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

## Contributing

Issues and pull requests are welcome:
- Keep changes focused, with a clear description of the behavior change.
- Match the existing code style (see `.editorconfig`).
- Call out any breaking changes to the public API in your PR description.

## License

MIT — see [LICENSE.txt](LICENSE.txt).

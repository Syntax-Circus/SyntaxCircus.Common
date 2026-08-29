# ApiResult Status Passthrough (SyntaxCircus.Common) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `ApiResult`/`ApiResult<T>` to `SyntaxCircus.Common`, subclassing `Result`/`Result<T>`, so a caller can carry an exact upstream HTTP status code through a failure result instead of being limited to `ResultErrorKind`'s fixed kind-to-status mapping.

**Architecture:** Unseal `Result`/`Result<T>` and widen their constructors to `protected` (purely additive, no behavior change). Add a new `ResultErrorKind.Passthrough` enum member. Add `ApiResult : Result` and `ApiResult<T> : Result<T>`, each with a nullable `StatusCode` property (null on success) and a `Failure(HttpStatusCode, string code, string message)` factory that builds a single `Passthrough`-kind `ResultError`. Pack the result locally into a shared folder feed so the sibling `SyntaxCircus.AspNetCore.Common` repo can consume the unreleased build before this package is actually published.

**Tech Stack:** .NET 10, xunit.v3, Shouldly, Central Package Management (`Directory.Packages.props`), GitVersion (TrunkBased workflow).

**Spec:** `docs/superpowers/specs/2026-08-28-apiresult-status-passthrough-design.md`

## Global Constraints

- No change to `Result`/`Result<T>`'s existing public API surface beyond removing `sealed` and widening constructors to `protected`.
- `ApiResult`/`ApiResult<T>.Failure` take only `(HttpStatusCode statusCode, string code, string message)` — no `target` parameter, no way to attach a bespoke payload beyond `code`/`message` (per spec's "Non-goals").
- `StatusCode` is `null` on success; success-side status selection stays entirely the controller's responsibility (unchanged from today).
- This plan does not touch `SyntaxCircus.AspNetCore.Common` source (that is a separate plan/repo) — it only produces a local package build for that repo to consume during development.
- This plan does not touch sinforgiver or `_template` (per spec's "Rollout scope").

---

### Task 1: Unseal `Result` and `Result<T>`

**Files:**
- Modify: `src/SyntaxCircus.Common/Result.cs`
- Modify: `src/SyntaxCircus.Common/ResultOfT.cs`
- Test: `tests/SyntaxCircus.Common.Tests/ResultTests.cs` (no new test — existing suite is the regression check)

**Interfaces:**
- Consumes: nothing new.
- Produces: `Result` with a `protected Result(bool isSuccess, IReadOnlyList<ResultError> errors)` constructor; `Result<T>` with a `protected Result(bool isSuccess, T? value, IReadOnlyList<ResultError> errors)` constructor. Task 3 calls both via `base(...)`.

This is a pure structural refactor (no new observable behavior), so the TDD cycle here is "confirm the existing suite is green before and after," not a new failing test.

- [ ] **Step 1: Confirm baseline is green**

Run: `dotnet test tests/SyntaxCircus.Common.Tests --filter FullyQualifiedName~ResultTests`
Expected: PASS (all existing `ResultTests` pass before any change).

- [ ] **Step 2: Unseal `Result` and widen its constructor**

In `src/SyntaxCircus.Common/Result.cs`, change:

```csharp
public sealed class Result
{
    private static readonly Result SuccessResult = new(true, []);

    private Result(bool isSuccess, IReadOnlyList<ResultError> errors)
```

to:

```csharp
public class Result
{
    private static readonly Result SuccessResult = new(true, []);

    protected Result(bool isSuccess, IReadOnlyList<ResultError> errors)
```

- [ ] **Step 3: Unseal `Result<T>` and widen its constructor**

In `src/SyntaxCircus.Common/ResultOfT.cs`, change:

```csharp
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(bool isSuccess, T? value, IReadOnlyList<ResultError> errors)
```

to:

```csharp
public class Result<T>
{
    private readonly T? _value;

    protected Result(bool isSuccess, T? value, IReadOnlyList<ResultError> errors)
```

- [ ] **Step 4: Confirm the suite is still green**

Run: `dotnet test tests/SyntaxCircus.Common.Tests --filter FullyQualifiedName~ResultTests`
Expected: PASS (identical results to Step 1 — proves no behavior change).

- [ ] **Step 5: Commit**

```bash
git add src/SyntaxCircus.Common/Result.cs src/SyntaxCircus.Common/ResultOfT.cs
git commit -m "refactor: unseal Result/Result<T> for ApiResult subclassing"
```

---

### Task 2: Add `ResultErrorKind.Passthrough`

**Files:**
- Modify: `src/SyntaxCircus.Common/ResultErrorKind.cs`
- Test: `tests/SyntaxCircus.Common.Tests/ResultTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `ResultErrorKind.Passthrough`, a valid (`Enum.IsDefined`) enum member. Task 3's `ApiResult`/`ApiResult<T>.Failure` construct `ResultError`s with this kind.

- [ ] **Step 1: Write the failing test**

In `tests/SyntaxCircus.Common.Tests/ResultTests.cs`, add this test immediately after `ResultError_RejectsTargetForNonValidationKind`:

```csharp
    [Fact]
    public void ResultError_AcceptsPassthroughKind()
    {
        var error = new ResultError(
            "upstream-error",
            "The upstream service returned an unexpected status.",
            ResultErrorKind.Passthrough);

        error.Kind.ShouldBe(ResultErrorKind.Passthrough);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/SyntaxCircus.Common.Tests --filter FullyQualifiedName~ResultError_AcceptsPassthroughKind`
Expected: FAIL — build error, `ResultErrorKind` has no member `Passthrough`.

- [ ] **Step 3: Add the enum member**

In `src/SyntaxCircus.Common/ResultErrorKind.cs`, change:

```csharp
public enum ResultErrorKind
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthenticated,
    Forbidden,
}
```

to:

```csharp
public enum ResultErrorKind
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthenticated,
    Forbidden,
    Passthrough,
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/SyntaxCircus.Common.Tests --filter FullyQualifiedName~ResultError_AcceptsPassthroughKind`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/SyntaxCircus.Common/ResultErrorKind.cs tests/SyntaxCircus.Common.Tests/ResultTests.cs
git commit -m "feat: add ResultErrorKind.Passthrough"
```

---

### Task 3: Add `ApiResult` and `ApiResult<T>`

**Files:**
- Create: `src/SyntaxCircus.Common/ApiResult.cs`
- Create: `src/SyntaxCircus.Common/ApiResultOfT.cs`
- Modify: `tests/SyntaxCircus.Common.Tests/GlobalUsings.cs` (add `System.Net` for `HttpStatusCode`)
- Create: `tests/SyntaxCircus.Common.Tests/ApiResultTests.cs`

**Interfaces:**
- Consumes: `Result`/`Result<T>`'s `protected` constructors from Task 1; `ResultErrorKind.Passthrough` from Task 2; the internal `ResultErrorCollection.Create(ResultError first, ResultError[] additional)` (already `internal static`, same assembly).
- Produces: `ApiResult` (`HttpStatusCode? StatusCode`, `static ApiResult Success()`, `static ApiResult Failure(HttpStatusCode, string, string)`) and `ApiResult<T>` (`HttpStatusCode? StatusCode`, `static ApiResult<T> Success(T)`, `static ApiResult<T> Failure(HttpStatusCode, string, string)`). `SyntaxCircus.AspNetCore.Common`'s plan (separate repo) consumes both types and their `StatusCode`/`Errors`/`Value`/`IsSuccess` members.

- [ ] **Step 1: Write the failing tests**

Add `System.Net` to `tests/SyntaxCircus.Common.Tests/GlobalUsings.cs` (append as a new line, keeping alphabetical-ish grouping used in the file):

```csharp
global using System.Security.Claims;
global using Microsoft.AspNetCore.Http;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using NSubstitute;
global using Shouldly;
global using SyntaxCircus.Common;
global using Xunit;
```

becomes:

```csharp
global using System.Net;
global using System.Security.Claims;
global using Microsoft.AspNetCore.Http;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using NSubstitute;
global using Shouldly;
global using SyntaxCircus.Common;
global using Xunit;
```

Create `tests/SyntaxCircus.Common.Tests/ApiResultTests.cs`:

```csharp
namespace SyntaxCircus.Common.Tests;

public class ApiResultTests
{
    [Fact]
    public void Success_HasNoStatusCode()
    {
        var result = ApiResult.Success();

        result.IsSuccess.ShouldBeTrue();
        result.StatusCode.ShouldBeNull();
    }

    [Fact]
    public void Failure_ExposesStatusCodeAndPassthroughError()
    {
        var result = ApiResult.Failure(
            HttpStatusCode.TooManyRequests,
            "revenuecat-rate-limited",
            "RevenueCat is rate-limiting requests.");

        result.IsSuccess.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        var error = result.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("revenuecat-rate-limited");
        error.Message.ShouldBe("RevenueCat is rate-limiting requests.");
        error.Kind.ShouldBe(ResultErrorKind.Passthrough);
    }

    [Fact]
    public void ApiResult_IsAssignableToResult()
    {
        ApiResult apiResult = ApiResult.Success();
        Result baseResult = apiResult;

        baseResult.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void GenericSuccess_HasNoStatusCodeAndExposesValue()
    {
        var result = ApiResult<string>.Success("accepted");

        result.IsSuccess.ShouldBeTrue();
        result.StatusCode.ShouldBeNull();
        result.Value.ShouldBe("accepted");
    }

    [Fact]
    public void GenericSuccess_RejectsNullValue()
    {
        Should.Throw<ArgumentNullException>(() => ApiResult<string>.Success(null!));
    }

    [Fact]
    public void GenericFailure_ExposesStatusCodeAndPassthroughError()
    {
        var result = ApiResult<string>.Failure(
            HttpStatusCode.BadGateway,
            "stripe-sync-failed",
            "Stripe rejected the sync request.");

        result.IsSuccess.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        var error = result.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("stripe-sync-failed");
        error.Message.ShouldBe("Stripe rejected the sync request.");
        error.Kind.ShouldBe(ResultErrorKind.Passthrough);
    }

    [Fact]
    public void GenericFailure_ThrowsWhenValueIsAccessed()
    {
        var result = ApiResult<string>.Failure(
            HttpStatusCode.ServiceUnavailable,
            "storage-timeout",
            "The storage backend timed out.");

        var exception = Should.Throw<InvalidOperationException>(() => _ = result.Value);

        exception.Message.ShouldContain("failure", Case.Insensitive);
    }

    [Fact]
    public void GenericApiResult_IsAssignableToGenericResult()
    {
        ApiResult<string> apiResult = ApiResult<string>.Success("accepted");
        Result<string> baseResult = apiResult;

        baseResult.Value.ShouldBe("accepted");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/SyntaxCircus.Common.Tests --filter FullyQualifiedName~ApiResultTests`
Expected: FAIL — build error, no type `ApiResult`/`ApiResult<T>` exists yet.

- [ ] **Step 3: Create `ApiResult`**

Create `src/SyntaxCircus.Common/ApiResult.cs`:

```csharp
namespace SyntaxCircus.Common;

public sealed class ApiResult : Result
{
    private ApiResult(bool isSuccess, IReadOnlyList<ResultError> errors, HttpStatusCode? statusCode)
        : base(isSuccess, errors)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }

    public static new ApiResult Success() => new(true, [], null);

    public static ApiResult Failure(HttpStatusCode statusCode, string code, string message) =>
        new(
            false,
            ResultErrorCollection.Create(new ResultError(code, message, ResultErrorKind.Passthrough), []),
            statusCode);
}
```

- [ ] **Step 4: Create `ApiResult<T>`**

Create `src/SyntaxCircus.Common/ApiResultOfT.cs`:

```csharp
namespace SyntaxCircus.Common;

#pragma warning disable CA1000 // Factories are intentionally discoverable on ApiResult<T>, matching Result<T>.
public sealed class ApiResult<T> : Result<T>
{
    private ApiResult(bool isSuccess, T? value, IReadOnlyList<ResultError> errors, HttpStatusCode? statusCode)
        : base(isSuccess, value, errors)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }

    public static new ApiResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(true, value, [], null);
    }

    public static ApiResult<T> Failure(HttpStatusCode statusCode, string code, string message) =>
        new(
            false,
            default,
            ResultErrorCollection.Create(new ResultError(code, message, ResultErrorKind.Passthrough), []),
            statusCode);
}
#pragma warning restore CA1000
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/SyntaxCircus.Common.Tests --filter FullyQualifiedName~ApiResultTests`
Expected: PASS (all 9 tests)

- [ ] **Step 6: Run the full test project to check for regressions**

Run: `dotnet test tests/SyntaxCircus.Common.Tests`
Expected: PASS (all tests, including `ResultTests`, `PagedResultTests`, etc.)

- [ ] **Step 7: Commit**

```bash
git add src/SyntaxCircus.Common/ApiResult.cs src/SyntaxCircus.Common/ApiResultOfT.cs tests/SyntaxCircus.Common.Tests/GlobalUsings.cs tests/SyntaxCircus.Common.Tests/ApiResultTests.cs
git commit -m "feat: add ApiResult/ApiResult<T> for HTTP status-code passthrough"
```

---

### Task 4: Pack locally for cross-repo consumption

**Files:**
- None (build output only).

**Interfaces:**
- Consumes: the completed `src/SyntaxCircus.Common/SyntaxCircus.Common.csproj` build output from Tasks 1–3.
- Produces: a `.nupkg`/`.snupkg` pair in the shared local feed folder `D:\dev\SyntaxCircus\.worktrees\result-local-feed`, and the exact package version string, which the `SyntaxCircus.AspNetCore.Common` implementation plan (separate repo/plan) needs to pin to.

This folder is an existing, untracked (not a git repository, not part of any repo's history) local NuGet folder-feed already used for this kind of cross-repo development — reuse it rather than creating a new location.

- [ ] **Step 1: Build and test in Release configuration**

Run: `dotnet build SyntaxCircus.Common.slnx --configuration Release`
Expected: Build succeeds, 0 warnings, 0 errors.

Run: `dotnet test SyntaxCircus.Common.slnx --no-build --configuration Release`
Expected: All tests PASS.

- [ ] **Step 2: Pack into the shared local feed folder**

Run:

```bash
dotnet pack src/SyntaxCircus.Common/SyntaxCircus.Common.csproj --no-build --configuration Release --output "D:\dev\SyntaxCircus\.worktrees\result-local-feed"
```

Expected: Command succeeds and prints the path to the produced `.nupkg`, e.g. `Successfully created package 'D:\dev\SyntaxCircus\.worktrees\result-local-feed\SyntaxCircus.Common.<VERSION>.nupkg'.`

- [ ] **Step 3: Record the exact package version**

Run: `ls "D:\dev\SyntaxCircus\.worktrees\result-local-feed" | grep "^SyntaxCircus.Common\."`

Read the version out of the filename — the part between `SyntaxCircus.Common.` and `.nupkg` (e.g. `0.2.0-api-result-status-passthrough.1`). This exact string is the value the `SyntaxCircus.AspNetCore.Common` plan's Task 1 pins `Directory.Packages.props`'s `SyntaxCircus.Common` `PackageVersion` to, wrapped in brackets (e.g. `Version="[0.2.0-api-result-status-passthrough.1]"`), matching the existing exact-pin convention already used there.

Note this version down (e.g. paste it into the chat) before moving to the `SyntaxCircus.AspNetCore.Common` plan — there is no commit for this step, since nothing in the git-tracked source changed.

## Self-Review Notes

- **Spec coverage:** Type shape (Task 3), error kind (Task 2), unsealing for subclassing (Task 1), and the local-feed rollout mechanism needed before the sibling repo's HTTP-mapping work (Task 4) are all covered. The HTTP-mapping section of the spec (`ToActionResult` overloads, `MapWithStatus`, the `DefaultStatusCodeMapper` fallback branch) belongs to `SyntaxCircus.AspNetCore.Common` and is covered by that repo's own plan, not this one.
- **Placeholder scan:** No TBD/TODO. Task 4's version-string step is runtime-determined by design (the exact string cannot be known until GitVersion computes it), not an unresolved decision — the step gives the exact command and exact rule for reading it out.
- **Type consistency:** `ApiResult.Failure`/`ApiResult<T>.Failure` signatures match between the type-shape steps (Task 3) and are used identically across all of `ApiResultTests.cs`.

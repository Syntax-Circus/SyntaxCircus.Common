# ApiResult / ApiResult&lt;T&gt;: HTTP status-code passthrough

## Context

`Result` and `Result<T>` (`SyntaxCircus.Common`) model an outcome as
success-or-failure, where failures carry one or more `ResultError`s tagged
with a `ResultErrorKind` (`Failure`/`Validation`/`NotFound`/`Conflict`/
`Unauthenticated`/`Forbidden`). `SyntaxCircus.AspNetCore.Common`'s
`ResultProblemDetailsMapper` turns a failed result into a `ProblemDetails`
by running `errors[0].Kind` through `ResultProblemDetailsOptions.StatusCodeMapper`
(`Func<ResultErrorKind, int>`), which maps each kind to one fixed HTTP status.

That fixed kind→status mapping cannot represent an arbitrary *upstream*
status code that a caller needs to forward verbatim — e.g. RevenueCat
returning 429/502/503, or a Stripe sync call failing with a 502. A consuming
project (sinforgiver) hit this gap on four call sites and left them as
direct `ActionResult` returns rather than force an awkward fit through
`Result<T>` (see sinforgiver's `docs/architecture/DECISION-LOG.md`, "Actions
not yet converted to handlers (ResultErrorKind gap)" and its
"Follow-up: extended Result&lt;T&gt; for upstream status passthrough" entry,
dated 2026-08-28).

This spec adds `ApiResult`/`ApiResult<T>` to `SyntaxCircus.Common`, plus
matching `ToActionResult` support in `SyntaxCircus.AspNetCore.Common`, to
close that gap.

## Decisions carried in from brainstorming

- `ApiResult`/`ApiResult<T>` are real subclasses of `Result`/`Result<T>`
  (not a parallel/standalone type), so `Result`/`Result<T>` lose `sealed`
  and get `protected` constructors. This is additive only — no existing
  consumer depends on either type being sealed or on constructor
  accessibility, since both are only ever produced via their static
  factories.
- Failure responses through `ApiResult` render as the same `ProblemDetails`/
  shape the rest of the codebase already produces, just with the caller's
  explicit status code instead of a kind-derived one. Consuming projects
  that adopt `ApiResult` at call sites with pre-existing bespoke failure
  bodies (e.g. a custom `{ Status, Message }` DTO) will see their failure
  response body shape change to `ProblemDetails`. That migration is out of
  scope for this spec (see "Rollout scope" below).

## Type shape

```csharp
namespace SyntaxCircus.Common;

public sealed class ApiResult : Result
{
    public HttpStatusCode? StatusCode { get; }

    public static new ApiResult Success();

    public static ApiResult Failure(HttpStatusCode statusCode, string code, string message);
}

public sealed class ApiResult<T> : Result<T>
{
    public HttpStatusCode? StatusCode { get; }

    public static new ApiResult<T> Success(T value);

    public static ApiResult<T> Failure(HttpStatusCode statusCode, string code, string message);
}
```

- `StatusCode` is `null` on success. Success responses are unaffected by
  this feature — the controller still picks 200/201/202/etc. itself via the
  `onSuccess` callback passed to `ToActionResult`, exactly as `Result<T>`
  works today.
- `Failure` takes `code`/`message` directly rather than a caller-constructed
  `ResultError`, because there is no meaningful `ResultErrorKind` for a
  caller to pick for this case (see next section) — asking them to supply
  one anyway would be a false choice.
- `Failure` does not accept a `target`. Passthrough failures represent a
  single upstream/transport-level outcome, not field-level validation, so
  the `ResultError.Target` (validation-only) concept doesn't apply here.
- Both `Success` factories use `new` to intentionally hide (not override)
  the base type's static factory of the same name — each type's factory is
  resolved at the call site's static type (`ApiResult<T>.Success(...)` vs
  `Result<T>.Success(...)`), there is no runtime dispatch involved.

## Error kind

`ApiResult`/`ApiResult<T>`'s `Failure` factories internally construct a
single `ResultError` using a new enum member:

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

`Passthrough` exists so that inspecting `result.Errors[0].Kind` on an
`ApiResult` self-documents "the real status lives on `StatusCode`, not this
enum" rather than silently reusing an unrelated existing kind (e.g.
`Failure`) whose usual 500 mapping would be misleading if anyone read the
error in isolation.

`ResultProblemDetailsOptions.DefaultStatusCodeMapper`'s switch expression
gains a `ResultErrorKind.Passthrough => StatusCodes.Status500InternalServerError`
branch purely so the mapper stays exhaustive (it currently throws
`ArgumentOutOfRangeException` for undefined kinds). This branch is never
exercised through the normal `ApiResult` → `ToActionResult` path described
below — it only matters if someone constructs a raw `ResultError` with
`ResultErrorKind.Passthrough` and pushes it through the *ordinary*
`Result<T>.Failure`/`ToActionResult` path, bypassing `ApiResult` entirely.

`ResultErrorCollection.Create`'s existing invariants (all errors in one
result share one `Kind`; only `Validation` may carry more than one error or
a non-null `Target`) apply unchanged — `ApiResult`'s `Failure` factories
only ever construct exactly one error, so both invariants are trivially
satisfied.

## HTTP mapping (`SyntaxCircus.AspNetCore.Common`)

`ResultActionResultExtensions` gains two new overloads, resolved by ordinary
C# overload resolution whenever the compile-time type of `result` is
`ApiResult`/`ApiResult<T>` (no virtual dispatch, no runtime type checks):

```csharp
public static IActionResult ToActionResult(
    this ApiResult result,
    ControllerBase controller,
    Func<IActionResult> onSuccess);

public static IActionResult ToActionResult<T>(
    this ApiResult<T> result,
    ControllerBase controller,
    Func<T, IActionResult> onSuccess);
```

On success, behavior is identical to the existing `Result`/`Result<T>`
overloads (call `onSuccess`). On failure, instead of calling
`ResultProblemDetailsMapper.Map` (which looks up the status via
`StatusCodeMapper(errors[0].Kind)`), these overloads call a new method:

```csharp
internal ObjectResult MapWithStatus(ControllerBase controller, ResultError error, int statusCode);
```

added to `ResultProblemDetailsMapper`. This reuses the exact same
`ProblemDetails` construction as the existing private `MapProblem` (`Type`
via `BuildTypeUri(error.Code)`, `Title` via
`ReasonPhrases.GetReasonPhrase(statusCode)`, `Detail` = `error.Message`,
`Instance` = the request path, same `application/problem+json` content
type) but takes `statusCode` as a parameter instead of deriving it from
`StatusCodeMapper`. `MapProblem` is refactored to delegate to
`MapWithStatus` internally to avoid duplicating the `ProblemDetails`
construction.

Only the single-error (`MapProblem`) path is needed — `ApiResult` failures
are always exactly one error (passthrough failures aren't validation
failures), so `MapWithStatus` does not need a validation-grouping
counterpart.

Because `ApiResult<T>.StatusCode` is `HttpStatusCode?` and only ever
non-null on a failure result, the new `ToActionResult` overloads read
`result.StatusCode!.Value` on the failure branch (guarded by
`result.IsFailure`, matching the existing `Result<T>.Value`
throw-on-failure-access pattern already used elsewhere in this file).

## Testing

- `SyntaxCircus.Common.Tests`: new `ApiResultTests.cs` covering both
  `ApiResult` and `ApiResult<T>` in one file, matching the existing
  `ResultTests.cs` convention (which likewise covers both `Result` and
  `Result<T>` in one file). Covers: success construction leaves
  `StatusCode` null; `Failure` sets
  `StatusCode` to the exact value passed; the constructed `ResultError` has
  `Kind == ResultErrorKind.Passthrough`; existing `ResultErrorCollection`
  single-error invariants are satisfied (no exception).
- `SyntaxCircus.AspNetCore.Common.Tests`: new
  `ApiResultActionResultExtensionsTests.cs` covering: a handful of
  representative arbitrary status codes (400, 404, 429, 502, 503, 413)
  round-trip through `ToActionResult` to an `ObjectResult` with that exact
  `StatusCode` and a `ProblemDetails` body whose `Status`/`Title`/`Detail`
  match; success path still calls `onSuccess` unchanged; existing
  `Result`/`Result<T>` `ToActionResult` tests are unaffected (regression
  check, not new coverage).
- `ResultActionResultExtensionsTests.cs` (which already exercises
  `DefaultStatusCodeMapper` today): add a case confirming
  `DefaultStatusCodeMapper(ResultErrorKind.Passthrough)` returns 500 rather
  than throwing, without asserting anything about when that path is hit in
  practice.

## Rollout scope

This spec covers only `SyntaxCircus.Common` and
`SyntaxCircus.AspNetCore.Common`. It does **not** include:

- Converting sinforgiver's four stranded call sites (RevenueCat confirm,
  three Stripe sync actions in `ExternalProductsController`, and
  `FilesController.GetContent`'s storage-timeout case) to use `ApiResult`/
  `ApiResult<T>`. That is a separate follow-up in the sinforgiver repo once
  both packages ship a version containing this change, tracked from
  sinforgiver's `docs/architecture/DECISION-LOG.md` follow-up entry.
- Any change to `_template`'s usage-examples documentation (a possible
  future step once the pattern is proven in sinforgiver, per that repo's
  own architecture plan — not part of this package-level change).

## Non-goals

- No change to `Result`/`Result<T>`'s existing public API surface beyond
  removing `sealed` and widening constructor accessibility to `protected`.
- No attempt to make `ApiResult` carry an arbitrary custom failure payload
  (beyond `code`/`message`) — the brainstorming discussion explicitly
  decided a standardized `ProblemDetails` failure body is acceptable for
  all four known call sites, so there is no requirement to preserve
  bespoke response DTOs on failure.
- No success-side status code customization on `ApiResult`/`ApiResult<T>`
  — success status selection remains entirely the controller's
  responsibility via the `onSuccess` callback, unchanged from
  `Result`/`Result<T>` today.

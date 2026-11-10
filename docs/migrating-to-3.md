# Migrating to 3.0

ResultCrafter 3.0.0 is a breaking release. Package IDs are unchanged, except that `ResultCrafter.MediatR` is
discontinued. Coming from 1.x? Apply the 2.0 changes first (flattened namespaces, non-null success values); they are
listed in the 2.0.0 release notes on NuGet.

## 1. .NET 10 and later only

All packages target `net10.0` only. They run on .NET 10 and .NET 11. Apps on .NET 8 or 9 stay on ResultCrafter 2.x.

## 2. `Error` is a class

`Error` is now a `sealed class` instead of a `readonly struct`. It keeps value equality (`==`, `Equals`,
`GetHashCode` compare content). What changes in your code:

```csharp
// 2.x
if (!result.IsSuccess) return Result<OrderDto>.Fail(result.Error.Value);
var type = error.Value.Type; // when error was an Error?

// 3.0
if (!result.IsSuccess) return Result<OrderDto>.Fail(result.Error);
var type = error.Type;
```

`Fail(null)`, the implicit conversion from a null `Error`, and `ToProblemResult()` on null throw
`ArgumentNullException`. There is no `default(Error)` anymore. See [Design and performance](design.md#small-result-structs-one-error-object) for why.

## 3. `SuccessKind.None`, and default results throw

`SuccessKind` gained `None = 0`, and the other values moved up by one (`Ok = 1`, `Created = 2`, `Accepted = 3`,
`NoContent = 4`). Failures carry `None`. If you stored or compared the numeric values, update them.

A `default(Result<T>)` or `default(Result)` was never created by a factory. In 2.x it reported success:
`default(Result<int>).ToOkResult()` sent a 200 with body `0`. In 3.0, `IsSuccess`, `Map`, `Match` and every
`To*Result()` method throw `InvalidOperationException` on a default result. `ToString`, `Equals` and `GetHashCode`
still work, so logging and the debugger are safe.

## 4. `Result.AcceptedLocation` is now `Result.Location`

The void `Result` uses the same property name as `Result<T>`.

## 5. `Error.BadRequest` takes `IReadOnlyDictionary`

`Error.BadRequest(IReadOnlyDictionary<string, string[]> errors, string? detail = null)` replaces the `Dictionary`
overload. Passing a `Dictionary` still compiles; recompile against 3.0. `Error.TooManyRequests` gained an optional
`retryAfter` parameter, which also needs a recompile.

## 6. Results returned without mapping now throw

An endpoint that returns a `Result<T>`, `Result` or `Error` itself used to send it as JSON with status 200, even on
failure. It now fails loudly. This applies anywhere in a response body written with the app's JSON options, not
only to the endpoint's return value: a DTO property or collection of `Error`, `Result` or `Result<T>`, and
`TypedResults.Ok(result)` or `Results.Json(result)` all throw. Map such values to your own DTO first. See
[Always map results at the endpoint](guide.md#always-map-results-at-the-endpoint).

## 7. EF Core deadlocks and serialization failures are 500

`AddResultCrafterEfCore()` no longer answers 409 for a `DbUpdateConcurrencyException` caused by a serialization
failure (SQLSTATE `40001`) or a deadlock (`40P01`). Some providers, such as Npgsql for EF Core 11, report those as
`DbUpdateConcurrencyException`. They now reach the 500 handler. Real optimistic-concurrency conflicts stay 409.

## 8. New response shapes for framework 400s

These framework responses now get the house BadRequest shape (`title: "bad_request"`, the default `detail`, `errors`
keys that match your JSON). If a client matched on the framework title `One or more validation errors occurred.` or on
PascalCase keys, update it. See [Built-in validation and bad JSON bodies](guide.md#built-in-validation-and-bad-json-bodies).

- An unreadable JSON body on .NET 11, in Development and Production.
- Built-in Minimal API validation (`AddValidation()`).
- MVC's automatic `[ApiController]` model-validation 400.

## 9. Also in 3.0

- `WithValidation<T>()` no longer validates when parameter binding already failed, so the binding error is what the
  client sees.
- The `Produces*` builder extensions and attributes declare a `ProblemResponse` schema with
  `application/problem+json` instead of `ProblemDetails`, so OpenAPI documents show `traceId`, `requestId` and
  `errors`. Generated API clients get a new type name; for controllers the documented media type also changes from
  `application/json`.
- ResultCrafter writes its own error responses with its own `IProblemDetailsWriter`, first in line, always as
  `application/problem+json`. They keep the house shape whether `AddControllers()` runs before or after
  `AddResultCrafter()` and whatever the `Accept` header. A custom `IProblemDetailsWriter` of yours no longer sees
  them; your `CustomizeProblemDetails` callback still runs.
- `ToProblemResult()` and the void `Result` controller methods `ToNoContentResult()` and `ToAcceptedResult()` return
  `ActionResult` instead of `IActionResult`, so `return error.ToProblemResult();` works in an `ActionResult<T>`
  action. Recompile.
- `ResultCrafterLogger` and `ProblemDetailsEnricher` are internal now. The pipeline still enriches and logs every
  problem; nothing replaces them.
- `AddResultCrafterValidation()` registers internal validators too, and takes assemblies without a lifetime:
  `AddResultCrafterValidation(typeof(Program).Assembly)`. It is marked `[RequiresUnreferencedCode]`; under trimming or
  NativeAOT register each validator explicitly.
- `ValidateToResultAsync`'s token parameter is now named `cancellationToken` (was `ct`); only named-argument calls
  need a change.
- The 500 handler writes the status `ExceptionHandlerOptions.StatusCodeSelector` picks, so body and response agree.
- `Error.TooManyRequests` rejects a `retryAfter` above `int.MaxValue` seconds, which clients cannot read.
- New: `Error.With(key, DateTime)`, written as given. Before, a `DateTime` converted to `DateTimeOffset` and took the
  server's local offset.
- A bare framework 422 now gets the house RFC 9110 `type` URI instead of the RFC 4918 one, and `instance` is escaped
  (a space becomes `%20`).
- New: `Error.With()` for extra ProblemDetails members, and `Retry-After` for 429. See
  [Extra members and Retry-After](guide.md#extra-members-and-retry-after).
- `ResultCrafter.MediatR` is discontinued. See [MediatR is discontinued](#mediatr-is-discontinued).

## MediatR is discontinued

ResultCrafter.MediatR is discontinued in 3.0. It uses reflection, so it is not AOT-compatible. The owner's view: the
in-process mediator pattern is legacy for new apps on modern .NET. Minimal API endpoints that call plain services cover
most needs. Use ResultCrafter.MediatR 2.0.0 only with ResultCrafter.Core 2.x; it breaks at runtime against 3.0.

To replace its validation behavior, validate at the endpoint with `WithValidation<T>()`:

```csharp
app.MapPost("/orders", async (CreateOrderRequest req, OrderService svc, CancellationToken ct) =>
        (await svc.CreateAsync(req, ct)).ToCreatedResult())
    .WithValidation<CreateOrderRequest>();
```

or inside the service with `ValidateToResultAsync` from ResultCrafter.FluentValidation:

```csharp
public async Task<Result<OrderDto>> CreateAsync(CreateOrderRequest req, CancellationToken ct)
{
    var error = await validator.ValidateToResultAsync(req, ct);
    if (error is not null) return error;

    // happy path
}
```

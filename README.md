![ResultCrafter](https://raw.githubusercontent.com/HaikAsatryan/ResultCrafter/main/ResultCrafterLogHorizontal.png)

# ResultCrafter

A minimal, opinionated Result pattern library for **modern .NET (8+)**, with built-in **RFC 9457 ProblemDetails**,
structured logging, and first-class Minimal API support, plus full MVC controller support.

ResultCrafter ships as five focused NuGet packages under the `ResultCrafter.*` prefix and **multi-targets**
`net8.0`, `net9.0`, and `net10.0`.

---

## Table of Contents

1. [Packages](#packages)
2. [Installation](#installation)
3. [Getting Started](#getting-started)
4. [Demo: Every Scenario](#demo-every-scenario)
5. [Map: Transforming Results](#map-transforming-results)
6. [MVC Controller Support](#mvc-controller-support)
7. [FluentValidation Integration](#fluentvalidation-integration)
8. [MediatR + FluentValidation Pipeline](#mediatr--fluentvalidation-pipeline)
9. [EF Core Integration](#ef-core-integration)
10. [Configuration](#configuration)
11. [Performance](#performance)
12. [Limitations](#limitations)
13. [Alternatives](#alternatives)
14. [Why ResultCrafter](#why-resultcrafter)
15. [Roadmap](#roadmap)

---

## Packages

| Package                           | Purpose                                                                                                                                                        |
|-----------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `ResultCrafter.Core`              | The `Result<T>`, `Result`, `Error`, and `ErrorType` primitives. No framework dependencies.                                                                     |
| `ResultCrafter.AspNetCore`        | RFC 9457 ProblemDetails pipeline, `IExceptionHandler`, structured logging, Minimal API extensions, and MVC controller extensions.                              |
| `ResultCrafter.AspNetCore.EfCore` | Intercepts `DbUpdateConcurrencyException` and maps it to a 409 ProblemDetails response automatically.                                                          |
| `ResultCrafter.FluentValidation`  | Bridges `IValidator<T>` to `Error.BadRequest` with field-level error dictionaries.                                                                             |
| `ResultCrafter.MediatR`           | MediatR pipeline behaviors that run FluentValidation automatically for handlers returning `Result` / `Result<T>`, short-circuiting with structured 400 errors. |

All packages multi-target: **`net8.0`, `net9.0`, `net10.0`**.

---

## Installation

Install the packages you need via the .NET CLI:

```bash
dotnet add package ResultCrafter.Core
dotnet add package ResultCrafter.AspNetCore
dotnet add package ResultCrafter.AspNetCore.EfCore   # optional, EF Core users
dotnet add package ResultCrafter.FluentValidation    # optional, FluentValidation users
dotnet add package ResultCrafter.MediatR             # optional, MediatR validation pipeline behaviors
```

---

## Getting Started

Two lines in `Program.cs` is all it takes to get fully configured ProblemDetails, structured logging, and exception
handling:

```csharp
// Program.cs
builder.Services
    .AddResultCrafter()          // registers ProblemDetails, IExceptionHandler, logging
    .AddResultCrafterEfCore();   // optional: intercepts DbUpdateConcurrencyException

var app = builder.Build();

app.UseResultCrafter();          // registers UseExceptionHandler() + UseStatusCodePages()
```

From there, your service methods return `Result<T>` or `Result`, and your endpoint handlers convert them in one call:

```csharp
app.MapGet("/orders/{id:int}", async (int id, OrderService svc, CancellationToken ct) =>
    (await svc.GetAsync(id, ct)).ToOkResult());
```

---

## Demo: Every Scenario

### Building an Error in your service layer

```csharp
// Simple errors with an optional detail message
return Error.NotFound($"Order {id} does not exist.");
return Error.Unauthorized("A valid API key is required.");
return Error.Forbidden("Only admins can access this resource.");
return Error.Conflict($"An order named '{name}' already exists.");
return Error.ConcurrencyConflict("The order was modified by another request. Fetch and retry.");

// Plain 400 with a prose reason
return Error.BadRequest("At least one item ID must be provided.");

// 400 with structured field errors, same shape as ASP.NET Core model validation
return Error.BadRequest(new Dictionary<string, string[]>
{
    ["email"]    = ["Email is required.", "Email must be a valid address."],
    ["quantity"] = ["Quantity must be greater than 0."]
});
```

### Returning Results from your service

```csharp
return Result<OrderDto>.Ok(dto);                                    // 200
return dto;                                                         // 200 - implicit conversion
return Result<OrderDto>.Created($"/api/orders/{id}", dto);          // 201
return Result<OrderDto>.Accepted(dto, $"/api/orders/{id}/status");  // 202
return Result.NoContent();                                          // 204
return Result.Accepted();                                           // 202 void
return Error.NotFound($"Order {id} does not exist.");               // failure - implicit conversion
```

### Mapping to HTTP responses in Minimal API handlers

```csharp
// GET /orders/{id} -> 200 Ok<OrderDto> or 404 ProblemDetails
app.MapGet("/orders/{id:int}", async (int id, OrderService svc, CancellationToken ct) =>
    (await svc.GetAsync(id, ct)).ToOkResult())
    .ProducesNotFound();

// POST /orders -> 201 Created<OrderDto> or 400 ProblemDetails
app.MapPost("/orders", async (CreateOrderRequest req, OrderService svc, CancellationToken ct) =>
    (await svc.CreateAsync(req, ct)).ToCreatedResult())
    .ProducesBadRequest();

// PUT /orders/{id} -> 200 Ok<OrderDto> or 404 / 400 / 409 ProblemDetails
app.MapPut("/orders/{id:int}", async (int id, UpdateOrderRequest req, OrderService svc, CancellationToken ct) =>
    (await svc.UpdateAsync(id, req, ct)).ToOkResult())
    .ProducesNotFound()
    .ProducesBadRequest()
    .ProducesConflict();

// DELETE /orders/{id} -> 204 NoContent or 404 ProblemDetails
app.MapDelete("/orders/{id:int}", async (int id, OrderService svc, CancellationToken ct) =>
    (await svc.DeleteAsync(id, ct)).ToNoContentResult())
    .ProducesNotFound();

// POST /orders/{id}/process -> 202 Accepted<OrderDto>
app.MapPost("/orders/{id:int}/process", async (int id, OrderService svc, CancellationToken ct) =>
    (await svc.EnqueueProcessingAsync(id, ct)).ToAcceptedResult())
    .ProducesNotFound()
    .ProducesForbidden();

// POST /orders/bulk-cancel -> 202 Accepted (no body)
app.MapPost("/orders/bulk-cancel", async (BulkCancelRequest req, OrderService svc, CancellationToken ct) =>
    (await svc.BulkCancelAsync(req, ct)).ToAcceptedResult())
    .ProducesBadRequest();
```

> **A note on OpenAPI**: because ResultCrafter uses `TypedResults` on the success path, ASP.NET Core's OpenAPI source
> generator picks up success responses (200, 201, 202, 204) automatically with no extra annotation. Error responses are
> a different story. `ProblemHttpResult` is deliberately excluded from automatic inference, so each possible problem
> status code needs to be declared explicitly. That is what the `ProducesNotFound()`, `ProducesBadRequest()`,
> `ProducesConflict()` etc. extension calls are doing. They have no effect at runtime. They exist purely to populate
> the OpenAPI schema correctly.

### What the error response looks like

A 404 from `Error.NotFound("Order 42 does not exist.")` produces:

```json
{
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
    "status": 404,
    "title": "not_found",
    "detail": "Order 42 does not exist.",
    "instance": "/api/orders/42",
    "traceId": "00-abc123def456abc123def456abc123de-abc123def456abc1-00",
    "requestId": "0HN8K2MJ7F4QP:00000001"
}
```

A validation 400 from `Error.BadRequest(fieldErrors)` produces:

```json
{
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "status": 400,
    "title": "bad_request",
    "detail": "the_request_was_invalid_or_cannot_be_otherwise_served",
    "instance": "/api/orders",
    "errors": {
        "email": [
            "Email is required.",
            "Email must be a valid address."
        ],
        "quantity": [
            "Quantity must be greater than 0."
        ]
    },
    "traceId": "00-abc123def456abc123def456abc123de-abc123def456abc1-00",
    "requestId": "0HN8K2MJ7F4QP:00000002"
}
```

### Unhandled exception (500) and EF Core concurrency (409) demo endpoints

```csharp
// Throws an unhandled exception. ResultCrafterExceptionHandler logs it at Error
// and converts it to a sanitised 500 ProblemDetails (full detail in dev/staging).
app.MapGet("/items/crash", () =>
    throw new InvalidOperationException("Simulated unhandled exception - watch the logs."));

// Throws DbUpdateConcurrencyException. EfCoreHandler intercepts it as 409
// before the generic 500 handler ever sees it.
app.MapGet("/items/db-crash", () =>
    throw new DbUpdateConcurrencyException("Simulated EF Core conflict.", []));
```

The 500 response (sanitised in production):

```json
{
    "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
    "status": 500,
    "title": "internal_server_error",
    "detail": "an_unexpected_error_occurred",
    "instance": "/api/items/crash",
    "traceId": "00-abc123def456abc123def456abc123de-abc123def456abc1-00",
    "requestId": "0HN8K2MJ7F4QP:00000003"
}
```

---

## Map: Transforming Results

`Result<T>.Map<TOut>()` transforms the success value while preserving the `SuccessKind` and `Location`.
On the failure path, the error propagates unchanged without invoking the selector.

```csharp
// Without Map
public async Task<Result<OrderDto>> GetAsync(int id, CancellationToken ct)
{
    var result = await _repo.FindAsync(id, ct); // returns Result<Order>
    if (!result.IsSuccess)
        return Result<OrderDto>.Fail(result.Error!.Value);
    return Result<OrderDto>.Ok(result.Value!.ToDto());
}

// With Map
public async Task<Result<OrderDto>> GetAsync(int id, CancellationToken ct)
{
    var result = await _repo.FindAsync(id, ct);
    return result.Map(order => order.ToDto());
}
```

`Map` works with all success kinds. A `Created` result stays `Created` after mapping; its `Location` is preserved:

```csharp
var result = Result<Order>.Created("/api/orders/1", order);
var mapped = result.Map(o => o.ToDto()); // still Created, still /api/orders/1
```

> **Why only `Map`?** ResultCrafter intentionally does not ship `MapAsync`, `Bind`, `BindAsync`, or Railway-style
> chaining. In practice, most .NET service calls are `async`, so `Bind` alone is not enough and you end up
> needing the full `Map`/`MapAsync`/`Bind`/`BindAsync` matrix. The resulting code
> (`await (await repo.FindAsync(id, ct)).BindAsync(o => billing.ChargeAsync(o, ct))`) is harder to read than
> a simple `if (!result.IsSuccess) return ...` check. `Map` earns its place because the sync transform
> (entity to DTO) is nearly universal. Everything beyond that adds complexity without proportional readability
> gains.

---

## MVC Controller Support

> **ResultCrafter is Minimal API-first.** The controller integration is a fully working,
> well-tested feature, not an afterthought. But Minimal APIs remain the recommended path for
> new code. The controller support is here for teams with existing controller codebases who want
> ResultCrafter's error handling without a full migration.

### What you get

Controller endpoints using ResultCrafter produce exactly the same outcomes as Minimal API endpoints: the same RFC 9457
ProblemDetails shape, the same `instance` / `traceId` / `requestId` enrichment, the same structured 4xx logging, and
the same `IExceptionHandler` behaviour for 5xx errors. None of this needs to be wired separately.

### Extension methods

The method names mirror the Minimal API versions exactly. Only the return types differ.

```csharp
using ResultCrafter.AspNetCore.Controllers;

// Result<T> - returns ActionResult<T>
result.ToOkResult()        // 200 Ok or ProblemDetails
result.ToCreatedResult()   // 201 Created or ProblemDetails
result.ToAcceptedResult()  // 202 Accepted or ProblemDetails

// void Result - returns IActionResult
result.ToNoContentResult() // 204 NoContent or ProblemDetails
result.ToAcceptedResult()  // 202 Accepted or ProblemDetails

// bare Error - returns IActionResult
error.ToProblemResult()    // ProblemDetails directly
```

### OpenAPI attributes

Where Minimal API endpoints use builder extension methods (`.ProducesNotFound()`), controller actions use attributes.
ResultCrafter provides a matching set, each inheriting from `ProducesResponseTypeAttribute<ProblemDetails>`:

```csharp
using ResultCrafter.AspNetCore.Controllers;

[ProducesBadRequest]    // 400
[ProducesUnauthorized]  // 401
[ProducesForbidden]     // 403
[ProducesNotFound]      // 404
[ProducesConflict]      // 409
```

### Example controller

```csharp
using ResultCrafter.AspNetCore.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(OrderService svc) : ControllerBase
{
    [HttpGet("{id:int}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesNotFound]
    public async Task<ActionResult<OrderDto>> Get(int id, CancellationToken ct) =>
        (await svc.GetAsync(id, ct)).ToOkResult();

    [HttpPost]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    [ProducesBadRequest]
    public async Task<ActionResult<OrderDto>> Create([FromBody] CreateOrderRequest req, CancellationToken ct) =>
        (await svc.CreateAsync(req, ct)).ToCreatedResult();

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesNotFound]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        (await svc.DeleteAsync(id, ct)).ToNoContentResult();
}
```

No additional DI registration is required. `AddResultCrafter()` covers everything. Just add
`builder.Services.AddControllers()` and `app.MapControllers()` as you normally would for MVC.

---

## FluentValidation Integration

The `ResultCrafter.FluentValidation` package bridges your validators directly to `Error.BadRequest`:

```csharp
public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CustomerEmail)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid address.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than 0.");
    }
}
```

In your service:

```csharp
public async Task<Result<OrderDto>> CreateAsync(CreateOrderRequest req, CancellationToken ct)
{
    var error = await _validator.ValidateToResultAsync(req, ct);
    if (error is not null)
        return Result<OrderDto>.Fail(error.Value);

    // happy path
}
```

`ValidateToResultAsync` returns `null` on success and an `Error.BadRequest` with the full field errors dictionary on
failure. Property names are used as-is from FluentValidation. If you want a specific casing convention (for example,
camelCase), configure `ValidatorOptions.Global.PropertyNameResolver` globally in your composition root.

---

## MediatR + FluentValidation Pipeline

The `ResultCrafter.MediatR` package adds pre-built MediatR pipeline behaviors that automatically run all registered
FluentValidation validators before your handler executes.

It supports both handler shapes:

- `IRequest<Result<T>>`
- `IRequest<Result>`

If validation fails, the pipeline short-circuits and returns `Error.BadRequest(fieldErrors)` (wrapped in `Result<T>` or
`Result`), so your handlers only run on valid requests.

### Registration

```csharp
using FluentValidation.DependencyInjectionExtensions;
using ResultCrafter.MediatR;

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddResultCrafterValidation();
});
```

### Example: `Result<T>` handler

```csharp
public sealed record CreateOrderCommand(string CustomerEmail, int Quantity) : IRequest<Result<OrderDto>>;

public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerEmail)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid address.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than 0.");
    }
}

public sealed class CreateOrderHandler(OrderService svc)
    : IRequestHandler<CreateOrderCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        return await svc.CreateAsync(request, ct);
    }
}
```

### Example: void `Result` handler

```csharp
public sealed record CancelOrderCommand(int OrderId) : IRequest<Result>;

public sealed class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
    }
}

public sealed class CancelOrderHandler(OrderService svc)
    : IRequestHandler<CancelOrderCommand, Result>
{
    public async Task<Result> Handle(CancelOrderCommand request, CancellationToken ct)
    {
        return await svc.CancelAsync(request.OrderId, ct);
    }
}
```

### Behavior notes

- Validators run sequentially (intentionally safe for validators that depend on non-thread-safe services like EF Core
  `DbContext`).
- All validator failures are aggregated into one response.
- Handlers with no registered validators are a pass-through with effectively zero overhead beyond pipeline dispatch.

---

## EF Core Integration

Add `AddResultCrafterEfCore()` after `AddResultCrafter()` to automatically catch `DbUpdateConcurrencyException` anywhere
in your request pipeline:

```csharp
builder.Services
    .AddResultCrafter()
    .AddResultCrafterEfCore();
```

When EF Core detects an optimistic concurrency conflict, the exception is intercepted before it reaches the generic 500
handler, logged at the configured client error level, and converted to a 409 ConcurrencyConflict ProblemDetails
response. This works identically for both Minimal API and controller endpoints.

You can also return concurrency conflicts explicitly from service methods without relying on exception handling:

```csharp
if (entity.Version != request.Version)
    return Error.ConcurrencyConflict($"Order {id} was modified. Fetch the latest version and retry.");
```

---

## Configuration

All configuration is optional. The defaults are sensible for production use.

```csharp
builder.Services.AddResultCrafter(options =>
{
    // How much exception detail to include in 500 responses.
    // Auto (default): full detail in dev/test/staging, sanitized in production.
    // Sanitized: always sanitized.
    // IncludeExceptionDetails: always full detail (debug deployments only).
    options.ExceptionDetailMode = ExceptionDetailMode.Auto;

    // The detail string returned in sanitized 500 responses.
    options.DefaultServerErrorMessage = "an_unexpected_error_occurred";

    // The log level used for 4xx client errors produced by ResultCrafter.
    // Warning (default) is appropriate for most APIs.
    // Use Information to reduce noise in high-traffic services.
    // Use None to suppress client-error logging entirely.
    options.ClientErrorLogLevel = LogLevel.Warning;
});
```

### Environment detection for ExceptionDetailMode.Auto

When `ExceptionDetailMode` is `Auto`, ResultCrafter exposes full exception details if the environment name contains any
of: `dev`, `local`, `test`, `qa`, `stage`, `uat`, `preprod`, `sandbox`, `debug`. Everything else is treated as
production and sanitized. This check runs once at startup, not per request.

---

## Performance

Performance was a first-class concern from the start, not an afterthought.

### Structs on the hot path

`Result<T>`, `Result`, and `Error` are all `readonly struct` types. This avoids per-result heap allocations and
reduces garbage collector pressure on the success path.

### IExceptionHandler vs. custom middleware

ResultCrafter uses .NET's `IExceptionHandler` interface rather than a hand-written try/catch middleware. In
benchmarks, this was roughly 3x faster than a custom middleware implementation. A custom try/catch middleware wraps
every request in a try/catch block, adding overhead on the happy path. `IExceptionHandler` is invoked only after the
framework's own `ExceptionHandlerMiddleware` has caught an exception. On the 99% of requests that succeed, the
exception handling code is never entered.

### Source-generated logging

All log methods use `[LoggerMessage]` source generation. Log message templates are compiled at build time rather than
parsed at runtime. On the 4xx logging path, there is an explicit `IsEnabled` guard so that if your log level filters
out warnings, you pay zero allocation cost for those log calls.

### Per-request caching in ProblemDetails enrichment

The `instance` URI and W3C `traceId` are computed once per request and cached in `HttpContext.Items` using typed object
keys (reference-equality lookup, faster than string-key dictionaries).

### No reflection, no expression trees

There is no dynamic dispatch, no `Expression` compilation, and no reflection anywhere in the hot path. The mapping from
`ErrorType` to HTTP status code is a simple `switch` expression with no indirection.

---

## Limitations

### .NET 8 and above only

ResultCrafter requires .NET 8 or later. All packages multi-target `net8.0`, `net9.0`, and `net10.0`,
so the correct build is selected automatically.

The .NET 8 minimum is deliberate. It is the lowest version that ships `IExceptionHandler`,
`IProblemDetailsService`, and the ProblemDetails middleware pipeline that ResultCrafter builds on.
Supporting .NET 6 or 7 would require wrapping or reimplementing those primitives, which is out of scope.

---

## Alternatives

An honest comparison. All of these are good libraries; they solve different problems and prioritize different
trade-offs. This table is evaluated from the perspective of a Minimal API-first ASP.NET Core project.

> **Last reviewed: April 2026.** Library features and capabilities change over time. If you notice
> something outdated, please open an issue.

### At a glance

| Feature | ResultCrafter | Ardalis.Result | ErrorOr | FluentResults | OneOf | LanguageExt |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Result type** | readonly struct | class | readonly record struct | class | readonly struct | struct (`Fin<A>`) |
| **Zero-alloc success path** | Yes | No | Yes | No | Yes | Yes |
| **RFC 9457 ProblemDetails** | Yes | Under discussion | No | No | No | No |
| **IExceptionHandler pipeline** | Yes | No | No | No | No | No |
| **Structured logging ([LoggerMessage])** | Yes | No | No | No | No | No |
| **Minimal API extensions** | Yes (first-class) | Yes (added later) | No | No | No | No |
| **MVC controller extensions** | Yes | Yes (primary path) | No | No | No | No |
| **FluentValidation bridge** | Yes (separate pkg) | Yes (separate pkg) | No | No | No | No |
| **MediatR pipeline behaviors** | Yes (separate pkg) | No | No | No | No | No |
| **Map / functional helpers** | Map | Map, Bind, Railway | MatchFirst, Then, FailIf, Switch | Map, Bind, Merge, CheckIf | Match | Full FP (monads, optics, etc.) |
| **EF Core concurrency handler** | Yes (separate pkg) | No | No | No | No | No |
| **Min .NET version** | .NET 8 | .NET 6+ | .NET 7+ | .NET 6+ | .NET Standard 2.0 | .NET Standard 2.0 |
| **Multi-target (8/9/10)** | Yes | No | No | Yes (8/9) | No | No |
| **API surface** | Minimal | Medium | Small | Rich | Small | Very large |
| **Setup to first ProblemDetails** | 2 lines | Manual wiring | Manual wiring | Manual wiring | Manual wiring | Manual wiring |

### Short takes

**Ardalis.Result**: Battle-tested with ~8M NuGet downloads. The ASP.NET Core package exists but is MVC-first;
`ToMinimalApiResult()` was added later. ProblemDetails support has been discussed but is not shipped as a built-in
feature. Good choice if you need broad .NET version support or are already in an MVC codebase.

**ErrorOr**: Clean, small API with ~8M downloads. The `readonly record struct` design avoids heap allocations.
No first-party ASP.NET Core integration; you map errors to HTTP responses yourself. Good choice if you want a
lightweight discriminated error type without framework coupling.

**FluentResults**: The most downloaded pure Result library (~27M). Rich feature set with `Reasons`, metadata,
and extensive chaining. No web integration; logging is manual via an adapter. Good choice if you need flexible
error metadata and don't mind wiring the HTTP layer yourself.

**OneOf**: A general-purpose discriminated union (~56M downloads), not a Result library. No error/success
semantics, no HTTP mapping. Good choice if you need compile-time exhaustive matching on arbitrary type sets.

**LanguageExt**: A complete functional programming framework (~44M downloads). `Fin<A>` is the Result
equivalent, but adopting it means your team thinks in monads. No web integration. Good choice if you want
full FP in C#; overkill if you just want to stop throwing `NotFoundException`.

---

## Why ResultCrafter

After evaluating the alternatives above, none of them felt built for the way .NET APIs are written today: Minimal APIs,
`IExceptionHandler`, and `IProblemDetailsService`. Some were too heavy. Some had no ASP.NET Core pipeline integration.
ResultCrafter fills that gap.

This library has no commercial backing. I work on it in my own time because I think the .NET community deserves a
well-maintained, zero-bloat Result library that just works. I intend to keep it maintained for as long as I write
.NET code.

---

## Testing

ResultCrafter ships with a comprehensive test suite covering the core primitives, the ASP.NET Core pipeline integration,
the controller extensions, the FluentValidation bridge, and the MediatR behaviors. The test project is structured into
focused directories (`Core`, `AspNetCore`, `FluentValidation`, `MediatR`), each targeting the specific contracts of that
layer.

If you are contributing, the expectation is that new behavior ships with new tests.

---

## Roadmap

ResultCrafter has no fixed release schedule. Changes happen when they make the library better, not on a calendar.
If there is something you want next, open an issue. Community feedback is what drives prioritization.

---

## Contributing

Issues and pull requests are welcome. Please open an issue before starting significant work so we can discuss the
approach.

If ResultCrafter has helped you, a GitHub star goes a long way.

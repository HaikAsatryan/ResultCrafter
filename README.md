![ResultCrafter](https://raw.githubusercontent.com/HaikAsatryan/ResultCrafter/main/ResultCrafterLogHorizontal.png)

# ResultCrafter

A minimal, opinionated Result pattern library for modern .NET, with built-in RFC 9457 ProblemDetails, structured
logging, and first-class Minimal API support.

---

## Why I Built This

I already maintain several libraries under the [Pandatech](https://www.nuget.org/profiles/Pandatech) umbrella - most of
them solving specific infrastructure problems, built for production use at speed, and published publicly as a
convenience. They are primarily used internally, and they do not follow strict semver or breaking change policies; if
you adopt them, expect that they may change in ways that suit internal needs first.

ResultCrafter is different. It is a personal project - no company behind it, no deadlines, no commercial interest. I
spent a long time on every inch of it: the API design, the dependency choices, the documentation, the tests, the
pipeline integration. It is the most carefully crafted library I have written, and I intend to keep it that way.

I built ResultCrafter because, after evaluating many of the most-used and widely recommended Result pattern libraries in
the .NET ecosystem, none of them felt like they were built specifically for the way .NET APIs are written today, in
2026, with Minimal APIs and the modern `IExceptionHandler` interface. Some were too heavy. Some had no ASP.NET Core
pipeline integration at all. That's a bold claim, and the alternatives section below backs it up honestly.

This library is not a commercial product. I am not trying to sell anything. I work on this in my own time because I
think the .NET community deserves a well-maintained, zero-bloat Result library that just works. I intend to keep it
maintained for as long as I write .NET code.

---

## Table of Contents

1. [Exceptions vs. the Result Pattern](#exceptions-vs-the-result-pattern)
2. [Alternatives](#alternatives)
3. [Packages](#packages)
4. [Installation](#installation)
5. [Getting Started](#getting-started)
6. [Demo: Every Scenario](#demo-every-scenario)
7. [MVC Controller Support](#mvc-controller-support)
8. [FluentValidation Integration](#fluentvalidation-integration)
9. [MediatR + FluentValidation Pipeline](#mediatr--fluentvalidation-pipeline)
10. [EF Core Integration](#ef-core-integration)
11. [Configuration](#configuration)
12. [Performance](#performance)
13. [Limitations](#limitations)
14. [Roadmap](#roadmap)

---

## Exceptions vs. the Result Pattern

Before choosing ResultCrafter, it's worth understanding why you'd reach for a Result type at all instead of just
throwing exceptions. And to be upfront about the trade-off, because there genuinely is one.

### The honest trade-off

`Task<User>` is cleaner to read than `Task<Result<User>>`. That is simply true. The exception-based approach wins on
aesthetics - your method signatures stay lean, your service interfaces look uncluttered, and you can throw from anywhere
in a deeply nested call stack without changing a single method signature above it.

That cleanliness has a real cost though, and the cost compounds as your codebase grows.

### The case against exceptions as control flow

Exceptions were designed for truly unexpected situations: a network socket drops, a disk fills up, memory runs out. They
were not designed to communicate that a user typed a wrong password or that a record wasn't found in a database. Yet in
most .NET APIs, exceptions are used for exactly that, because it's the path of least resistance.

The problems this creates are real.

**Performance.** Throwing and catching an exception in .NET is expensive. The runtime has to capture the full stack
trace, walk the call stack looking for a matching catch block, and allocate memory for the exception object. In a busy
API where "user not found" and "invalid input" are completely normal, high-frequency outcomes, you are paying that cost
on every request.

**Lost context.** When an exception propagates up through the ASP.NET Core middleware pipeline, the HTTP context you
were working with can become unreliable. In some cases, particularly once a response has started, there is nothing you
can do to write a proper error response at all. The exception has torn the context out from under you.

**Invisible contracts.** A method that returns `User` tells you nothing about what happens when the user doesn't exist.
A method that returns `Result<User>` tells you explicitly: this can fail, and here are the ways it can fail. Callers are
forced to handle both paths. No surprises.

**try/catch pyramid in complex features.** In a modern API feature that calls multiple downstream services, you often
end up with nested try/catch blocks or a single broad catch that loses granularity. With a Result type, you compose
calls naturally without any try/catch at all.

### When exceptions still make sense

ResultCrafter does not pretend exceptions are always wrong. Truly unexpected failures - things you could not plan for
and cannot recover from - still belong in exceptions. ResultCrafter's `IExceptionHandler` integration is specifically
designed to catch these, log them properly, and convert them to a clean 500 ProblemDetails response. The two patterns
are complementary, not mutually exclusive.

The mental model is simple: **expected failures return a Result, unexpected failures throw an exception.**

---

## Alternatives

Here is an honest breakdown of several widely used alternatives, evaluated from the perspective of a Minimal API-first
ASP.NET Core project that wants built-in ProblemDetails mapping and structured logging.

### Ardalis.Result

A well-known library by Steve Smith (ardalis). It has been around for a long time and has over seven million total NuGet
downloads.

**Where it's strong:** Wide adoption, good documentation, supports both MVC controllers and Minimal APIs, and has a
FluentValidation companion package. If you need broad compatibility down to older .NET versions or need controller
support, it is a solid, battle-tested choice.

**Where it falls short for this use case:** The library's primary integration story is built around translating Results
to `ActionResult` types for MVC controllers, which is a pattern that is increasingly considered legacy. Its
`ToMinimalApiResult()` method was added later and is less integrated. There is no built-in structured logging: you wire
that yourself. There is no built-in exception handling pipeline: again, you wire that yourself. It also pulls in more
abstractions than you likely need, including `Map`, `Bind`, and Railway Oriented Programming helpers that make sense in
functional contexts but add cognitive load in a typical business API. The result type itself is a class, not a struct,
so every result allocation is a heap allocation.

### ErrorOr

A newer, stylish library that has gained real traction in the community. The syntax is clean and readable, and the
single-package approach is appealing.

**Where it's strong:** Ergonomic, small API surface, good community momentum, and probably the closest alternative to
ResultCrafter in spirit. The `MatchFirst`, `Then`, and `FailIf` extension methods make chaining operations feel natural.

**Where it falls short for this use case:** There is no first-party, batteries-included ASP.NET Core pipeline
integration in the core package. ProblemDetails mapping and logging can be added, but you wire that in yourself (or via
ecosystem extensions). To get an RFC 9457-compliant error response with structured log output, you write all of that
yourself on top of the library. For greenfield projects where you want everything wired and ready to go, that is a
meaningful gap.

### FluentResults

Probably the closest conceptually to ResultCrafter. It has a rich feature set and good documentation.

**Where it's strong:** Mature, flexible, and has a thoughtful design. The `Reasons` system for attaching structured
metadata to results is genuinely powerful.

**Where it falls short for this use case:** The richness becomes the problem. Fluent chaining with `Bind`, `Map`,
`Merge`, and `CheckIf` methods adds surface area that most APIs simply don't need. Logging integration exists but it is
manual: you call `result.Log()` explicitly and configure a logging adapter separately. There is no automatic structured
logging through `Microsoft.Extensions.Logging`. No ProblemDetails integration and no exception handling pipeline either.

### OneOf

A discriminated union library rather than a Result library specifically. It is genuinely useful for modelling complex
domain types where a value can be one of several things.

**Where it's strong:** If you need true discriminated unions in C# and want to be forced to handle every case at compile
time, `OneOf` is excellent. The syntax reads well: `OneOf<Success, NotFound, Forbidden>`.

**Where it falls short for this use case:** It is a general-purpose union type, not an API result abstraction. You get
no HTTP status mapping, no ProblemDetails, no logging, and no exception handling. You would essentially be building
everything in this library yourself on top of OneOf. The match syntax also becomes verbose in endpoint handlers.

### LanguageExt

An entire functional programming toolkit for C#. Monads, immutable collections, discriminated unions, optics, the works.

**Where it's strong:** If you want to write genuinely functional C# with proper effect types, LanguageExt is the most
complete option in the ecosystem by a wide margin.

**Where it falls short for this use case:** It is not a Result library. It is a functional language extension. The
learning curve is steep, the API surface is enormous, and adopting it in a typical business API usually means your
entire team needs to think in functional terms or the code becomes inconsistent. If you just want to stop throwing
`NotFoundException`, this is not the right tool.

### Pandatech.ResponseCrafter

An exception-driven library where you throw typed exceptions (`NotFoundException`, `BadRequestException`, etc.) and a
middleware pipeline catches, maps, and logs them as ProblemDetails. It works, but it carries the same trade-offs as any
exception-as-control-flow approach: performance cost, invisible failure contracts, and harder-to-test code.
ResultCrafter
was built to address exactly those issues. The two can coexist if you want exception handling for truly unexpected
errors alongside Result types for expected ones.

---

## Packages

| Package                           | Purpose                                                                                                                                                        |
|-----------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `ResultCrafter.Core`              | The `Result<T>`, `Result`, `Error`, and `ErrorType` primitives. No framework dependencies.                                                                     |
| `ResultCrafter.AspNetCore`        | RFC 9457 ProblemDetails pipeline, `IExceptionHandler`, structured logging, Minimal API extensions, and MVC controller extensions. Requires .NET 10+.           |
| `ResultCrafter.AspNetCore.EfCore` | Intercepts `DbUpdateConcurrencyException` and maps it to a 409 ProblemDetails response automatically.                                                          |
| `ResultCrafter.FluentValidation`  | Bridges `IValidator<T>` to `Error.BadRequest` with field-level error dictionaries.                                                                             |
| `ResultCrafter.MediatR`           | MediatR pipeline behaviors that run FluentValidation automatically for handlers returning `Result` / `Result<T>`, short-circuiting with structured 400 errors. |

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

## MVC Controller Support

> **ResultCrafter is Minimal API-first.** The controller integration is a fully working,
> well-tested feature, not an afterthought. But Minimal APIs remain the recommended path for
> new code. The controller support is here for teams with existing controller codebases who want
> ResultCrafter's error handling without a full migration.

### What you get

Controller endpoints using ResultCrafter produce exactly the same outcomes as Minimal API endpoints: the same RFC 9457
ProblemDetails shape, the same `instance` / `traceId` / `requestId` enrichment, the same structured 4xx logging, and
the same `IExceptionHandler` behaviour for 5xx errors. None of this needs to be wired separately.

This parity is not accidental. On the failure path, `ControllerResultExtensions` returns a `ProblemActionResult`, a
thin `ActionResult` subclass that routes through `IProblemDetailsService.TryWriteAsync` on execution, so the same
`ConfigureResultCrafterProblemDetails` post-configure callback fires for both paths.

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

All attributes have `AllowMultiple = true` and are picked up automatically by the OpenAPI tooling. No additional
configuration is required.

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

    [HttpPut("{id:int}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesNotFound]
    [ProducesBadRequest]
    [ProducesConflict]
    public async Task<ActionResult<OrderDto>> Update(int id, [FromBody] UpdateOrderRequest req, CancellationToken ct) =>
        (await svc.UpdateAsync(id, req, ct)).ToOkResult();

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

### Example request + handler (`Result<T>`)

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

public sealed class CreateOrderHandler : IRequestHandler<CreateOrderCommand, Result<OrderDto>>
{
    public Task<Result<OrderDto>> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        var dto = new OrderDto(123, request.CustomerEmail, request.Quantity);
        return Task.FromResult(Result<OrderDto>.Created($"/api/orders/{dto.Id}", dto));
    }
}
```

### Example request + handler (`Result`)

```csharp
public sealed record CancelOrderCommand(int OrderId) : IRequest<Result>;

public sealed class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
    }
}

public sealed class CancelOrderHandler : IRequestHandler<CancelOrderCommand, Result>
{
    public Task<Result> Handle(CancelOrderCommand request, CancellationToken ct) =>
        Task.FromResult(Result.NoContent());
}
```

### Behavior notes

- Validators run sequentially (intentionally safe for validators that depend on non-thread-safe services like EF Core `DbContext`).
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

`Result<T>`, `Result`, and `Error` are all `readonly struct` types. Using `readonly struct` avoids per-result object
allocations in the common path and reduces garbage collector pressure on the success path.

### IExceptionHandler vs. custom middleware

ResultCrafter uses .NET's `IExceptionHandler` interface rather than a hand-written try/catch middleware. In my
benchmarks, this was significantly faster (roughly 3x) than a custom middleware implementation. The reason is
straightforward: a custom try/catch middleware wraps **every single request** in a try/catch block, which adds overhead
on the happy path even when no exception occurs. `IExceptionHandler` is invoked only after the framework's own
`ExceptionHandlerMiddleware` has already caught an exception. On the 99% of requests that succeed, the exception
handling code is never entered at all.

### Source-generated logging

All log methods use `[LoggerMessage]` source generation. This means log message templates are compiled at build time
rather than parsed at runtime, and the `logger.IsEnabled(level)` check happens before any string or object allocations
for log parameters. On the 4xx logging path, there is an explicit `IsEnabled` guard so that if your log level is set to
filter out warnings, you pay zero allocation cost for those log calls.

### Per-request caching in ProblemDetails enrichment

The `instance` URI and W3C `traceId` are computed once per request and cached in `HttpContext.Items` using typed object
keys (reference-equality lookup, faster than string-key dictionaries). Repeated calls within the same request pipeline
hit the cache.

### Exception path is intentionally expensive

The 500 exception handler path does allocate and does do work. That is correct. You are paying the exception overhead
only when something genuinely unexpected happened, which should be rare. The 4xx Result path, which is frequent, is the
path that is optimized.

### No reflection, no expression trees

There is no dynamic dispatch, no `Expression` compilation, and no reflection anywhere in the hot path. The mapping from
`ErrorType` to HTTP status code is a simple `switch` expression evaluated at runtime with no indirection.

---

## Testing

ResultCrafter ships with a comprehensive test suite covering the core primitives, the ASP.NET Core pipeline integration,
the controller extensions, the FluentValidation bridge, and the MediatR behaviors. The test project is structured into
focused directories like `Core`, `AspNetCore`, `AspNetCore/Controllers`, `FluentValidation`, and `MediatR`, each
targeting the specific contracts of that layer.

Tests were written with the goal of catching real regressions, not just hitting coverage numbers. Every public contract
has tests. Every known edge case has a test. Every DI registration guard has a test. If you are contributing, the
expectation is that new behavior ships with new tests.

---

## Limitations

### .NET 10 and above only

ResultCrafter requires .NET 10 or later. This is a deliberate choice.

This is primarily a product scope and support decision, not because the core building blocks do not exist on earlier
runtimes. ASP.NET Core already provides the necessary primitives in earlier versions (including `IExceptionHandler` and
the ProblemDetails service), but ResultCrafter intentionally targets .NET 10+ to stay focused on a single, modern
integration path with no compatibility shims.

Supporting older runtimes would expand the test matrix, documentation surface, and maintenance burden. If you need
support for .NET 8 or 9 today, the alternatives listed above are good options.

---

## Roadmap

ResultCrafter has no fixed release schedule. Changes happen when they make the library better, not on a calendar.
If there is something you want next, open an issue. Community feedback is what drives prioritization.

---

## Contributing

Issues and pull requests are welcome. Please open an issue before starting significant work so we can discuss the
approach.

This project has no commercial backing and no roadmap driven by business requirements. Changes are driven by what makes
the library more useful, more correct, and more aligned with modern .NET practices.

If ResultCrafter has helped you, a GitHub star goes a long way.
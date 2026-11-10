# ResultCrafter guide

Everything ResultCrafter does, with examples. New here? The [README](../README.md) has the short tour.

## Contents

- [Setup](#setup)
- [Return errors and results](#return-errors-and-results)
- [Map results to HTTP](#map-results-to-http)
- [Exceptions and binding failures](#exceptions-and-binding-failures)
- [Extra members and Retry-After](#extra-members-and-retry-after)
- [Always map results at the endpoint](#always-map-results-at-the-endpoint)
- [Map: transforming results](#map-transforming-results) (and `Match`)
- [MVC controllers](#mvc-controllers)
- [FluentValidation](#fluentvalidation)
- [Endpoint filter validation](#endpoint-filter-validation)
- [Built-in validation and bad JSON bodies](#built-in-validation-and-bad-json-bodies)
- [C# unions](#c-unions)
- [EF Core](#ef-core)
- [Configuration](#configuration)
- [Limitations](#limitations)

## Setup

Two lines in `Program.cs` is all it takes to get fully configured ProblemDetails, structured logging, and exception
handling:

```csharp
// Program.cs
using ResultCrafter.AspNetCore;
using ResultCrafter.AspNetCore.EfCore;

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

## Return errors and results

### Errors

```csharp
using ResultCrafter;

// Simple errors with an optional detail message
return Error.NotFound($"Order {id} does not exist.");
return Error.Unauthorized("A valid API key is required.");
return Error.Forbidden("Only admins can access this resource.");
return Error.Conflict($"An order named '{name}' already exists.");
return Error.ConcurrencyConflict("The order was modified by another request. Fetch and retry.");
return Error.Gone($"Order {id} was archived and permanently removed.");
return Error.UnprocessableEntity("The order references a discontinued product.");
return Error.TooManyRequests("Rate limit exceeded.", TimeSpan.FromSeconds(60)); // Retry-After: 60

// Plain 400 with a prose reason
return Error.BadRequest("At least one item ID must be provided.");

// 400 with structured field errors, same shape as ASP.NET Core model validation
return Error.BadRequest(new Dictionary<string, string[]>
{
    ["email"]    = ["Email is required.", "Email must be a valid address."],
    ["quantity"] = ["Quantity must be greater than 0."]
});

// Extra members in the response body
return Error.Conflict("Seat already booked.").With("seatId", seatId).With("retryable", false);
```

### Results

```csharp
using ResultCrafter;

return Result<OrderDto>.Ok(dto);                                    // 200
return dto;                                                         // 200 - implicit conversion
return Result<OrderDto>.Created($"/api/orders/{id}", dto);          // 201
return Result<OrderDto>.Accepted(dto, $"/api/orders/{id}/status");  // 202
return Result.NoContent();                                          // 204
return Result.Accepted();                                           // 202 void
return Error.NotFound($"Order {id} does not exist.");               // failure - implicit conversion
```

All `Result<T>` success factories reject null values with `ArgumentNullException`. When success carries
no value, use the void `Result` (`Result.NoContent()` / `Result.Accepted()`) instead of a null payload.
Always create results through these factories: a `default(Result<T>)` is invalid and throws when read.

## Map results to HTTP

### Minimal API handlers

```csharp
using ResultCrafter.AspNetCore;

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

> **A note on OpenAPI**: because ResultCrafter uses `TypedResults` on the success path, ASP.NET Core's OpenAPI support
> picks up success responses (200, 201, 202, 204) automatically. Error responses must be declared, because
> `ProblemHttpResult` is excluded from automatic inference. That is what `ProducesNotFound()`, `ProducesBadRequest()`,
> `ProducesConflict()` etc. do. They have no effect at runtime. Each one declares the status code with an
> `application/problem+json` body of type `ProblemResponse` (namespace `ResultCrafter.AspNetCore.ProblemDetails`): the
> real ResultCrafter error body, including `traceId`, `requestId` and `errors`. `ProblemResponse` exists for OpenAPI
> only; ResultCrafter never creates it. The full set: `ProducesBadRequest()` (400), `ProducesUnauthorized()` (401),
> `ProducesForbidden()` (403), `ProducesNotFound()` (404), `ProducesConflict()` (409), `ProducesGone()` (410),
> `ProducesUnprocessableEntity()` (422), and `ProducesTooManyRequests()` (429).

### What the error response looks like

A 404 from `Error.NotFound("Order 42 does not exist.")` produces:

```json
{
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
    "title": "not_found",
    "status": 404,
    "detail": "Order 42 does not exist.",
    "instance": "/orders/42",
    "traceId": "00-abc123def456abc123def456abc123de-abc123def456abc1-00",
    "requestId": "0HN8K2MJ7F4QP:00000001"
}
```

A validation 400 from `Error.BadRequest(fieldErrors)` produces:

```json
{
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "title": "bad_request",
    "status": 400,
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

## Exceptions and binding failures

### Unhandled exceptions (500) and EF Core concurrency (409)

```csharp
using Microsoft.EntityFrameworkCore;

// Throws an unhandled exception. ResultCrafterExceptionHandler logs it at Error
// and converts it to a sanitised 500 ProblemDetails (full detail in dev/staging).
app.MapGet("/items/crash", () =>
{
    throw new InvalidOperationException("Simulated unhandled exception - watch the logs.");
});

// Throws DbUpdateConcurrencyException. The EF Core handler (AddResultCrafterEfCore()) intercepts it as 409
// before the generic 500 handler ever sees it.
app.MapGet("/items/db-crash", () =>
{
    throw new DbUpdateConcurrencyException("Simulated EF Core conflict.", []);
});
```

The 500 response (sanitised in production):

```json
{
    "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
    "title": "internal_server_error",
    "status": 500,
    "detail": "an_unexpected_error_occurred",
    "instance": "/items/crash",
    "traceId": "00-abc123def456abc123def456abc123de-abc123def456abc1-00",
    "requestId": "0HN8K2MJ7F4QP:00000003"
}
```

### Binding failures keep their real status

When Minimal API model binding fails (for example a required route/query/`[AsParameters]` value is missing or
unparseable), ASP.NET Core throws `BadHttpRequestException` in the Development environment, where
`RouteHandlerOptions.ThrowOnBadRequest` is enabled. ResultCrafter maps that exception to its carried status code,
typically `400` (or e.g. `413` for an oversized body), instead of letting it fall through to the generic 500 handler.
Your own `IExceptionHandler` must be registered before `AddResultCrafter()`: handlers run in registration order, and
ResultCrafter's 500 handler takes every exception that reaches it. If you set
`ExceptionHandlerOptions.StatusCodeSelector`, that handler writes the status it picks instead of 500.
In Production, where binding failures produce a clean 4xx without throwing, that same status flows through the
identical ProblemDetails pipeline via `UseStatusCodePages`. So the response is the same across environments: for
catalog-mapped statuses such as `400` you get the normalized house-style body (`title: "bad_request"`), not the
framework default. No configuration is required; this is part of `AddResultCrafter()`.

## Extra members and Retry-After

### `Error.With()`

Add extra members to the error body with `With(key, value)`. Each call returns a new `Error`; the original is
unchanged, and the same key again replaces the value.

```csharp
return Error.Conflict("Seat already booked.")
    .With("seatId", seatId)          // Guid
    .With("retryable", false)        // bool
    .With("alternatives", ["14A", "14C"]);
```

```json
{
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
    "title": "conflict",
    "status": 409,
    "detail": "Seat already booked.",
    "instance": "/api/bookings",
    "seatId": "6f9619ff-8b86-d011-b42d-00c04fc964ff",
    "retryable": false,
    "alternatives": ["14A", "14C"],
    "traceId": "00-abc123def456abc123def456abc123de-abc123def456abc1-00",
    "requestId": "0HN8K2MJ7F4QP:00000005"
}
```

- Value types: `string`, `int`, `long`, `bool`, `double`, `decimal`, `Guid`, `DateTimeOffset`, `DateTime` and
  `string[]`. A `DateTime` is written as given (`Z` for UTC, no offset for an unspecified kind). There is
  no `object` overload on purpose: these are the types ResultCrafter can always write as JSON, including under
  NativeAOT, where reflection-based JSON is off. A `double` must be finite.
- Keys are written exactly as given; your JSON naming policy is not applied.
- Reserved keys throw `ArgumentException` (compared case-insensitively): `type`, `title`, `status`, `detail`,
  `instance`, `errors`, `traceId`, `requestId`, `x-rc`, and anything starting with `x-rc-`.
- Read them back with `error.Extensions`. Equality includes them.

### Retry-After for 429

```csharp
return Error.TooManyRequests("Rate limit exceeded.", TimeSpan.FromSeconds(59.2));
```

```http
HTTP/1.1 429 Too Many Requests
Retry-After: 60
Content-Type: application/problem+json
```

The header value is whole seconds, rounded up, so a client never retries too early. `retryAfter` must be positive.
Without it, no header is sent. The value travels through the pipeline as an internal `x-rc-retry-after` marker that
never reaches the body.

## Always map results at the endpoint

An endpoint must turn a result into an HTTP result: `ToOkResult()` and friends for a `Result<T>` or `Result`,
`ToProblemResult()` for an `Error`. If an endpoint returns a `Result<T>`, `Result` or `Error` itself, ASP.NET Core
serializes it as JSON with status 200, even when it holds an error.

`AddResultCrafter()` blocks that mistake. It adds a JSON converter to the app's Minimal API and MVC JSON options that
throws `InvalidOperationException` with a message that names the fix. When you see it:

- **Minimal API**: the throw happens while ASP.NET Core builds the endpoint. If the app registers authorization
  services (for example `AddAuthorization()` or `AddControllers()`), endpoints are built at startup and the app does
  not start. Otherwise they are built on the first request, and every request then fails with a bare 500 with no body;
  the exception is in the server log. One such endpoint stops all endpoints, so the mistake shows up on the first run.
- **Controllers**: the action runs, then writing the response fails with a 500 ProblemDetails.
- **OpenAPI**: while any endpoint or action declares one of these types as its response, generating the OpenAPI
  document fails with a 500.

The converter fires wherever the app's HTTP JSON options meet one of these types, not only for an endpoint's return
value: a DTO property or a list of `Error`, `Result` or `Result<T>`, `TypedResults.Ok(result)` and
`Results.Json(result)` all throw. Map such values to your own DTO first.

The converter lives only in the app's HTTP JSON options. Code that serializes these types with its own
`JsonSerializerOptions` (a cache, a message bus) keeps working.

## Map: transforming results

`Result<T>.Map<TOut>()` transforms the success value while preserving the `SuccessKind` and `Location`.
On the failure path, the error propagates unchanged without invoking the selector.

```csharp
using ResultCrafter;

// Without Map
public async Task<Result<OrderDto>> GetAsync(int id, CancellationToken ct)
{
    var result = await _repo.FindAsync(id, ct); // returns Result<Order>
    if (!result.IsSuccess)
        return Result<OrderDto>.Fail(result.Error); // Error is non-null here ([MemberNotNullWhen])
    return Result<OrderDto>.Ok(result.Value.ToDto()); // Value is non-null here, no ! needed
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

`Match` handles both cases and returns one value. It runs `onSuccess` with the value or `onFailure` with the error:

```csharp
string text = result.Match(order => order.Name, error => error.ToString());

// Void Result: onSuccess takes no argument.
string status = deleted.Match(() => "deleted", error => error.Type.ToString());
```

Like `Map`, `Match` throws `InvalidOperationException` on a `default` result.

> **Why only `Map`?** ResultCrafter intentionally does not ship `MapAsync`, `Bind`, `BindAsync`, or Railway-style
> chaining. In practice, most .NET service calls are `async`, so `Bind` alone is not enough and you end up
> needing the full `Map`/`MapAsync`/`Bind`/`BindAsync` matrix. The resulting code
> (`await (await repo.FindAsync(id, ct)).BindAsync(o => billing.ChargeAsync(o, ct))`) is harder to read than
> a simple `if (!result.IsSuccess) return ...` check. `Map` earns its place because the sync transform
> (entity to DTO) is nearly universal. Everything beyond that adds complexity without proportional readability
> gains.

## MVC controllers

> **ResultCrafter is Minimal API-first.** The controller integration is a fully working,
> well-tested feature, not an afterthought. But Minimal APIs remain the recommended path for
> new code. The controller support is here for teams with existing controller codebases who want
> ResultCrafter's error handling without a full migration.

### What you get

Controller error responses are the same as Minimal API ones: the same RFC 9457 ProblemDetails shape, the same
`instance` / `traceId` / `requestId` enrichment, the same structured 4xx logging, and the same `IExceptionHandler`
behaviour for 5xx errors. None of this needs to be wired separately. Success responses follow MVC's own rules: content
negotiation and output formatters apply (a `string` value goes out as `text/plain`), and the MVC JSON options are used.

### Extension methods

The method names mirror the Minimal API versions exactly. Only the return types differ. Because the names are the
same, import `ResultCrafter.AspNetCore` or `ResultCrafter.AspNetCore.Mvc` in a file, not both (also not both as global
usings): with both, every `To*Result()` call is ambiguous (CS0121).

```csharp
using ResultCrafter.AspNetCore.Mvc;

// Result<T> - returns ActionResult<T>
result.ToOkResult()        // 200 Ok or ProblemDetails
result.ToCreatedResult()   // 201 Created or ProblemDetails
result.ToAcceptedResult()  // 202 Accepted or ProblemDetails

// void Result - returns ActionResult
result.ToNoContentResult() // 204 NoContent or ProblemDetails
result.ToAcceptedResult()  // 202 Accepted or ProblemDetails

// bare Error - returns ActionResult, so an early return works in an ActionResult<T> action
error.ToProblemResult()    // ProblemDetails directly
```

### OpenAPI attributes

Where Minimal API endpoints use builder extension methods (`.ProducesNotFound()`), controller actions use attributes.
ResultCrafter provides a matching set. Each declares its status code with an `application/problem+json` body of type
`ProblemResponse`:

```csharp
using ResultCrafter.AspNetCore.Mvc;

[ProducesBadRequest]            // 400
[ProducesUnauthorized]          // 401
[ProducesForbidden]             // 403
[ProducesNotFound]              // 404
[ProducesConflict]              // 409
[ProducesGone]                  // 410
[ProducesUnprocessableEntity]   // 422
[ProducesTooManyRequests]       // 429
```

Once an action has any response-type attribute, MVC stops inferring the success response for OpenAPI, and it can never
infer 201 or 202 from `ActionResult<T>`. Declare the success response yourself, for example
`[ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]`.

### Example controller

```csharp
using Microsoft.AspNetCore.Mvc;
using ResultCrafter.AspNetCore.Mvc;

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

## FluentValidation

The `ResultCrafter.FluentValidation` package bridges your validators directly to `Error.BadRequest`:

```csharp
using FluentValidation;

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
using ResultCrafter;
using ResultCrafter.FluentValidation;

public async Task<Result<OrderDto>> CreateAsync(CreateOrderRequest req, CancellationToken ct)
{
    var error = await _validator.ValidateToResultAsync(req, ct);
    if (error is not null)
        return Result<OrderDto>.Fail(error);

    // happy path
}
```

`ValidateToResultAsync` returns `null` on success and an `Error.BadRequest` with the full field errors dictionary on
failure. Property names are used as-is from FluentValidation. If you call it directly and want a specific casing
convention, configure `ValidatorOptions.Global.PropertyNameResolver` in your composition root. When you use the
[endpoint filter](#endpoint-filter-validation) below, key casing is aligned to your JSON settings for you.

## Endpoint filter validation

The `ResultCrafter.AspNetCore.FluentValidation` package turns the bridge above into a one-liner for Minimal API
endpoints. `WithValidation<T>()` adds an endpoint filter that resolves the registered `IValidator<T>`, validates the
`T` argument before your handler runs, and short-circuits with a 400 ProblemDetails on failure.

```csharp
using ResultCrafter.AspNetCore;
using ResultCrafter.AspNetCore.FluentValidation;

// Register your validators once. AddResultCrafterValidation() scans the calling assembly for public and
// internal validators and registers them as Scoped by default (matching FluentValidation); pass a lifetime
// and/or assemblies to override. (Plain FluentValidation registration such as AddValidatorsFromAssembly(...)
// works too.) Under trimming or NativeAOT, register each validator explicitly instead:
// builder.Services.AddScoped<IValidator<CreateOrderRequest>, CreateOrderRequestValidator>();
builder.Services.AddResultCrafterValidation();

// Per endpoint (WithValidation<T>() also documents the 400 response in OpenAPI):
app.MapPost("/orders", async (CreateOrderRequest req, OrderService svc, CancellationToken ct) =>
        (await svc.CreateAsync(req, ct)).ToCreatedResult())
    .WithValidation<CreateOrderRequest>();

// Or for every endpoint in a group:
var orders = app.MapGroup("/orders").WithValidation<CreateOrderRequest>();
orders.MapPost("/", async (CreateOrderRequest req, OrderService svc, CancellationToken ct) =>
    (await svc.CreateAsync(req, ct)).ToCreatedResult());
```

The failure response is the same RFC 9457 ProblemDetails shape as everywhere else, with the field-error keys aligned to
your app's configured JSON property-naming policy (camelCase by default for Minimal APIs) so they match the property
names on the wire, including nested (`address.street`) and indexed (`items[0].name`) paths. Only the naming policy is
applied: a property renamed with `[JsonPropertyName]` keeps its policy name in `errors`. For example:

```json
{
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "title": "bad_request",
    "status": 400,
    "detail": "the_request_was_invalid_or_cannot_be_otherwise_served",
    "instance": "/orders",
    "errors": {
        "customerEmail": ["Email is required."],
        "quantity": ["Quantity must be greater than 0."]
    },
    "traceId": "00-abc123def456abc123def456abc123de-abc123def456abc1-00",
    "requestId": "0HN8K2MJ7F4QP:00000004"
}
```

The filter is a transparent pass-through when the endpoint has no matching `T` argument, so it is safe to apply to a
whole group. If a `T` argument is present but no `IValidator<T>` is registered, it throws so the misconfiguration
surfaces immediately instead of silently skipping validation.

Endpoint filters also run after parameter binding failed (in Production, where binding failures do not throw). The
filter then skips validation and lets the binding 400 through, so a bad query string never hides behind the
validator's field errors, and the validator does not run for nothing.

## Built-in validation and bad JSON bodies

ResultCrafter also brings two framework 400 responses into the house shape. Both are part of `AddResultCrafter()`.

### Built-in validation (`AddValidation()`)

ASP.NET Core's own Minimal API validation (`builder.Services.AddValidation()` with Data Annotations) writes the title
`One or more validation errors occurred.`, no `detail`, and C# property names (`Name`). ResultCrafter rewrites it to
`title: "bad_request"`, the default `detail`, and keys that follow your JSON naming policy (`name`). The messages are
not changed. MVC's automatic `[ApiController]` 400 gets the same treatment, using the MVC JSON options. An application
`ValidationProblem` that keeps the framework title is normalized the same way; a title you set yourself is never
overwritten. The naming policy is applied to every key, so in a snake_case app a query parameter `pageSize` is
reported as `page_size`.

### Unreadable JSON body

On .NET 11, a JSON body that cannot be read (malformed JSON, or a value of the wrong type) reports which field failed.
ResultCrafter turns that into the house shape with fixed messages, identical in Development and Production:

```json
{
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "title": "bad_request",
    "status": 400,
    "detail": "the_request_was_invalid_or_cannot_be_otherwise_served",
    "instance": "/api/orders",
    "errors": {
        "quantity": ["invalid_value"]
    },
    "traceId": "00-abc123def456abc123def456abc123de-abc123def456abc1-00",
    "requestId": "0HN8K2MJ7F4QP:00000006"
}
```

Keys come from the JSON path: `$.quantity` becomes `quantity`, `$.address.zip` becomes `address.zip`,
`$.items[0].name` becomes `items[0].name`. Malformed JSON, and valid JSON that misses a `required` member, are
reported as `"_": ["invalid_json"]`. The framework's own
parser text, which names CLR types and byte positions, never reaches the client. On .NET 10 the framework does not
report the field, so the body is the generic `bad_request` without `errors`, in both environments.

## C# unions

C# 15 (.NET 11) adds unions. ResultCrafter's types are not unions, and that is deliberate. If you define your own
union that holds an `Error`, map it at the edge, inside the endpoint:

```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using ResultCrafter;
using ResultCrafter.AspNetCore;

public sealed record UserDto(int Id, string Name);

public union CreateUserResult(UserDto, Error);

app.MapPost("/users", Results<Created<UserDto>, ProblemHttpResult> (CreateUserRequest request, UserService users) =>
    users.Create(request) switch
    {
        UserDto user => TypedResults.Created($"/users/{user.Id}", user),
        Error error => error.ToProblemResult()
    });
```

The success case gives a 201 and the `Error` case the usual house ProblemDetails (a 409 for `Error.Conflict`).
Returning a union directly from an endpoint is not supported: ASP.NET Core would serialize it as JSON with status 200,
so with an `Error` case it fails like any [unmapped result](#always-map-results-at-the-endpoint).

## EF Core

Add `AddResultCrafterEfCore()` after `AddResultCrafter()` to automatically catch `DbUpdateConcurrencyException` anywhere
in your request pipeline:

```csharp
using ResultCrafter.AspNetCore;
using ResultCrafter.AspNetCore.EfCore;

builder.Services
    .AddResultCrafter()
    .AddResultCrafterEfCore();
```

When EF Core detects an optimistic concurrency conflict, the exception is intercepted before it reaches the generic 500
handler, logged at the configured client error level, and converted to a 409 ConcurrencyConflict ProblemDetails
response. This works identically for both Minimal API and controller endpoints.

Some providers (Npgsql for EF Core 11) also throw `DbUpdateConcurrencyException` for a serialization failure
(SQLSTATE `40001`) or a deadlock (`40P01`). Those are server-side failures, not a stale row the caller can refetch, so
when any inner exception is a `DbException` with one of those codes, the handler steps aside and the 500 handler takes
it.

You can also return concurrency conflicts explicitly from service methods without relying on exception handling:

```csharp
if (entity.Version != request.Version)
    return Error.ConcurrencyConflict($"Order {id} was modified. Fetch the latest version and retry.");
```

## Configuration

All configuration is optional. The defaults are sensible for production use.

```csharp
using ResultCrafter.AspNetCore;

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

When `ExceptionDetailMode` is `Auto`, ResultCrafter exposes full exception details if the environment is the standard
`Development` or `Staging` environment, or if the environment name contains any of: `dev`, `local`, `test`, `qa`,
`stage`, `uat`, `preprod`, `sandbox`, `debug`. Everything else is treated as production and sanitized. This check runs
once at startup, not per request. The match is on substrings, so a name such as `Latest` (contains `test`) or `DevOps`
(contains `dev`) also gets full details. Name production environments so they contain none of these words, or set
`ExceptionDetailMode.Sanitized` there.

## Limitations

### .NET 10 and later only

ResultCrafter targets `net10.0` and runs on .NET 10 and later; it is tested on .NET 10 and .NET 11. Apps on .NET 8 or 9
can stay on ResultCrafter 2.x.

### MVC controllers and unreadable JSON bodies

For MVC controllers, an unreadable JSON body still gets the framework's validation response: the framework title, a
key for the action parameter next to the JSON path key, and the parser's own message. Minimal APIs get the house shape
described in [Built-in validation and bad JSON bodies](#built-in-validation-and-bad-json-bodies).

### Extension value types

`Error.With()` accepts a fixed set of value types (see [Extra members and Retry-After](#extra-members-and-retry-after)).
There is no overload for nested objects.

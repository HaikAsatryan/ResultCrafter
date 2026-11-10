<h1 align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/resultcrafter-lockup-dark.svg">
    <img src="assets/resultcrafter-lockup-light.svg" alt="ResultCrafter" width="334" height="64">
  </picture>
</h1>

**Return errors. Don't throw them.** ResultCrafter gives your ASP.NET Core services a `Result<T>` and turns it into the
right HTTP response in one call. Errors leave as clean RFC 9457 ProblemDetails, the same shape every time.

[![NuGet](https://img.shields.io/nuget/v/ResultCrafter.AspNetCore?label=nuget)](https://www.nuget.org/packages/ResultCrafter.AspNetCore)
[![Downloads](https://img.shields.io/nuget/dt/ResultCrafter.AspNetCore)](https://www.nuget.org/packages/ResultCrafter.AspNetCore)
[![Build](https://img.shields.io/github/actions/workflow/status/HaikAsatryan/ResultCrafter/main.yml?branch=main&label=build)](https://github.com/HaikAsatryan/ResultCrafter/actions/workflows/main.yml)

## The idea

Exceptions are for surprises. A missing order is not a surprise. It is Tuesday. So the service returns it:

```csharp
public async Task<Result<OrderDto>> GetAsync(int id, CancellationToken ct)
{
    var order = await repo.FindAsync(id, ct);
    if (order is null)
        return Error.NotFound($"Order {id} does not exist.");

    return order.ToDto();
}
```

The endpoint maps it in one call:

```csharp
app.MapGet("/orders/{id:int}", async (int id, OrderService orders, CancellationToken ct) =>
    (await orders.GetAsync(id, ct)).ToOkResult());
```

The client gets `200` with the order, or this:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "not_found",
  "status": 404,
  "detail": "Order 42 does not exist.",
  "instance": "/orders/42",
  "traceId": "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-00",
  "requestId": "0HN8K2MJ7F4QP:00000001"
}
```

No try/catch in endpoints, no custom middleware, no hand-written error JSON.

## Install

```bash
dotnet add package ResultCrafter.AspNetCore
```

```csharp
builder.Services.AddResultCrafter();

var app = builder.Build();
app.UseResultCrafter();
```

That is the whole setup. It needs .NET 10 or later.

ResultCrafter ships only as `ResultCrafter.*` packages on nuget.org and as source in this repository. There is no ZIP
download: a "ResultCrafter" ZIP anywhere else is not from this project.

## What you get

- **The right status for every error.** Nine error types: 400, 401, 403, 404, 409 (conflict and concurrency), 410,
  422, and 429 with a `Retry-After` header.
- **One error shape.** Your errors, binding failures, built-in validation and unhandled exceptions all leave as the
  same ProblemDetails, with `traceId` and `requestId`.
- **Crashes handled.** An unhandled exception becomes a clean 500: full detail in Development, a plain message in
  Production.
- **Typed success.** `200`, `201` with `Location`, `202` and `204`. OpenAPI sees them, and `.ProducesNotFound()` and
  friends document the errors.
- **Logging done.** 4xx at Warning, 5xx at Error, source-generated.
- **Guard rails.** Return a `Result` from an endpoint without mapping it, and it fails loudly the first time it runs
  instead of sending your error with a cheerful 200.
- **Minimal APIs first, controllers welcome.** Same method names, same responses.
- **NativeAOT-safe.** Trim and AOT compatible, tested on .NET 10 and .NET 11 with a NativeAOT publish in CI.

## Packages

| Package                                     | Add it when                                                          |
|---------------------------------------------|----------------------------------------------------------------------|
| `ResultCrafter.AspNetCore`                  | Your API maps results to HTTP. Brings in Core.                       |
| `ResultCrafter.Core`                        | A project with no ASP.NET Core (a domain or service library) needs `Result<T>`, `Result` and `Error`. |
| `ResultCrafter.AspNetCore.EfCore`           | EF Core concurrency conflicts should answer 409.                     |
| `ResultCrafter.FluentValidation`            | You want a validator's failures as an `Error.BadRequest`.           |
| `ResultCrafter.AspNetCore.FluentValidation` | You want `.WithValidation<T>()` on Minimal API endpoints.           |

## What it will not do

ResultCrafter has `Map` and `Match`, and stops there. No `Bind`, no `BindAsync`, no railway. Most service calls are
async, and a chain of awaits reads worse than this:

```csharp
var order = await orders.GetAsync(orderId, ct);
if (!order.IsSuccess)
    return order.Error;

return await billing.ChargeAsync(order.Value, ct);
```

Everyone on your team can read an `if`.

## How it compares

Ardalis.Result, ErrorOr and FluentResults are good libraries. If you need an older .NET or a full functional toolkit,
pick one of them. ResultCrafter is narrower: it is built for the HTTP edge of a .NET 10+ API, and the error response,
the exception pipeline, the logging and the OpenAPI declarations come with it.
[The full comparison](docs/comparison.md) has the table.

## Learn more

- [Guide](docs/guide.md): every feature with examples, from errors and results to validation, EF Core and
  configuration.
- [Migrating to 3.0](docs/migrating-to-3.md): what changed from 2.x, and what to do about it.
- [How it compares](docs/comparison.md): the table against Ardalis.Result, ErrorOr, FluentResults, OneOf and
  LanguageExt.
- [Design and performance](docs/design.md): why the types look the way they do, and what a 404 costs: 5.2 µs per
  request returned, 16.6 µs thrown.
- [Contributing](CONTRIBUTING.md): build, test, and how to propose a change.

## Why I built it

None of the Result libraries I tried felt built for the way .NET APIs are written today: Minimal APIs,
`IExceptionHandler`, `IProblemDetailsService`. So I built the one I wanted. No company is behind it. I maintain it in
my own time, and I plan to for as long as I write .NET code.

If it saves you some boilerplate, a star helps others find it.

## License

[MIT](LICENSE)

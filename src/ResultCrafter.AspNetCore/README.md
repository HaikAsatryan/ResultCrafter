# ResultCrafter.AspNetCore

**Return errors. Don't throw them.** Turn a `Result<T>` from your service into the right HTTP response in one call.
Errors leave as clean RFC 9457 ProblemDetails, the same shape every time. For .NET 10 and later.

## Setup

```csharp
using ResultCrafter.AspNetCore;

builder.Services.AddResultCrafter();

var app = builder.Build();
app.UseResultCrafter();
```

## Use

The service returns what happened:

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

The client gets `200` with the order, or a `404` like this:

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

## What you get

- Nine error types with the right status: 400, 401, 403, 404, 409 (conflict and concurrency conflict), 410, 422,
  and 429 with `Retry-After`.
- One ProblemDetails shape for your errors, binding failures, built-in validation and unhandled exceptions.
- Typed success results (`200`, `201`, `202`, `204`) that OpenAPI sees, and `.ProducesNotFound()` and friends for
  the errors.
- Source-generated logging: 4xx at Warning, 5xx at Error.
- Minimal APIs first, MVC controllers supported (`ResultCrafter.AspNetCore.Mvc`).
- NativeAOT and trim compatible.

## Learn more

- [Guide](https://github.com/HaikAsatryan/ResultCrafter/blob/main/docs/guide.md)
- [Migrating to 3.0](https://github.com/HaikAsatryan/ResultCrafter/blob/main/docs/migrating-to-3.md)
- [Source and issues](https://github.com/HaikAsatryan/ResultCrafter)

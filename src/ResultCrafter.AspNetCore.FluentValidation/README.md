# ResultCrafter.AspNetCore.FluentValidation

`WithValidation<T>()` validates a Minimal API request with its FluentValidation validator before your handler runs,
and answers a 400 ProblemDetails when it fails. Part of [ResultCrafter](https://github.com/HaikAsatryan/ResultCrafter).
For .NET 10 and later.

## Setup

```csharp
using ResultCrafter.AspNetCore;
using ResultCrafter.AspNetCore.FluentValidation;

builder.Services
    .AddResultCrafter()
    .AddResultCrafterValidation();   // registers the public and internal validators in the calling assembly
```

`AddResultCrafterValidation()` scans assemblies, which is not trim-safe. In a trimmed or NativeAOT app, register each
validator explicitly: `builder.Services.AddScoped<IValidator<CreateOrderRequest>, CreateOrderRequestValidator>();`.

## Use

```csharp
app.MapPost("/orders", async (CreateOrderRequest req, OrderService orders, CancellationToken ct) =>
        (await orders.CreateAsync(req, ct)).ToCreatedResult())
    .WithValidation<CreateOrderRequest>();

// or for every endpoint in a group
var group = app.MapGroup("/orders").WithValidation<CreateOrderRequest>();
```

A failed request gets the house 400, with keys that follow your JSON naming policy, nested and indexed paths included:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "bad_request",
  "status": 400,
  "detail": "the_request_was_invalid_or_cannot_be_otherwise_served",
  "instance": "/orders",
  "errors": {
    "customerEmail": ["Email is required."],
    "items[0].quantity": ["Quantity must be greater than 0."]
  },
  "errorCodes": {
    "customerEmail": [{ "code": "NotEmptyValidator" }],
    "items[0].quantity": [{ "code": "GreaterThanValidator", "args": { "comparisonValue": 0 } }]
  }
}
```

- `errorCodes` carries each message's code and args at the same key and index, so a client can translate it. The code
  is the rule's `WithErrorCode` value, or FluentValidation's validator name; the attempted value is never sent.
- Endpoints without a `T` argument pass through, so the filter is safe on a whole group.
- A `T` argument with no registered `IValidator<T>` throws, so a missing validator shows up at once.
- If parameter binding already failed, the filter steps aside and the client sees the binding error.
- `WithValidation<T>()` also declares the 400 response in OpenAPI.

## Learn more

- [Endpoint filter validation in the guide](https://github.com/HaikAsatryan/ResultCrafter/blob/main/docs/guide.md#endpoint-filter-validation)
- [Source and issues](https://github.com/HaikAsatryan/ResultCrafter)

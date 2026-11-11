# ResultCrafter.FluentValidation

Turns FluentValidation failures into an `Error.BadRequest` with field errors, so a service can return them as a
result. No ASP.NET Core dependency. Part of [ResultCrafter](https://github.com/HaikAsatryan/ResultCrafter). For .NET 10
and later.

## Use

```csharp
using ResultCrafter;
using ResultCrafter.FluentValidation;

public async Task<Result<OrderDto>> CreateAsync(CreateOrderRequest req, CancellationToken ct)
{
    var error = await validator.ValidateToResultAsync(req, ct);
    if (error is not null)
        return error;

    // happy path
}
```

`ValidateToResultAsync` returns `null` when the request is valid. Otherwise it returns an `Error.BadRequest` whose
`errors` hold every failed field. At the HTTP edge that becomes a 400 ProblemDetails (shortened here):

```json
{
  "title": "bad_request",
  "status": 400,
  "errors": {
    "CustomerEmail": ["Email is required."],
    "Quantity": ["Quantity must be greater than 0."]
  },
  "errorCodes": {
    "CustomerEmail": [{ "code": "NotEmptyValidator" }],
    "Quantity": [{ "code": "GreaterThanValidator", "args": { "comparisonValue": 0 } }]
  }
}
```

`errorCodes` lets a client show each message in its own language: entry `i` of a field belongs to message `i`. The
code is the rule's `WithErrorCode` value, or FluentValidation's validator name when the rule sets none. The args are
the rule's limits (`minLength`, `maxLength` when the rule has a maximum, `from`, `to`, `expectedPrecision`,
`expectedScale`, and a `comparisonValue` that is neither a string nor another property). The value the caller sent is
never copied into them.

Property names are used as FluentValidation reports them. To validate Minimal API requests before the handler runs,
with keys that match your JSON naming, use
[ResultCrafter.AspNetCore.FluentValidation](https://www.nuget.org/packages/ResultCrafter.AspNetCore.FluentValidation).

## Learn more

- [FluentValidation in the guide](https://github.com/HaikAsatryan/ResultCrafter/blob/main/docs/guide.md#fluentvalidation)
- [Source and issues](https://github.com/HaikAsatryan/ResultCrafter)

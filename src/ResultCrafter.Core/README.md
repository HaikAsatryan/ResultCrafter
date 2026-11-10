# ResultCrafter.Core

The `Result<T>`, `Result` and `Error` types of ResultCrafter, with no ASP.NET Core dependency. Use it in domain and
service projects. For .NET 10 and later.

To turn results into HTTP responses, add
[ResultCrafter.AspNetCore](https://www.nuget.org/packages/ResultCrafter.AspNetCore) to the API project.

## Use

```csharp
using ResultCrafter;

public Result<OrderDto> Get(int id)
{
    var order = repo.Find(id);
    if (order is null)
        return Error.NotFound($"Order {id} does not exist.");

    return order.ToDto();
}
```

Each success says which HTTP answer it wants:

```csharp
Result<OrderDto>.Ok(dto);                              // 200 (or just return dto)
Result<OrderDto>.Created($"/orders/{id}", dto);        // 201 with Location
Result<OrderDto>.Accepted(dto, $"/orders/{id}/status"); // 202
Result.NoContent();                                    // 204
```

Errors come from factories: `BadRequest`, `Unauthorized`, `Forbidden`, `NotFound`, `Conflict`,
`ConcurrencyConflict`, `Gone`, `UnprocessableEntity` and `TooManyRequests`. Add extra members with
`Error.Conflict("Seat already booked.").With("seatId", seatId)`.

Transform a success value with `Map`, or handle both cases with `Match`:

```csharp
Result<OrderDto> dto = result.Map(order => order.ToDto());
string text = result.Match(order => order.Name, error => error.ToString());
```

`Result<T>` and `Result` are small `readonly struct` types, so the success path allocates nothing. Success values are
never null, and a `default` result throws instead of pretending to be a success.

## Learn more

- [Guide](https://github.com/HaikAsatryan/ResultCrafter/blob/main/docs/guide.md)
- [Migrating to 3.0](https://github.com/HaikAsatryan/ResultCrafter/blob/main/docs/migrating-to-3.md)
- [Source and issues](https://github.com/HaikAsatryan/ResultCrafter)

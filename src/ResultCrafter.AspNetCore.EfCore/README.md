# ResultCrafter.AspNetCore.EfCore

Answers EF Core optimistic-concurrency conflicts with a 409 ProblemDetails, for Minimal APIs and controllers alike.
Part of [ResultCrafter](https://github.com/HaikAsatryan/ResultCrafter). For .NET 10 and later.

## Setup

```csharp
using ResultCrafter.AspNetCore;
using ResultCrafter.AspNetCore.EfCore;

builder.Services
    .AddResultCrafter()
    .AddResultCrafterEfCore();
```

## What it does

A `DbUpdateConcurrencyException` anywhere in the request is caught before the generic 500 handler, logged as a client
error, and answered with a 409 `ConcurrencyConflict` ProblemDetails.

Some providers (Npgsql for EF Core 11) also throw `DbUpdateConcurrencyException` for a serialization failure
(SQLSTATE `40001`) or a deadlock (`40P01`). Those are server-side failures, not a stale row the caller can refetch, so
they stay 500.

You can also return the conflict yourself, without an exception:

```csharp
if (entity.Version != request.Version)
    return Error.ConcurrencyConflict($"Order {id} was modified. Fetch the latest version and retry.");
```

## Learn more

- [EF Core in the guide](https://github.com/HaikAsatryan/ResultCrafter/blob/main/docs/guide.md#ef-core)
- [Source and issues](https://github.com/HaikAsatryan/ResultCrafter)

# Design and performance

Performance was a first-class concern from the start, not an afterthought.

## Measured

.NET 11.0.0 on one laptop, BenchmarkDotNet: a 404 from a service three async calls deep, alone and inside a whole
`GET /orders/{id}` through TestServer. Method, fairness rules and raw output: [benchmarks](../benchmarks/README.md),
[run of 2026-11-11](../benchmarks/results/2026-11-11/).

| A 404 as                                  |             Alone |    Whole request |
|-------------------------------------------|------------------:|-----------------:|
| Exception, caught by `IExceptionHandler`  | 4,834 ns, 2,808 B | 16.6 µs, 19.6 KB |
| Exception, caught by try/catch middleware |     same as above | 15.9 µs, 17.4 KB |
| ResultCrafter `Result<T>`                 |       68 ns, 64 B |  5.2 µs, 12.4 KB |
| C# 15 union, mapped at the endpoint       |       19 ns, 24 B |  4.8 µs, 11.8 KB |
| `TypedResults.NotFound()`, no body        |                   |   3.3 µs, 9.1 KB |

A thrown 404 costs about 70 times a returned one on its own and about 3 times inside a request. On success every
variant costs 4.3 to 4.9 µs and 10.8 KB per request.

## Small result structs, one error object

`Result<T>` and `Result` are `readonly struct` types, so the success path allocates nothing (measured: 0 B). `Error` is
a sealed class: a result holds one reference to it instead of a copy of every error field. On 64-bit .NET, `Result` is
24 bytes (48 in 2.x) and `Result<string>` is 32 bytes (56 in 2.x). Results are copied each time they are returned or
passed, so the smaller size pays off on every call. Through three async methods a successful `Result<T>` still takes
about 50 ns longer than a plain object, likely because each `await` copies its 32 bytes where a plain object needs one
reference. That is about 1% of even an in-memory request. A failure allocates one 64-byte `Error` object, which is
cheap next to the HTTP error response it becomes, and a class also removes `default(Error)`, an invalid value that
skipped every factory check.

## NativeAOT

All packages are marked AOT- and trim-compatible, and CI publishes a NativeAOT smoke app on .NET 10 and .NET 11 that
calls its own endpoints. `Error.With()` values are written as `JsonElement`, which works with reflection-based JSON
turned off. One method is not AOT-safe and says so with `[RequiresUnreferencedCode]`: `AddResultCrafterValidation()`
scans assemblies, so a trimmed or NativeAOT app registers each validator explicitly instead.

## IExceptionHandler, not custom middleware

ResultCrafter plugs into ASP.NET Core's own exception pipeline through `IExceptionHandler` instead of adding a
middleware of its own. The framework's `ExceptionHandlerMiddleware` (added by `UseResultCrafter()`) already wraps the
request; ResultCrafter's handlers run only after it has caught an exception. A request that succeeds never enters
ResultCrafter's exception code. Handlers run in registration order and ResultCrafter's 500 handler takes every
exception that reaches it, so register your own `IExceptionHandler` before `AddResultCrafter()`.

This is a choice for fitting the framework, not for speed. Measured, a 404 thrown and handled by an
`IExceptionHandler` costs 16.6 µs per request and by a hand-written try/catch middleware 15.9 µs, with 2.2 KB less
allocated; on success the two cost the same. The middleware sat inside `UseResultCrafter()`, one frame closer to the
throw. Either way the exception costs about three times a returned `Result<T>` (5.2 µs).

## Source-generated logging

All log methods use `[LoggerMessage]` source generation. Log message templates are compiled at build time rather than
parsed at runtime. On the 4xx logging path, there is an explicit `IsEnabled` guard so that if your log level filters
out warnings, you pay zero allocation cost for those log calls. Measured: letting the 4xx warning through adds 0.4 µs
and 0.4 KB per 404, even into a provider that only formats the message and drops it; a filtered level pays none of it.

## Per-request caching in ProblemDetails enrichment

The `instance` URI and W3C `traceId` are computed once per request and cached in `HttpContext.Items` using typed object
keys (reference-equality lookup, faster than string-key dictionaries).

## No reflection, no expression trees

There is no dynamic dispatch, no `Expression` compilation, and no reflection anywhere in the hot path. The mapping from
`ErrorType` to HTTP status code is a simple `switch` expression with no indirection.

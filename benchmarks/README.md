# Benchmarks

What does an expected error, such as a 404, cost when you throw it versus when you return it? These benchmarks answer
that on .NET 11 with [BenchmarkDotNet](https://benchmarkdotnet.org/). Published results live in [`results/`](results/),
one folder per run, with the machine and runtime details next to the raw reports.

## What is measured

**Suite A, the error alone** (`ErrorPathBenchmarks`). An order lookup one or three async calls deep (`Depth`) that finds
the order or reports "not found" (`Found`), with no HTTP around it. Each layer is an `async ValueTask<T>` that completes
synchronously, so the numbers show the error mechanism, not thread-pool hops.

| Variant         | How "not found" travels                                  |
|-----------------|----------------------------------------------------------|
| `ThrowCatch`    | `throw new OrderNotFoundException()`, caught at the top  |
| `ResultCrafter` | `Error.NotFound(...)` returned through `Result<Order>`   |
| `Union`         | a C# 15 `union OrderLookup(Order, OrderNotFound)`        |
| `ArdalisResult` | `Result<Order>.NotFound(...)` (Ardalis.Result)           |
| `ErrorOr`       | `ErrorOr<Order>` with `Error.NotFound(...)` (ErrorOr)    |
| `FluentResults` | `Result.Fail<Order>(...)` (FluentResults)                |

**Suite B, the whole request** (`RequestBenchmarks`). `GET /orders/{id}` through TestServer, answering 200 or 404
(`Status`). The service is three async calls deep. One app per variant:

| Variant                    | App                                                                                  |
|----------------------------|--------------------------------------------------------------------------------------|
| `ExceptionHandler`         | the service throws; an `IExceptionHandler` writes the 404                            |
| `TryCatchMiddleware`       | the service throws; a try/catch middleware inside `UseResultCrafter()` writes it      |
| `ResultCrafter`            | the service returns `Result<Order>`; the endpoint calls `ToOkResult()`               |
| `Union`                    | the service returns the union; the endpoint maps it with `TypedResults.Problem`      |
| `Floor`                    | no ResultCrafter, no exception middleware: `TypedResults.NotFound()`, no body        |
| `ResultCrafterLogFiltered` | `ResultCrafter` with a logging provider at `Error`: the 4xx `Warning` is filtered out |
| `ResultCrafterLogEnabled`  | the same provider at `Warning`: the 4xx entry is formatted, then dropped              |

The program also prints `Unsafe.SizeOf` for `Result`, `Result<string>` and the union.

## Fairness rules

1. Every 404 except the floor goes through the same `AddResultCrafter()` ProblemDetails setup. Before measuring, each
   benchmark process sends one request to every app and stops if any status, content type or body differs from
   `ResultCrafter` (`traceId` and `requestId` ignored). The floor only has to match the status.
2. Logging is off in every app except the two `Log` rows, which share one provider and differ only in its level.
3. In Suite B the exception travels through the real ASP.NET Core frames: service, endpoint, middleware. The try/catch
   middleware sits inside `UseResultCrafter()`, because outside it ResultCrafter's catch-all 500 handler would take
   the exception first.
4. Published numbers come from one full default-job run per suite, with nothing else heavy running. The run keeps the
   machine's own Windows power plan (BenchmarkDotNet would switch to High performance otherwise).
5. On a CPU with fast and slow cores, every benchmark process is pinned to the fast cores. Otherwise each row lands
   on whichever core Windows picks, and rows differ by core, not by code.

## How to run

From the repository root, one command per suite:

```bash
dotnet run -c Release --project benchmarks/ResultCrafter.Benchmarks -- --filter "*ErrorPathBenchmarks*"
dotnet run -c Release --project benchmarks/ResultCrafter.Benchmarks -- --filter "*RequestBenchmarks*"
```

On a hybrid CPU add `--affinity <mask>` for the fast cores (`255`, CPUs 0 to 7, on the machine in `results/`). Add
`--job short` for a quick look while developing. Reports go to `BenchmarkDotNet.Artifacts/results/`; publish the
`-report-github.md` and `-report-brief.json` files under `results/<yyyy-MM-dd>/` with a README for the machine.

## What the numbers do not show

- This is not a load test: one request at a time, no concurrency, no throughput.
- A laptop, not a server. Compare rows within one run, not with numbers from another machine.
- TestServer skips sockets, Kestrel and HTTP parsing, so a real request costs more in every row alike.
- The lookups complete synchronously. With a real database call every row pays the same extra I/O time.

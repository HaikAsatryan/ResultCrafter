# How ResultCrafter compares

An honest comparison. All of these are good libraries; they solve different problems and prioritize different
trade-offs. This table is evaluated from the perspective of a Minimal API-first ASP.NET Core project.

> **Last reviewed: November 2026** for the other libraries. The ResultCrafter column reflects 3.0. Library features and
> capabilities change over time. If you notice something outdated, please open an issue.

## At a glance

| Feature                                  |                ResultCrafter                |            Ardalis.Result            |              ErrorOr              |         FluentResults         |                 OneOf                  |          LanguageExt           |
|------------------------------------------|:-------------------------------------------:|:------------------------------------:|:---------------------------------:|:-----------------------------:|:--------------------------------------:|:------------------------------:|
| **Result type**                          | readonly struct (`Error` is a sealed class) |                class                 |       readonly record struct      |             class             |            readonly struct             | readonly struct (`Fin<A>`, v4) |
| **Zero-alloc success path**              |                     Yes                     |                  No                  |                Yes                |               No              |                  Yes                   |              Yes               |
| **RFC 9457 ProblemDetails**              |                     Yes                     |       Partial (AspNetCore pkg)       |                 No                |               No              |                   No                   |               No               |
| **IExceptionHandler pipeline**           |                     Yes                     |                  No                  |                 No                |               No              |                   No                   |               No               |
| **Structured logging ([LoggerMessage])** |                     Yes                     |                  No                  |                 No                |               No              |                   No                   |               No               |
| **Minimal API extensions**               |              Yes (first-class)              |          Yes (added later)           |                 No                |               No              |                   No                   |               No               |
| **MVC controller extensions**            |                     Yes                     |          Yes (primary path)          |                 No                |     Preview (separate pkg)    |                   No                   |               No               |
| **FluentValidation bridge**              |              Yes (separate pkg)             |          Yes (separate pkg)          |                 No                |               No              |                   No                   |               No               |
| **Map / functional helpers**             |                  Map, Match                 |              Map, Bind               | Match, Then, Else, FailIf, Switch | Map, Bind, Merge, FailIf, Try |             Match, Switch              | Full FP (monads, optics, etc.) |
| **EF Core concurrency handler**          |              Yes (separate pkg)             |                  No                  |                 No                |               No              |                   No                   |               No               |
| **Min .NET version**                     |                   .NET 10                   | .NET Standard 2.0 (web pkg: .NET 6+) |         .NET Standard 2.0         |       .NET Standard 2.0       | .NET Standard 1.3 / .NET Framework 3.5 |       .NET Standard 2.0        |
| **API surface**                          |                   Minimal                   |                Medium                |               Small               |              Rich             |                 Small                  |           Very large           |
| **Setup to first ProblemDetails**        |                   2 lines                   |          Per call (partial)          |           Manual wiring           |         Manual wiring         |             Manual wiring              |         Manual wiring          |

## Measured

An order lookup three async calls deep that finds the order or reports "not found", on .NET 11.0.0 (one laptop,
BenchmarkDotNet; method and raw output in [benchmarks](../benchmarks/README.md)). Ardalis.Result 10.1.0, ErrorOr 2.1.1,
FluentResults 4.0.0; OneOf and LanguageExt were not measured.

| Returned through           |        Found |         Not found |
|----------------------------|-------------:|------------------:|
| ResultCrafter `Result<T>`  |   66 ns, 0 B |       68 ns, 64 B |
| Ardalis.Result `Result<T>` |  21 ns, 72 B |      22 ns, 104 B |
| ErrorOr `ErrorOr<T>`       |   44 ns, 0 B |       45 ns, 88 B |
| FluentResults `Result<T>`  | 36 ns, 208 B |      44 ns, 360 B |
| C# 15 union, no library    |   18 ns, 0 B |       19 ns, 24 B |
| Exception, no library      |   17 ns, 0 B | 4,834 ns, 2,808 B |

The allocations agree with the "Zero-alloc success path" row. Every result type reports "not found" 70 to 250 times
cheaper than an exception, and the differences between them are tens of nanoseconds: under 1% of even an in-memory
request (about 5 µs), and far less once sockets and a database are involved. ResultCrafter sits at the slow end of
that range, likely because its `Result<T>` is a 32-byte struct that each `await` copies, where the others pass less.

## Short takes

**Ardalis.Result**: Battle-tested with ~11.6M NuGet downloads. The ASP.NET Core package exists but is MVC-first;
`ToMinimalApiResult()` was added later. It returns `ProblemDetails` bodies for most error statuses, but not
consistently: Minimal API validation errors come back as a plain list, and some bodies omit `status` (open
issues #193 and #253). Good choice if you need broad .NET version support or are already in an MVC codebase.

**ErrorOr**: Clean, small API with ~13.4M downloads. The `readonly record struct` design avoids heap allocations on
the success path. No first-party ASP.NET Core integration; you map errors to HTTP responses yourself. An
`ErrorOr.AspNetCore` package is proposed for 3.0 but not shipped. Good choice if you want a lightweight
discriminated error type without framework coupling.

**FluentResults**: One of the most downloaded Result libraries (~39M). Rich feature set with `Reasons`, metadata,
and extensive chaining. The first-party `FluentResults.Extensions.AspNetCore` package (still marked preview) adds
`ToActionResult()` for MVC controllers only; by default a failure becomes a 400 with a list of messages, not
`ProblemDetails`. Logging goes through an `IResultLogger` you write and register. Good choice if you need flexible
error metadata and don't mind wiring the HTTP layer yourself.

**OneOf**: A general-purpose discriminated union (~81M downloads), not a Result library. No error/success
semantics, no HTTP mapping. Good choice if you need compile-time exhaustive matching on arbitrary type sets.

**LanguageExt**: A complete functional programming framework (~51M downloads). `Fin<A>` is the Result
equivalent, but adopting it means your team thinks in monads. No web integration. Version 5, in beta since 2024
and .NET 10 only, turns `Fin<A>` into a class. Good choice if you want full FP in C#; overkill if you just want to
stop throwing `NotFoundException`.

## Why ResultCrafter

After evaluating the alternatives above, none of them felt built for the way .NET APIs are written today: Minimal APIs,
`IExceptionHandler`, and `IProblemDetailsService`. Some were too heavy. Some had no ASP.NET Core pipeline integration.
ResultCrafter fills that gap.

This library has no commercial backing. I work on it in my own time because I think the .NET community deserves a
well-maintained, zero-bloat Result library that just works. I intend to keep it maintained for as long as I write
.NET code.

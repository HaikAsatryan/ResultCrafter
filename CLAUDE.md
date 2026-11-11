# CLAUDE.md

## Project overview

ResultCrafter is a minimal, opinionated Result pattern NuGet library for modern .NET (10+).
Five packages: `Core`, `AspNetCore`, `AspNetCore.EfCore`, `AspNetCore.FluentValidation`, `FluentValidation`
(`ResultCrafter.MediatR` was discontinued in 3.0). Libraries target `net10.0` only and run on .NET 10 and 11; do not
add a net11.0 library build. Current version: 3.1.0 (field error codes, released 2026-11-11). All five
packages share one `<Version>` and release together.

Public namespaces (flattened in 2.0): core primitives (`Result`, `Result<T>`, `Error`, `FieldError`, `ErrorType`,
`SuccessKind`) live in the root `ResultCrafter` namespace (files directly under `src/ResultCrafter.Core/`,
no subfolder). `ResultCrafter.AspNetCore` holds the Minimal API `To*Result` extensions, `Produces*` builder
extensions, DI, options, and exception handlers (files directly under `src/ResultCrafter.AspNetCore/`, no
subfolder). `ResultCrafter.AspNetCore.Mvc` holds the controller extensions, `ProblemActionResult`, and the
`Produces*Attribute` family (folder `src/ResultCrafter.AspNetCore/Mvc/`). `ResultCrafter.AspNetCore.ProblemDetails`
holds the public catalog, builder and keys, the OpenAPI-only `ProblemResponse`, and the internal enricher and logger
(folder
`src/ResultCrafter.AspNetCore/ProblemDetails/`). The remaining packages use their package ID as namespace. Code inside
any `ResultCrafter.*` namespace resolves core types without a `using` (parent namespace lookup).

Folders match namespaces throughout, and `dotnet_style_namespace_match_folder`/`IDE0130` is enabled to enforce it:
put a new file in the folder whose path equals its namespace suffix (root of the project for the flat namespace,
`Mvc/` or `ProblemDetails/` for those sub-namespaces). `ResultCrafter.Core` sets
`<RootNamespace>ResultCrafter</RootNamespace>` because its files sit at the project root under the flat `ResultCrafter`
namespace. Do not reintroduce organizational subfolders whose names are not part of the namespace.

## Build and test

```bash
dotnet build
dotnet test
bash tests/ResultCrafter.AotSmoke/run-aot-smoke.sh   # NativeAOT publish + self-test, both TFMs (heavy)
```

`global.json` pins the .NET 11 SDK. The test project targets `net10.0;net11.0` with per-TFM pins for TestHost,
EF Core and Microsoft.AspNetCore.OpenApi (test-only); every test runs on both runtimes, and `#if NET11_0_OR_GREATER`
is used only for C# union tests. `dotnet test` runs via Microsoft.Testing.Platform (configured through `global.json`
`test.runner` plus `UseMicrosoftTestingPlatformRunner` in the test project), not VSTest. `Directory.Build.targets`
adds `EnablePackageValidation`, `PackageValidationBaselineVersion` `3.1.0` (pack fails on a public API break
against 3.1.0) and `IsAotCompatible` for packable projects.

`tests/ResultCrafter.AotSmoke` is a `PublishAot` web app (`net10.0;net11.0`, `CreateSlimBuilder`, source-generated
JSON context) that starts Kestrel on `127.0.0.1:0`, calls its own endpoints and exits 0 or 1. Its runtimeconfig turns
reflection-based JSON off even in a normal build. `run-aot-smoke.sh` fails on publish warnings from ResultCrafter
code; third-party ones (FluentValidation) are printed only. Run the publish once, near the end of a change: it is heavy.

`benchmarks/ResultCrafter.Benchmarks` (net11.0, BenchmarkDotNet 0.16 preview because 0.15.8 cannot run .NET 11) is in
the slnx so CI compiles it; CI never runs it. One suite: `dotnet run -c Release --project
benchmarks/ResultCrafter.Benchmarks -- --filter "*RequestBenchmarks*" --affinity 255` (pin to the fast cores on this
hybrid-CPU laptop). Method and fairness rules in `benchmarks/README.md`; publish under `benchmarks/results/<date>/`.

CI runs on `ubuntu-latest` via GitHub Actions, with setup-dotnet installing `10.0.x` plus the global.json SDK.
There is no `dev` branch: work goes on short-lived branches and reaches `main` only through a pull request.
`pr.yml` runs build (`-warnaserror`) + test on pull requests to `main`, plus the AOT smoke job. `main.yml` runs the AOT
smoke job, then build, test, pack and publishes to NuGet on push to `main` through trusted publishing
(`NuGet/login@v1`, OIDC, `id-token: write`, `secrets.NUGET_USER`), keeping `--skip-duplicate`. Every merge to `main`
runs the publish; without a `<Version>` bump in `Directory.Build.props` the push is skipped as a duplicate.

## Code style

- 4-space indent (configured in `.editorconfig`).
- `var` everywhere. File-scoped namespaces.
- Primary constructors for DI.
- Braces required for multi-line blocks; single-line guard clauses (early `return`/`throw`) may omit them.
- Nullable enabled globally.
- `readonly struct` for `Result<T>` and `Result`; `Error` is a `sealed class` whose members cannot be reassigned
  (see below).
- Switch expressions over if-chains in catalog/mapping code.
- `[LoggerMessage]` source generation for all logging. Never use string interpolation in log calls.
- No reflection, no expression trees on the hot path.

## Architecture rules

- `Error` is a `sealed class` with a private constructor, factories only, and deep value equality
  (`IEquatable<Error>`, `==`, `!=`, `GetHashCode` over type, detail, errors, retry-after and extensions; `Equals` also
  compares field error codes and args). Why: a result holds one reference instead of a copy of every field (`Result`
  24 bytes, `Result<string>` 32 on 64-bit, down from 48 and 56 in 2.x), and there is no `default(Error)`. `Fail`,
  the implicit conversions and `ToProblemResult` throw `ArgumentNullException` on a null `Error`.
- `ErrorType.None = 0` is a sentinel. No `Error` factory produces it. `HttpErrorCatalog` throws
  `ArgumentOutOfRangeException` on `None`. Never add a mapping for `None`. The catalog spans nine error
  types: `BadRequest` (400), `NotFound` (404), `Conflict` (409), `Unauthorized` (401), `Forbidden` (403),
  `ConcurrencyConflict` (409), `Gone` (410), `UnprocessableEntity` (422), `TooManyRequests` (429; its type
  URI points at RFC 6585 section 4, the others at RFC 9110).
- `SuccessKind.None = 0` is a sentinel (`Ok = 1`, `Created = 2`, `Accepted = 3`, `NoContent = 4`). Failures carry
  `None`. A default result (no error, `Kind == None`) is invalid: `IsSuccess`, `Map`, `Match` and every `To*Result`
  throw `InvalidOperationException`; `ToString`, `DebuggerDisplay`, `Equals` and `GetHashCode` must not throw.
- `Result<T>` success values are non-null by construction: `Ok`, `Created`, and `Accepted` throw
  `ArgumentNullException` on a null value, and `Map` throws `InvalidOperationException` when its selector
  returns null. `IsSuccess` carries `[MemberNotNullWhen(true, nameof(Value))]` and
  `[MemberNotNullWhen(false, nameof(Error))]`. Do not weaken these guards or annotations.
- Both `Result<T>` and void `Result` carry a `SuccessKind` and a `Location`. ALL `To*Result()` extension methods,
  including `ToOkResult` and the typed `ToAcceptedResult`, on both the Minimal API and controller surfaces,
  validate that the `SuccessKind` matches the intended HTTP status and throw `InvalidOperationException`
  on mismatch. Do not remove these guards.
- `Error.With()` has typed overloads only (`string`, `int`, `long`, `bool`, `double` finite, `decimal`, `Guid`,
  `DateTimeOffset`, `DateTime`, `string[]`); no `object` overload, because with reflection-based JSON off (NativeAOT)
  other extension value types crash the error response. Keys are written as given; reserved keys (ProblemDetails
  members, `traceId`, `requestId`, `x-rc`, any `x-rc-` prefix) throw case-insensitively. Core stays free of
  System.Text.Json; `ProblemDetailsBuilder` converts each value to `JsonElement`.
- `FieldError` (message, optional code, args with the same typed `With` overloads as `Error`) feeds
  `Error.BadRequest(IReadOnlyDictionary<string, FieldError[]>)`; `Error.FieldErrors` reads them back and `Errors` still
  holds the messages. `Error` keeps both dictionaries behind one private reference, so field errors cost no extra
  field on every `Error`. An error built from message strings has no `FieldErrors` and equals one built from field
  errors without codes or args.
  `ProblemDetailsBuilder` writes `errors` unchanged and, when any entry has a code or args, an `errorCodes` member as a
  `JsonElement`: same keys, entry `i` for message `i`, `code` null when none, `args` left out when empty. `errorCodes`
  is not a reserved `With` key: an app's own member of that name replaces it.
- The FluentValidation bridge copies the failure's `ErrorCode` (null when blank) and only the allowlisted limit
  placeholders, renamed to camelCase (`ArgName` in `ValidatorExtensions`). Never copy `PropertyValue`, a value measured
  from it (`TotalLength`, `Digits`, `ActualScale`), display names, custom placeholders, or a `ComparisonValue` unless
  `ComparisonProperty` is empty and the value is not a string. Skip `MaxLength` `-1`: FluentValidation's "no maximum"
  marker (`MinimumLength`), not a limit. `ValidationFilter` re-keys `FieldErrors`, so codes stay with their messages.
- `AddResultCrafter()` registers `UnmappedResultJsonConverterFactory` in the Minimal API JSON options and MVC
  `JsonOptions` only, never as `[JsonConverter]` on the Core types (own `JsonSerializerOptions` must keep working).
  Its `CreateConverter` throws, so returning `Result<T>`/`Result`/`Error` from an endpoint fails instead of sending
  200, and no `MakeGenericType` is needed (AOT-safe).
- `ProblemActionResult` (controllers) and `EfCoreConcurrencyExceptionHandler` set the transport
  `Response.StatusCode` from the mapped error before calling the ProblemDetails service; the service
  serialises the body but never copies `pd.Status` onto the response. Keep the explicit assignment.
- `ProblemDetailsBuilder` sets the RFC 9110 `type` URI explicitly. Keep this in sync with `HttpErrorCatalog.TypeUri()`.
- `ConfigureResultCrafterProblemDetails` normalizes framework-default ProblemDetails to the house style so Development
  and Production match. Bare status codes and binding failures (title still the reason phrase or empty) go through
  `HttpErrorCatalog.TryGetErrorType`; a framework `type` URI (`https://tools.ietf.org/html/...`, RFC 4918 for 422) is
  replaced by the catalog one, a custom one is kept. A 400 `HttpValidationProblemDetails` that keeps the framework
  title gets the house BadRequest type/title/detail and: all keys start with `$` (unreadable JSON body, .NET 11) ->
  `"_": ["invalid_json"]` for `$`, `"<path>": ["invalid_value"]` for `$.<path>`; no key starts with `$` (built-in
  `AddValidation()`, MVC `[ApiController]`, app `ValidationProblem`) -> keys converted per segment with the
  endpoint's JSON naming policy, messages kept; mixed keys -> untouched. Never overwrite a title already set by a
  Result or the application.
- `x-rc`, `x-rc-error-id` and `x-rc-retry-after` are internal pipeline markers: the hook strips them before the
  response body is written (it moves `x-rc-retry-after` to the `Retry-After` header first). The
  `ProblemDetailsKeys` string constants stay public (an app's own `CustomizeProblemDetails` runs first and may branch
  on them), but never let the marker extensions leak into a written response. `AddResultCrafter()` inserts the internal
  `ResultCrafterProblemDetailsWriter` first in the writer list: it takes every problem carrying the `x-rc` marker and
  writes `application/problem+json`, so MVC's writer (when `AddControllers()` ran first) or a non-JSON `Accept` header
  can never skip the hook. Keep it first.
- `Error.TooManyRequests(detail, retryAfter)`: `retryAfter` must be positive; the header is whole seconds, rounded up.
- `GetInstance` returns `PathBase + Path` only (relative URI, escaped with `ToUriComponent()`). Never add host
  resolution.
- EF Core handler must be inserted before the generic 500 handler in the DI list. It returns false (500 handler takes
  it) when any inner exception is a `DbException` with `SqlState` `40001` or `40P01`. Add no other SqlState mappings.
- `BadHttpRequestExceptionHandler` (`ResultCrafter.AspNetCore`) is registered by `AddResultCrafter()` before the
  generic 500 handler and maps a 4xx `BadHttpRequestException` to its carried `StatusCode` (dev/prod parity for
  Minimal API binding failures). On a .NET 11+ runtime, a 400 with an inner `JsonException` becomes an
  `HttpValidationProblemDetails` keyed by the JSON path (gated on the runtime major version, because .NET 10
  Production returns the generic body). Keep it before the catch-all; non-4xx fall through to the 500 handler.
- `ResultCrafter.AspNetCore.FluentValidation` is the ASP.NET-Core-coupled bridge (`WithValidation<T>()` endpoint
  filter). `ValidationFilter<T>` skips validation and calls `next` when `Response.StatusCode` is already 400 on entry
  (in Production, filters run even after a parameter binding failure). It aligns `errors` keys to the app's JSON
  `PropertyNamingPolicy` per path segment through the shared internal `FieldKeyNaming` helper in
  `ResultCrafter.AspNetCore` (exposed via `InternalsVisibleTo`; keep its members unchanged within a major version,
  because an app may update `ResultCrafter.AspNetCore` alone); it must not mutate `ValidatorOptions.Global`.
  It passes through only when the endpoint has no non-null `TRequest` argument (a null-bound `TRequest` is
  indistinguishable from an absent argument and also passes through); if a non-null `TRequest` is present but no
  `IValidator<TRequest>` is registered it throws (fail-fast). Keep `ResultCrafter.FluentValidation`
  ASP.NET-Core-free.
- The `Produces*` builder extensions, `[Produces*]` attributes and `WithValidation<T>()` declare `ProblemResponse`
  with `application/problem+json`. No Microsoft.OpenApi dependency in shipped packages.
- `TreatWarningsAsErrors` is `false` in `Directory.Build.props`; nullable warnings are escalated to errors via
  `<WarningsAsErrors>$(WarningsAsErrors);Nullable</WarningsAsErrors>`. The CI workflows build with
  `-warnaserror`, so any warning fails the gate even though local builds only warn. No third-party analyzers
  are referenced. All CS1591 (missing XML doc) on public members in `src/` projects should still be fixed before
  merging.
- Test, demo and AOT smoke projects set `<IsPackable>false</IsPackable>` and
  `<GenerateDocumentationFile>false</GenerateDocumentationFile>`.

## Adding a new ErrorType

1. Add the enum value in `ErrorType.cs` (with an explicit int value).
2. Add a factory method on `Error`.
3. Add `Status`, `Title`, `DefaultDetail`, and `TypeUri` cases in `HttpErrorCatalog` (and a `TryGetErrorType`
   reverse-map case if a bare response with that status should be normalized to the house style).
4. Add a `ProducesXxx` attribute in `Mvc/ControllersAttributes.cs`
   (`ProducesResponseTypeAttribute<ProblemResponse>(status, "application/problem+json")`).
5. Add a `ProducesXxx` builder extension in `RouteHandlerBuilderExtensions.cs` (`Produces<ProblemResponse>`).
6. Add tests for the new type in `HttpErrorCatalogTests`, `ProblemDetailsBuilderTests`, and the
   `ControllerResultAttributesTests` and `RouteHandlerBuilderExtensionsTests` theories.

## Conventions

- No git commit or push without explicit instruction.
- `FluentValidation` PropertyName normalisation uses `string.IsNullOrWhiteSpace`.
- Demo project (`tests/ResultCrafter.Demo/`, net10.0) uses `ConcurrentDictionary` and `Interlocked.Increment` for
  thread safety since `ItemService` is registered as Singleton. Keep it building and in sync with `docs/guide.md`.
- Public docs: `README.md` is the short GitHub page (HTML allowed, relative links). Full reference lives in `docs/`
  (`guide.md`, `migrating-to-3.md`, `comparison.md`, `design.md`, `brand.md`). Each `src/<Package>/README.md` is that
  package's nuget.org README: no raw HTML (nuget.org shows it as text) and absolute links only (it blanks relative
  ones). A behavior change updates the guide, and the package README when its example changes. `llms.txt` is a short
  link index and `context7.json` configures Context7; keep both in sync when docs files move.
- Brand files (mark, NuGet icon, lockups, social card) are generated by `assets/build.py`; never edit the outputs by
  hand. Rules in `docs/brand.md`. `ResultCrafterLogHorizontal.png` stays at the root because the 2.0.0 NuGet pages
  load it from there.
- In a project that calls `AddValidation()`, the .NET 11 validation generator (RC3 and 11.0.0 GA) treats any
  `Map(delegate)` call as an endpoint and warns (ASP0034) when the delegate's parameter type is generic; call
  `Result<T>.Map` with a concrete type there (see `DefaultResultTests`).

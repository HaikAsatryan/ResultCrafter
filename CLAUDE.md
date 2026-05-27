# CLAUDE.md

## Project overview

ResultCrafter is a minimal, opinionated Result pattern NuGet library for modern .NET (8+).
Six packages: `Core`, `AspNetCore`, `AspNetCore.EfCore`, `AspNetCore.FluentValidation`, `FluentValidation`, `MediatR`.
Multi-targets `net8.0`, `net9.0`, `net10.0`.

## Build and test

```bash
dotnet build
dotnet test
```

CI runs on `ubuntu-latest` via GitHub Actions. The `main.yml` workflow publishes to NuGet on push to `main`.
The `pr.yml` workflow runs build + test on pull requests to `dev` and `main`.

## Code style

- 4-space indent (configured in `.editorconfig`).
- `var` everywhere. File-scoped namespaces.
- Primary constructors for DI.
- Braces required for multi-line blocks; single-line guard clauses (early `return`/`throw`) may omit them.
- Nullable enabled globally.
- `readonly struct` for `Result<T>`, `Result`, and `Error`.
- Switch expressions over if-chains in catalog/mapping code.
- `[LoggerMessage]` source generation for all logging. Never use string interpolation in log calls.
- No reflection, no expression trees on the hot path.

## Architecture rules

- `ErrorType.None = 0` is a sentinel. All `Error` factory methods reject it. `HttpErrorCatalog` throws
  `ArgumentOutOfRangeException` on `None`. Never add a mapping for `None`.
- Both `Result<T>` and void `Result` carry a `SuccessKind` field. The `To*Result()` extension methods
  validate that the `SuccessKind` matches the intended HTTP status. Do not remove these guards.
- `ProblemDetailsBuilder` sets the RFC 9110 `type` URI explicitly. Keep this in sync with `HttpErrorCatalog.TypeUri()`.
- `ConfigureResultCrafterProblemDetails` normalizes framework-default ProblemDetails (binding failures, and bare
  status codes via `UseStatusCodePages`) to the house style via `HttpErrorCatalog.TryGetErrorType`, only when the
  title is still the framework default. This keeps Development (handler) and Production (status-code path) responses
  identical. Never overwrite a title already set by a Result or the application.
- `x-rc` and `x-rc-error-id` are internal pipeline markers stripped before the response is written.
  Do not expose them as public API.
- `GetInstance` returns `PathBase + Path` only (relative URI). Never add host resolution.
- EF Core handler must be inserted before the generic 500 handler in the DI list.
- `BadHttpRequestExceptionHandler` (`ResultCrafter.AspNetCore`) is registered by `AddResultCrafter()` before the
  generic 500 handler and maps a 4xx `BadHttpRequestException` to its carried `StatusCode` (dev/prod parity for
  Minimal API binding failures). Keep it before the catch-all; non-4xx fall through to the 500 handler.
- `ResultCrafter.AspNetCore.FluentValidation` is the ASP.NET-Core-coupled bridge (`WithValidation<T>()` endpoint
  filter). `ValidationFilter<T>` aligns `errors` keys to the app's JSON `PropertyNamingPolicy` per path segment;
  it must not mutate `ValidatorOptions.Global`. It passes through only when the endpoint has no `TRequest` argument;
  if a `TRequest` is present but no `IValidator<TRequest>` is registered it throws (fail-fast). Keep
  `ResultCrafter.FluentValidation` ASP.NET-Core-free.
- `TreatWarningsAsErrors` is `false` in `Directory.Build.props`; nullable warnings are escalated to errors via
  `<WarningsAsErrors>$(WarningsAsErrors);Nullable</WarningsAsErrors>`. No third-party analyzers are referenced.
  All CS1591 (missing XML doc) on public members in `src/` projects should still be fixed before merging.
- Test and demo projects set `<IsPackable>false</IsPackable>` and
  `<GenerateDocumentationFile>false</GenerateDocumentationFile>`.

## Adding a new ErrorType

1. Add the enum value in `ErrorType.cs` (with an explicit int value).
2. Add a factory method on `Error`.
3. Add `Status`, `Title`, `DefaultDetail`, and `TypeUri` cases in `HttpErrorCatalog` (and a `TryGetErrorType`
   reverse-map case if a bare response with that status should be normalized to the house style).
4. Add a `ProducesXxx` attribute in `ControllersAttributes.cs`.
5. Add a `ProducesXxx` builder extension in `RouteHandlerBuilderExtensions.cs`.
6. Add tests for the new type in `HttpErrorCatalogTests`, `ProblemDetailsBuilderTests`, and
   `ControllerResultAttributesTests`.

## Conventions

- No git commit or push without explicit instruction.
- `MediatR` and `FluentValidation` PropertyName normalisation uses `string.IsNullOrWhiteSpace` consistently.
- Demo project (`tests/ResultCrafter.Demo/`) uses `ConcurrentDictionary` and `Interlocked.Increment` for
  thread safety since `ItemService` is registered as Singleton.

# CLAUDE.md

## Project overview

ResultCrafter is a minimal, opinionated Result pattern NuGet library for modern .NET (8+).
Six packages: `Core`, `AspNetCore`, `AspNetCore.EfCore`, `AspNetCore.FluentValidation`, `FluentValidation`, `MediatR`.
Multi-targets `net8.0`, `net9.0`, `net10.0`. Current version: 2.0.0 (breaking release).

Public namespaces (flattened in 2.0): core primitives (`Result`, `Result<T>`, `Error`, `ErrorType`,
`SuccessKind`) live in the root `ResultCrafter` namespace (files directly under `src/ResultCrafter.Core/`,
no subfolder). `ResultCrafter.AspNetCore` holds the Minimal API `To*Result` extensions, `Produces*` builder
extensions, DI, options, and exception handlers (files directly under `src/ResultCrafter.AspNetCore/`, no
subfolder). `ResultCrafter.AspNetCore.Mvc` holds the controller extensions, `ProblemActionResult`, and the
`Produces*Attribute` family (folder `src/ResultCrafter.AspNetCore/Mvc/`). `ResultCrafter.AspNetCore.ProblemDetails`
holds the catalog/builder/enricher utilities (folder `src/ResultCrafter.AspNetCore/ProblemDetails/`). The remaining
packages use their package ID as namespace. Code inside any `ResultCrafter.*` namespace resolves core types without
a `using` (parent namespace lookup).

Folders match namespaces throughout, and `dotnet_style_namespace_match_folder`/`IDE0130` is enabled to enforce it:
put a new file in the folder whose path equals its namespace suffix (root of the project for the flat namespace,
`Mvc/` or `ProblemDetails/` for those sub-namespaces). `ResultCrafter.Core` sets
`<RootNamespace>ResultCrafter</RootNamespace>` because its files sit at the project root under the flat `ResultCrafter`
namespace. Do not reintroduce organizational subfolders whose names are not part of the namespace.

## Build and test

```bash
dotnet build
dotnet test
```

`dotnet test` runs via Microsoft.Testing.Platform (configured through `global.json` `test.runner` plus
`UseMicrosoftTestingPlatformRunner` in the test project), not VSTest. `Directory.Build.targets` adds
`EnablePackageValidation` and `IsAotCompatible` for packable projects; the `MediatR` package opts out of AOT.
`PackageValidationBaselineVersion` is intentionally absent while 2.0.0 is unreleased (2.0.0 is a deliberate
breaking release, so no baseline comparison against 1.x). After 2.0.0 ships, set it to `2.0.0` in
`Directory.Build.targets` so subsequent releases are validated against it.

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
  `ArgumentOutOfRangeException` on `None`. Never add a mapping for `None`. The catalog spans nine error
  types: `BadRequest` (400), `NotFound` (404), `Conflict` (409), `Unauthorized` (401), `Forbidden` (403),
  `ConcurrencyConflict` (409), `Gone` (410), `UnprocessableEntity` (422), `TooManyRequests` (429; its type
  URI points at RFC 6585 section 4, the others at RFC 9110).
- `Result<T>` success values are non-null by construction: `Ok`, `Created`, and `Accepted` throw
  `ArgumentNullException` on a null value, and `Map` throws `InvalidOperationException` when its selector
  returns null. `IsSuccess` carries `[MemberNotNullWhen(true, nameof(Value))]` and
  `[MemberNotNullWhen(false, nameof(Error))]`. Do not weaken these guards or annotations.
  `default(Result<T>)` is documented-invalid; the void `Result` is the value-less success type.
- Both `Result<T>` and void `Result` carry a `SuccessKind` field. ALL `To*Result()` extension methods,
  including `ToOkResult` and the typed `ToAcceptedResult`, on both the Minimal API and controller surfaces,
  validate that the `SuccessKind` matches the intended HTTP status and throw `InvalidOperationException`
  on mismatch. Do not remove these guards.
- `ProblemActionResult` (controllers) and `EfCoreConcurrencyExceptionHandler` set the transport
  `Response.StatusCode` from the mapped error before calling the ProblemDetails service; the service
  serialises the body but never copies `pd.Status` onto the response. Keep the explicit assignment.
- `ProblemDetailsBuilder` sets the RFC 9110 `type` URI explicitly. Keep this in sync with `HttpErrorCatalog.TypeUri()`.
- `ConfigureResultCrafterProblemDetails` normalizes framework-default ProblemDetails (binding failures, and bare
  status codes via `UseStatusCodePages`) to the house style via `HttpErrorCatalog.TryGetErrorType`, only when the
  title is still the framework default. This keeps Development (handler) and Production (status-code path) responses
  identical. Never overwrite a title already set by a Result or the application.
- `x-rc` and `x-rc-error-id` are internal pipeline markers: the marker extensions are stripped before the
  response body is written. The `ProblemDetailsKeys` string constants stay public (documented for middleware
  branching use), but never let the marker extensions leak into a written response.
- `GetInstance` returns `PathBase + Path` only (relative URI). Never add host resolution.
- EF Core handler must be inserted before the generic 500 handler in the DI list.
- `BadHttpRequestExceptionHandler` (`ResultCrafter.AspNetCore`) is registered by `AddResultCrafter()` before the
  generic 500 handler and maps a 4xx `BadHttpRequestException` to its carried `StatusCode` (dev/prod parity for
  Minimal API binding failures). Keep it before the catch-all; non-4xx fall through to the 500 handler.
- `ResultCrafter.AspNetCore.FluentValidation` is the ASP.NET-Core-coupled bridge (`WithValidation<T>()` endpoint
  filter). `ValidationFilter<T>` aligns `errors` keys to the app's JSON `PropertyNamingPolicy` per path segment;
  it must not mutate `ValidatorOptions.Global`. It passes through only when the endpoint has no non-null `TRequest`
  argument (a null-bound `TRequest` is indistinguishable from an absent argument and also passes through);
  if a non-null `TRequest` is present but no `IValidator<TRequest>` is registered it throws (fail-fast). Keep
  `ResultCrafter.FluentValidation` ASP.NET-Core-free.
- `TreatWarningsAsErrors` is `false` in `Directory.Build.props`; nullable warnings are escalated to errors via
  `<WarningsAsErrors>$(WarningsAsErrors);Nullable</WarningsAsErrors>`. The PR CI workflow builds with
  `-warnaserror`, so any warning fails the PR gate even though local builds only warn. No third-party analyzers
  are referenced. All CS1591 (missing XML doc) on public members in `src/` projects should still be fixed before
  merging.
- Test and demo projects set `<IsPackable>false</IsPackable>` and
  `<GenerateDocumentationFile>false</GenerateDocumentationFile>`.

## Adding a new ErrorType

1. Add the enum value in `ErrorType.cs` (with an explicit int value).
2. Add a factory method on `Error`.
3. Add `Status`, `Title`, `DefaultDetail`, and `TypeUri` cases in `HttpErrorCatalog` (and a `TryGetErrorType`
   reverse-map case if a bare response with that status should be normalized to the house style).
4. Add a `ProducesXxx` attribute in `Mvc/ControllersAttributes.cs`.
5. Add a `ProducesXxx` builder extension in `RouteHandlerBuilderExtensions.cs`.
6. Add tests for the new type in `HttpErrorCatalogTests`, `ProblemDetailsBuilderTests`, and
   `ControllerResultAttributesTests`.

## Conventions

- No git commit or push without explicit instruction.
- `MediatR` and `FluentValidation` PropertyName normalisation uses `string.IsNullOrWhiteSpace` consistently.
- Demo project (`tests/ResultCrafter.Demo/`) uses `ConcurrentDictionary` and `Interlocked.Increment` for
  thread safety since `ItemService` is registered as Singleton.

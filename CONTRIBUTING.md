# Contributing

Issues and pull requests are welcome. Please open an issue before starting significant work so we can discuss the
approach.

If ResultCrafter has helped you, a GitHub star goes a long way.

## Build and test

```bash
dotnet build
dotnet test
bash tests/ResultCrafter.AotSmoke/run-aot-smoke.sh   # NativeAOT publish and self-test (heavy)
```

ResultCrafter ships with a comprehensive test suite covering the core primitives, the ASP.NET Core pipeline
integration, the controller extensions, and the FluentValidation bridge. The test project is structured into focused
directories (`Core`, `AspNetCore`, `AspNetCore.EfCore`, `AspNetCore.FluentValidation`, `FluentValidation`) and runs
every test on both .NET 10 and .NET 11. A separate NativeAOT smoke app (`tests/ResultCrafter.AotSmoke`) is published
and run on both runtimes in CI.

If you are contributing, the expectation is that new behavior ships with new tests.

## Roadmap

ResultCrafter has no fixed release schedule. Changes happen when they make the library better, not on a calendar.
If there is something you want next, open an issue. Community feedback is what drives prioritization.

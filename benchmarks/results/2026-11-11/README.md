# Run of 2026-11-11

Raw BenchmarkDotNet output, renamed: `ErrorPathBenchmarks.md` and `RequestBenchmarks.md` (GitHub markdown report),
`ErrorPathBenchmarks.json` and `RequestBenchmarks.json` (brief JSON: statistics, no per-iteration samples).

## Machine and runtime

| Item          | Value                                                                                         |
|---------------|-----------------------------------------------------------------------------------------------|
| Time          | 2026-11-11, 00:34 to 00:48 (UTC+4)                                                            |
| Runtime       | .NET 11.0.0 GA, X64 (`RuntimeInformation.FrameworkDescription`: `.NET 11.0.0`)                |
| SDK           | 11.0.100 (`global.json`); runtimes installed: 10.0.14 and 11.0.0                              |
| Tool          | BenchmarkDotNet 0.16.0-preview.2 (0.15.8, the latest stable, cannot run on .NET 11)           |
| CPU           | AMD Ryzen AI 9 HX PRO 370, 12 cores / 24 threads: 4 Zen 5 and 8 Zen 5c cores                  |
| Memory, OS    | 56 GB, Windows 11 Pro 10.0.26300                                                              |
| Power         | on AC; Windows power plan Balanced, power mode overlay `ded574b5` (Best performance), unchanged |
| Pinning       | `--affinity 255`: benchmark processes on logical CPUs 0 to 7, the Zen 5 cores                 |
| Load          | idle laptop, 2 to 5% total CPU before the start (Windows Search, Defender)                    |
| Code measured | `release/3.0` at `e8d791d` plus the uncommitted 3.0 change set (ResultCrafter 3.0.0)          |
| Competitors   | Ardalis.Result 10.1.0, ErrorOr 2.1.1, FluentResults 4.0.0                                     |

Sizes printed by the program (`Unsafe.SizeOf`): `Result` 24 B, `Result<string>` 32 B, the union `OrderLookup` 8 B.

## Why pinned

The CPU mixes fast and slow cores. The same single-thread loop pinned to each logical CPU took 3.9 ms on CPUs 0 to 7
and 6.4 ms on CPUs 8 to 23. BenchmarkDotNet starts one process per row, and Windows puts each process on either kind.
An unpinned run of the same code a few minutes earlier gave bimodal rows and rows up to 1.8 times slower depending on
the core a process got, so it was discarded. Allocations do not depend on the core and matched in both runs.

## Commands

```bash
dotnet run -c Release --project benchmarks/ResultCrafter.Benchmarks -- --filter "*ErrorPathBenchmarks*" --affinity 255
dotnet run -c Release --project benchmarks/ResultCrafter.Benchmarks -- --filter "*RequestBenchmarks*" --affinity 255
```

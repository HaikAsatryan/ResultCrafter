```

BenchmarkDotNet v0.16.0-preview.2, Windows 11 (10.0.26300.9711)
AMD Ryzen AI 9 HX PRO 370 w/ Radeon 890M 2.00GHz, 1 CPU, 24 logical and 12 physical cores
Memory: 55.65 GB Total, 38.54 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4
  Job-WGXIRD : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

Affinity=000000000000000011111111  PowerPlanMode=67b4a053-3646-4532-affd-0535c9ea82a7  

```
| Method        | Depth | Found | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------- |------ |------ |-------------:|-----------:|-----------:|------:|--------:|-------:|----------:|------------:|
| **ThrowCatch**    | **1**     | **False** | **2,207.883 ns** | **12.2537 ns** | **10.2324 ns** | **1.000** |    **0.00** | **0.0992** |     **856 B** |        **1.00** |
| ResultCrafter | 1     | False |    20.703 ns |  0.0711 ns |  0.0594 ns | 0.009 |    0.00 | 0.0076 |      64 B |        0.07 |
| Union         | 1     | False |    10.813 ns |  0.1769 ns |  0.1655 ns | 0.005 |    0.00 | 0.0029 |      24 B |        0.03 |
| ArdalisResult | 1     | False |    14.252 ns |  0.0875 ns |  0.0819 ns | 0.006 |    0.00 | 0.0124 |     104 B |        0.12 |
| ErrorOr       | 1     | False |    21.277 ns |  0.1991 ns |  0.1863 ns | 0.010 |    0.00 | 0.0105 |      88 B |        0.10 |
| FluentResults | 1     | False |    36.327 ns |  0.5156 ns |  0.4306 ns | 0.016 |    0.00 | 0.0430 |     360 B |        0.42 |
|               |       |       |              |            |            |       |         |        |           |             |
| **ThrowCatch**    | **1**     | **True**  |     **9.505 ns** |  **0.1777 ns** |  **0.1575 ns** |  **1.00** |    **0.00** |      **-** |         **-** |          **NA** |
| ResultCrafter | 1     | True  |    20.771 ns |  0.0922 ns |  0.0817 ns |  2.19 |    0.04 |      - |         - |          NA |
| Union         | 1     | True  |     9.467 ns |  0.0870 ns |  0.0771 ns |  1.00 |    0.02 |      - |         - |          NA |
| ArdalisResult | 1     | True  |    14.161 ns |  0.1946 ns |  0.1725 ns |  1.49 |    0.03 | 0.0086 |      72 B |          NA |
| ErrorOr       | 1     | True  |    18.201 ns |  0.0675 ns |  0.0599 ns |  1.92 |    0.03 |      - |         - |          NA |
| FluentResults | 1     | True  |    26.740 ns |  0.3672 ns |  0.3435 ns |  2.81 |    0.06 | 0.0249 |     208 B |          NA |
|               |       |       |              |            |            |       |         |        |           |             |
| **ThrowCatch**    | **3**     | **False** | **4,834.249 ns** | **17.5163 ns** | **15.5277 ns** | **1.000** |    **0.00** | **0.3357** |    **2808 B** |       **1.000** |
| ResultCrafter | 3     | False |    67.577 ns |  0.1847 ns |  0.1728 ns | 0.014 |    0.00 | 0.0076 |      64 B |       0.023 |
| Union         | 3     | False |    18.705 ns |  0.1463 ns |  0.1221 ns | 0.004 |    0.00 | 0.0029 |      24 B |       0.009 |
| ArdalisResult | 3     | False |    22.189 ns |  0.2141 ns |  0.2003 ns | 0.005 |    0.00 | 0.0124 |     104 B |       0.037 |
| ErrorOr       | 3     | False |    44.504 ns |  0.3767 ns |  0.3524 ns | 0.009 |    0.00 | 0.0105 |      88 B |       0.031 |
| FluentResults | 3     | False |    43.789 ns |  0.4969 ns |  0.4648 ns | 0.009 |    0.00 | 0.0430 |     360 B |       0.128 |
|               |       |       |              |            |            |       |         |        |           |             |
| **ThrowCatch**    | **3**     | **True**  |    **17.162 ns** |  **0.1274 ns** |  **0.1129 ns** |  **1.00** |    **0.00** |      **-** |         **-** |          **NA** |
| ResultCrafter | 3     | True  |    65.802 ns |  0.2416 ns |  0.2142 ns |  3.83 |    0.03 |      - |         - |          NA |
| Union         | 3     | True  |    17.958 ns |  0.1616 ns |  0.1512 ns |  1.05 |    0.01 |      - |         - |          NA |
| ArdalisResult | 3     | True  |    20.953 ns |  0.2489 ns |  0.2328 ns |  1.22 |    0.02 | 0.0086 |      72 B |          NA |
| ErrorOr       | 3     | True  |    44.354 ns |  0.1958 ns |  0.1831 ns |  2.58 |    0.02 |      - |         - |          NA |
| FluentResults | 3     | True  |    35.788 ns |  0.2439 ns |  0.2162 ns |  2.09 |    0.02 | 0.0249 |     208 B |          NA |

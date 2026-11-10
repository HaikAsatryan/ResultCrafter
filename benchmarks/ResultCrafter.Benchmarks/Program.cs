using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using ResultCrafter;
using ResultCrafter.Benchmarks;

Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}, {RuntimeInformation.ProcessArchitecture}");
Console.WriteLine($"Size: Result {Unsafe.SizeOf<Result>()} B, Result<string> {Unsafe.SizeOf<Result<string>>()} B, " +
                  $"union OrderLookup {Unsafe.SizeOf<OrderLookup>()} B");

var config = DefaultConfig.Instance
    .AddExporter(JsonExporter.Brief)
    // BenchmarkDotNet switches Windows to the High performance power plan by default. Keep the plan the user has.
    .AddJob(Job.Default.WithPowerPlan(PowerPlan.UserPowerPlan).AsMutator());

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);

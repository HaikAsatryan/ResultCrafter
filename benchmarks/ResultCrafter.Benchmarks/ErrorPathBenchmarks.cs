using BenchmarkDotNet.Attributes;

namespace ResultCrafter.Benchmarks;

/// <summary>
///     Suite A, the error alone: an order lookup <see cref="Depth" /> async calls deep that finds the order or reports
///     "not found", with no HTTP around it.
/// </summary>
[MemoryDiagnoser]
public class ErrorPathBenchmarks
{
    private int _id;

    [Params(1, 3)]
    public int Depth { get; set; }

    [Params(true, false)]
    public bool Found { get; set; }

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _id = Found ? Orders.KnownId : Orders.MissingId;

        // Every variant must give the same answer, so nothing broken gets measured.
        int[] answers =
        [
            await ThrowCatch(), await ResultCrafter(), await Union(), await ArdalisResult(), await ErrorOr(),
            await FluentResults()
        ];
        var expected = Found ? Orders.KnownId : -1;
        if (answers.Any(answer => answer != expected))
            throw new InvalidOperationException($"Expected {expected} from every variant, got [{string.Join(", ", answers)}].");
    }

    [Benchmark(Baseline = true)]
    public async Task<int> ThrowCatch()
    {
        try
        {
            return (await Orders.GetOrThrowAsync(_id, Depth)).Id;
        }
        catch (OrderNotFoundException)
        {
            return -1;
        }
    }

    [Benchmark]
    public async Task<int> ResultCrafter()
    {
        var result = await Orders.GetResultAsync(_id, Depth);
        return result.IsSuccess ? result.Value.Id : -1;
    }

    [Benchmark]
    public async Task<int> Union()
    {
        return await Orders.GetUnionAsync(_id, Depth) switch
        {
            Order order => order.Id,
            OrderNotFound => -1
        };
    }

    [Benchmark]
    public async Task<int> ArdalisResult()
    {
        var result = await Competitors.GetArdalisAsync(_id, Depth);
        return result.IsSuccess ? result.Value.Id : -1;
    }

    [Benchmark]
    public async Task<int> ErrorOr()
    {
        var result = await Competitors.GetErrorOrAsync(_id, Depth);
        return result.IsError ? -1 : result.Value.Id;
    }

    [Benchmark]
    public async Task<int> FluentResults()
    {
        var result = await Competitors.GetFluentResultAsync(_id, Depth);
        return result.IsFailed ? -1 : result.Value.Id;
    }
}

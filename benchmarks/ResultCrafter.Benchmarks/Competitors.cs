using ErrorOr;

namespace ResultCrafter.Benchmarks;

/// <summary>The same lookup as <see cref="Orders" />, returned through Ardalis.Result, ErrorOr and FluentResults.</summary>
public static class Competitors
{
    public static async ValueTask<Ardalis.Result.Result<Order>> GetArdalisAsync(int id, int depth)
    {
        if (depth > 1) return await GetArdalisAsync(id, depth - 1);
        return Orders.Find(id) is { } order
            ? Ardalis.Result.Result<Order>.Success(order)
            : Ardalis.Result.Result<Order>.NotFound(Orders.Detail);
    }

    public static async ValueTask<ErrorOr<Order>> GetErrorOrAsync(int id, int depth)
    {
        if (depth > 1) return await GetErrorOrAsync(id, depth - 1);
        return Orders.Find(id) is { } order ? order : global::ErrorOr.Error.NotFound(description: Orders.Detail);
    }

    public static async ValueTask<FluentResults.Result<Order>> GetFluentResultAsync(int id, int depth)
    {
        if (depth > 1) return await GetFluentResultAsync(id, depth - 1);
        return Orders.Find(id) is { } order
            ? FluentResults.Result.Ok(order)
            : FluentResults.Result.Fail<Order>(Orders.Detail);
    }
}

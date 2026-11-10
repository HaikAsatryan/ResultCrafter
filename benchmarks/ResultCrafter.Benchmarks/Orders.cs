namespace ResultCrafter.Benchmarks;

public sealed record Order(int Id, string Item, decimal Total);

public sealed class OrderNotFoundException() : Exception(Orders.Detail);

public sealed record OrderNotFound(string Detail);

public readonly union OrderLookup(Order, OrderNotFound);

/// <summary>
///     One order lookup written four ways. Each call goes <c>depth</c> async methods deep, like endpoint, service and
///     repository. The lookup itself completes synchronously (a cache hit), so the numbers show the cost of the error
///     mechanism, not of thread-pool hops.
/// </summary>
public static class Orders
{
    public const int KnownId = 1;
    public const int MissingId = 2;
    public const string Detail = "order_not_found";

    private static readonly Order Known = new(KnownId, "book", 12.50m);

    internal static Order? Find(int id)
    {
        return id == KnownId ? Known : null;
    }

    public static async ValueTask<Order> GetOrThrowAsync(int id, int depth)
    {
        if (depth > 1) return await GetOrThrowAsync(id, depth - 1);
        return Find(id) ?? throw new OrderNotFoundException();
    }

    public static async ValueTask<Result<Order>> GetResultAsync(int id, int depth)
    {
        if (depth > 1) return await GetResultAsync(id, depth - 1);
        return Find(id) is { } order ? order : Error.NotFound(Detail);
    }

    public static async ValueTask<OrderLookup> GetUnionAsync(int id, int depth)
    {
        if (depth > 1) return await GetUnionAsync(id, depth - 1);
        return Find(id) is { } order ? order : new OrderNotFound(Detail);
    }

    public static async ValueTask<Order?> GetOrNullAsync(int id, int depth)
    {
        if (depth > 1) return await GetOrNullAsync(id, depth - 1);
        return Find(id);
    }
}

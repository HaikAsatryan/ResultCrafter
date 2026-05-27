namespace ResultCrafter.Demo;

// ── Requests ──────────────────────────────────────────────────────────────────

public sealed record CreateItemRequest(string? Name, decimal Price, int Stock);

/// <param name="Version">Optimistic concurrency token — must match the stored version.</param>
public sealed record UpdateItemRequest(string? Name, decimal Price, int Stock, int Version);

public sealed record BulkDeleteRequest(IReadOnlyList<int>? Ids);

// ── Responses ─────────────────────────────────────────────────────────────────

public sealed record ItemDto(int Id, string Name, decimal Price, int Stock, int Version, bool IsReserved);

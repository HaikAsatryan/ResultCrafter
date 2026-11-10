using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResultCrafter.AspNetCore.Mvc;

namespace ResultCrafter.Demo;

/// <summary>
///     MVC controller mirroring the CRUD, auth and crash scenarios of <see cref="DemoEndpoints" />, with the same
///     RFC 9457 ProblemDetails responses, structured logging and OpenAPI documentation as the Minimal API path.
///     One difference: create errors come from <c>ValidateToResultAsync</c> in the service, so their keys keep
///     FluentValidation's names (<c>Name</c>), while the Minimal API filter converts them (<c>name</c>).
/// </summary>
[ApiController]
[Route("api/ctrl/items")]
[Tags("Items (Controller)")]
public sealed class ItemsController(ItemService svc) : ControllerBase
{
    // ── GET /api/ctrl/items/{id} ──────────────────────────────────────────────

    /// <summary>Get item by ID: 200 Ok or 404 NotFound.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<ItemDto>(StatusCodes.Status200OK)]
    [ProducesNotFound]
    public async Task<ActionResult<ItemDto>> Get(int id, CancellationToken ct)
    {
        return (await svc.GetAsync(id, ct)).ToOkResult();
    }

    // ── POST /api/ctrl/items ──────────────────────────────────────────────────

    /// <summary>Create item: 201 Created or 400 BadRequest (field errors).</summary>
    [HttpPost]
    [ProducesResponseType<ItemDto>(StatusCodes.Status201Created)]
    [ProducesBadRequest]
    public async Task<ActionResult<ItemDto>> Create([FromBody] CreateItemRequest req, CancellationToken ct)
    {
        return (await svc.CreateAsync(req, ct)).ToCreatedResult();
    }

    // ── PUT /api/ctrl/items/{id} ──────────────────────────────────────────────

    /// <summary>Update item: 200 Ok, 404 NotFound, 400 BadRequest, or 409 Conflict / ConcurrencyConflict.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<ItemDto>(StatusCodes.Status200OK)]
    [ProducesNotFound]
    [ProducesBadRequest]
    [ProducesConflict]
    public async Task<ActionResult<ItemDto>> Update(int id, [FromBody] UpdateItemRequest req, CancellationToken ct)
    {
        return (await svc.UpdateAsync(id, req, ct)).ToOkResult();
    }

    // ── DELETE /api/ctrl/items/{id} ───────────────────────────────────────────

    /// <summary>Delete item: 204 NoContent or 404 NotFound.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesNotFound]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        return (await svc.DeleteAsync(id, ct)).ToNoContentResult();
    }

    // ── POST /api/ctrl/items/{id}/reserve ─────────────────────────────────────

    /// <summary>Reserve item: 202 Accepted&lt;ItemDto&gt;, 404 NotFound, or 403 Forbidden.</summary>
    [HttpPost("{id:int}/reserve")]
    [ProducesResponseType<ItemDto>(StatusCodes.Status202Accepted)]
    [ProducesNotFound]
    [ProducesForbidden]
    public async Task<ActionResult<ItemDto>> Reserve(int id, CancellationToken ct)
    {
        return (await svc.ReserveAsync(id, ct)).ToAcceptedResult();
    }

    // ── POST /api/ctrl/items/bulk-delete ──────────────────────────────────────

    /// <summary>Queue bulk deletion: 202 Accepted (no body) or 400 BadRequest.</summary>
    [HttpPost("bulk-delete")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesBadRequest]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequest req, CancellationToken ct)
    {
        return (await svc.BulkDeleteAsync(req, ct)).ToAcceptedResult();
    }

    // ── GET /api/ctrl/items/admin ─────────────────────────────────────────────

    /// <summary>
    ///     Admin list: 200 Ok, 401 Unauthorized (missing key), or 403 Forbidden (non-admin role).
    ///     Pass headers:  X-Api-Key: any-value   X-Role: admin
    /// </summary>
    [HttpGet("admin")]
    [ProducesResponseType<List<ItemDto>>(StatusCodes.Status200OK)]
    [ProducesUnauthorized]
    [ProducesForbidden]
    public async Task<ActionResult<List<ItemDto>>> AdminList([FromHeader(Name = "X-Api-Key")] string? apiKey,
        [FromHeader(Name = "X-Role")] string? role,
        CancellationToken ct)
    {
        return (await svc.GetAllAdminAsync(apiKey, role, ct)).ToOkResult();
    }

    // ── Crash demos ───────────────────────────────────────────────────────────

    /// <summary>
    ///     Throws an unhandled <see cref="InvalidOperationException" /> to demonstrate the
    ///     500 <c>IExceptionHandler</c> path: same handler as Minimal API.
    /// </summary>
    [HttpGet("crash")]
    public IActionResult Crash()
    {
        throw new InvalidOperationException(
            "Simulated unhandled exception from a controller: watch the logs.");
    }

    /// <summary>
    ///     Throws <see cref="DbUpdateConcurrencyException" /> to demonstrate the EF Core
    ///     integration intercepting it as a 409 ConcurrencyConflict ProblemDetails response
    ///     before it reaches the generic 500 handler.
    /// </summary>
    [HttpGet("db-crash")]
    public IActionResult DbCrash()
    {
        throw new DbUpdateConcurrencyException(
            "Simulated EF Core concurrency conflict: intercepted as 409 by ResultCrafterEfCore.",
            []);
    }
}

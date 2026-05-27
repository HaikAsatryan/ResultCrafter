using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResultCrafter.AspNetCore.Endpoints;
using ResultCrafter.AspNetCore.FluentValidation;

namespace ResultCrafter.Demo;

public static class DemoEndpoints
{
    public static RouteGroupBuilder MapDemoEndpoints(this RouteGroupBuilder api)
    {
        // ── Items CRUD ────────────────────────────────────────────────────────

        // Success: 200 Ok<ItemDto>
        // Error:   404 NotFound
        api.MapGet("/items/{id:int}",
                async (int id, ItemService svc, CancellationToken ct) =>
                    (await svc.GetAsync(id, ct)).ToOkResult())
            .WithName("GetItem")
            .WithSummary("Get item by ID — Ok or NotFound")
            .ProducesNotFound();

        // Success: 201 Created<ItemDto>
        // Error:   400 BadRequest (field errors)
        // WithValidation<T>() runs the registered IValidator<CreateItemRequest> as an endpoint
        // filter, short-circuiting invalid input with a 400 ProblemDetails before the handler runs.
        // It also documents the 400 response in OpenAPI, so no separate .ProducesBadRequest() is needed.
        api.MapPost("/items",
                async (CreateItemRequest req, ItemService svc, CancellationToken ct) =>
                    (await svc.CreateAsync(req, ct)).ToCreatedResult())
            .WithValidation<CreateItemRequest>()
            .WithName("CreateItem")
            .WithSummary("Create item — Created or BadRequest");

        // Success: 200 Ok<ItemDto>
        // Error:   404 NotFound | 400 BadRequest (field errors) | 409 Conflict | 409 ConcurrencyConflict
        api.MapPut("/items/{id:int}",
                async (int id, UpdateItemRequest req, ItemService svc, CancellationToken ct) =>
                    (await svc.UpdateAsync(id, req, ct)).ToOkResult())
            .WithName("UpdateItem")
            .WithSummary("Update item — Ok, NotFound, BadRequest, Conflict, or ConcurrencyConflict")
            .ProducesNotFound()
            .ProducesBadRequest()
            .ProducesConflict();

        // Success: 204 NoContent
        // Error:   404 NotFound
        api.MapDelete("/items/{id:int}",
                async (int id, ItemService svc, CancellationToken ct) =>
                    (await svc.DeleteAsync(id, ct)).ToNoContentResult())
            .WithName("DeleteItem")
            .WithSummary("Delete item — NoContent or NotFound")
            .ProducesNotFound();

        // ── Async / queue operations ──────────────────────────────────────────

        // Success: 202 Accepted<ItemDto>  (background workflow started, poll GET to confirm)
        // Error:   404 NotFound | 403 Forbidden (already reserved)
        api.MapPost("/items/{id:int}/reserve",
                async (int id, ItemService svc, CancellationToken ct) =>
                    (await svc.ReserveAsync(id, ct)).ToAcceptedResult())
            .WithName("ReserveItem")
            .WithSummary("Reserve item — Accepted<ItemDto>, NotFound, or Forbidden")
            .ProducesNotFound()
            .ProducesForbidden();

        // Success: 202 Accepted  (no body — deletion is queued)
        // Error:   400 BadRequest with plain detail  (empty list)
        //          400 BadRequest with errors dict   (invalid IDs / too many)
        api.MapPost("/items/bulk-delete",
                async (BulkDeleteRequest req, ItemService svc, CancellationToken ct) =>
                    (await svc.BulkDeleteAsync(req, ct)).ToAcceptedResult())
            .WithName("BulkDeleteItems")
            .WithSummary("Queue bulk deletion — Accepted (void), or BadRequest (plain or with errors)")
            .ProducesBadRequest();

        // ── Auth-gated endpoint ───────────────────────────────────────────────

        // Success: 200 Ok<IReadOnlyList<ItemDto>>
        // Error:   401 Unauthorized (missing key) | 403 Forbidden (wrong role)
        //
        // Pass headers:  X-Api-Key: any-value   X-Role: admin
        api.MapGet("/items/admin",
                async ([FromHeader(Name = "X-Api-Key")] string? apiKey,
                        [FromHeader(Name = "X-Role")] string? role,
                        ItemService svc,
                        CancellationToken ct) =>
                    (await svc.GetAllAdminAsync(apiKey, role, ct)).ToOkResult())
            .WithName("AdminListItems")
            .WithSummary("Admin list — Ok, Unauthorized (missing key), or Forbidden (non-admin role)")
            .ProducesUnauthorized()
            .ProducesForbidden();

        // ── Exception / crash demos ───────────────────────────────────────────

        // Deliberately throws to demonstrate the 500 IExceptionHandler path.
        // ResultCrafterExceptionHandler catches it, logs it at Error, and returns a
        // clean RFC 9457 ProblemDetails 500 response.
        api.MapGet("/items/crash",
                (ItemService _) =>
                {
                    throw new InvalidOperationException(
                        "Simulated unhandled exception — watch the logs.");
                })
            .WithName("CrashEndpoint")
            .WithSummary("Throws an unhandled exception — demonstrates the 500 IExceptionHandler path");

        // Deliberately throws DbUpdateConcurrencyException to demonstrate the EF Core
        // integration intercepting it as a 409 before the generic 500 handler sees it.
        api.MapGet("/items/db-crash",
                (ItemService _) =>
                {
                    throw new DbUpdateConcurrencyException(
                        "Simulated EF Core concurrency conflict — intercepted as 409 by ResultCrafterEfCore.",
                        []);
                })
            .WithName("DbCrashEndpoint")
            .WithSummary("Throws DbUpdateConcurrencyException — demonstrates the 409 EF Core handler");

        return api;
    }
}

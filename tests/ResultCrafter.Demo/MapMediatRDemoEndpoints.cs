using MediatR;
using ResultCrafter.AspNetCore.Endpoints;

namespace ResultCrafter.Demo;

/// <summary>
/// Minimal API endpoints demonstrating ResultCrafter + MediatR + FluentValidation
/// working together end-to-end.
///
/// The pipeline for each request:
///   HTTP request
///     → MediatR.Send()
///     → ResultValidationBehavior / VoidResultValidationBehavior  (auto-validates)
///         → 400 ProblemDetails if invalid  (behavior short-circuits here)
///         → Handler.Handle()               (only reached when valid)
///             → Result success/failure
///     → .ToOkResult() / .ToNoContentResult()
///     → HTTP response
/// </summary>
public static class MediatRDemoEndpoints
{
   public static RouteGroupBuilder MapMediatRDemoEndpoints(this RouteGroupBuilder group)
   {
      // ── Query: Result<T> path ─────────────────────────────────────────────
      //
      // GET /mediatR/items/{id}
      //
      // Valid id   → GetItemQueryHandler runs → 200 Ok<ItemDto> or 404 NotFound
      // Invalid id → behavior short-circuits  → 400 BadRequest (field errors)
      group.MapGet("/items/{id:int}",
              async (int id, IMediator mediator, CancellationToken ct) =>
                 (await mediator.Send(new GetItemQuery(id), ct)).ToOkResult())
           .WithName("MediatR_GetItem")
           .WithSummary("MediatR: get item — 200, 400 (invalid id), or 404 (not found)")
           .ProducesNotFound()
           .ProducesBadRequest();

      // ── Command: void Result path ─────────────────────────────────────────
      //
      // DELETE /mediatR/items/{id}
      //
      // Valid id   → DeleteItemCommandHandler runs → 204 NoContent or 404 NotFound
      // Invalid id → behavior short-circuits       → 400 BadRequest (field errors)
      group.MapDelete("/items/{id:int}",
              async (int id, IMediator mediator, CancellationToken ct) =>
                 (await mediator.Send(new DeleteItemCommand(id), ct)).ToNoContentResult())
           .WithName("MediatR_DeleteItem")
           .WithSummary("MediatR: delete item — 204, 400 (invalid id), or 404 (not found)")
           .ProducesNotFound()
           .ProducesBadRequest();

      return group;
   }
}
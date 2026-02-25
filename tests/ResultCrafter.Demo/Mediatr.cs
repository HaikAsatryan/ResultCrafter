using FluentValidation;
using MediatR;
using ResultCrafter.Core.Primitives;

namespace ResultCrafter.Demo;

// ── Commands / Queries ────────────────────────────────────────────────────────

/// <summary>Query: fetch an item by ID. Returns Result&lt;ItemDto&gt;.</summary>
public sealed record GetItemQuery(int Id) : IRequest<Result<ItemDto>>;

/// <summary>Command: delete an item. Returns void Result (204 or error).</summary>
public sealed record DeleteItemCommand(int Id) : IRequest<Result>;

// ── Validators ────────────────────────────────────────────────────────────────

public sealed class GetItemQueryValidator : AbstractValidator<GetItemQuery>
{
   public GetItemQueryValidator()
   {
      RuleFor(x => x.Id)
         .GreaterThan(0)
         .WithMessage("Id must be greater than 0.")
         .LessThan(10_000)
         .WithMessage("Id must be less than 10 000.");
   }
}

public sealed class DeleteItemCommandValidator : AbstractValidator<DeleteItemCommand>
{
   public DeleteItemCommandValidator()
   {
      RuleFor(x => x.Id)
         .GreaterThan(0)
         .WithMessage("Id must be greater than 0.")
         .LessThan(10_000)
         .WithMessage("Id must be less than 10 000.");
   }
}

// ── Handlers ──────────────────────────────────────────────────────────────────

/// <summary>
/// Handles <see cref="GetItemQuery"/>.
/// Validation is automatic via the pipeline behavior — this handler only runs
/// when the query is valid.
/// </summary>
public sealed class GetItemQueryHandler(ItemService svc)
   : IRequestHandler<GetItemQuery, Result<ItemDto>>
{
   public Task<Result<ItemDto>> Handle(GetItemQuery query, CancellationToken ct) => svc.GetAsync(query.Id, ct);
}

/// <summary>
/// Handles <see cref="DeleteItemCommand"/>.
/// Validation is automatic via the pipeline behavior — this handler only runs
/// when the command is valid.
/// </summary>
public sealed class DeleteItemCommandHandler(ItemService svc)
   : IRequestHandler<DeleteItemCommand, Result>
{
   public Task<Result> Handle(DeleteItemCommand cmd, CancellationToken ct) => svc.DeleteAsync(cmd.Id, ct);
}
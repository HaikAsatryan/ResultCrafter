using FluentValidation;
using FluentValidation.Results;
using MediatR;
using ResultCrafter.Core.Primitives;
using ResultCrafter.MediatR;

namespace ResultCrafter.Tests.MediatR;

public sealed class VoidResultValidationBehaviorTests
{
   // ════════════════════════════════════════════════════════
   // Pass-through
   // ════════════════════════════════════════════════════════

   [Fact]
   public async Task Handle_NoValidators_CallsNext()
   {
      var behavior = new VoidResultValidationBehavior<DeleteCommand>([]);
      var called   = false;

      await behavior.Handle(
         new DeleteCommand(1),
         _ => { called = true; return Task.FromResult(Result.NoContent()); },
         CancellationToken.None);

      Assert.True(called);
   }

   [Fact]
   public async Task Handle_NoValidators_ReturnsHandlerValue()
   {
      var behavior = new VoidResultValidationBehavior<DeleteCommand>([]);

      var result = await behavior.Handle(
         new DeleteCommand(1),
         _ => Task.FromResult(Result.NoContent()),
         CancellationToken.None);

      Assert.True(result.IsSuccess);
   }

   // ════════════════════════════════════════════════════════
   // Valid request
   // ════════════════════════════════════════════════════════

   [Fact]
   public async Task Handle_ValidRequest_CallsNext()
   {
      var behavior = new VoidResultValidationBehavior<DeleteCommand>([new DeleteCommandValidator()]);
      var called   = false;

      await behavior.Handle(
         new DeleteCommand(1),
         _ => { called = true; return Task.FromResult(Result.NoContent()); },
         CancellationToken.None);

      Assert.True(called);
   }

   [Fact]
   public async Task Handle_ValidRequest_CanReturnAccepted()
   {
      var behavior = new VoidResultValidationBehavior<DeleteCommand>([new DeleteCommandValidator()]);

      var result = await behavior.Handle(
         new DeleteCommand(1),
         _ => Task.FromResult(Result.Accepted("/status/1")),
         CancellationToken.None);

      Assert.True(result.IsSuccess);
      Assert.Equal("/status/1", result.AcceptedLocation);
   }

   // ════════════════════════════════════════════════════════
   // Invalid request
   // ════════════════════════════════════════════════════════

   [Fact]
   public async Task Handle_InvalidRequest_DoesNotCallNext()
   {
      var behavior = new VoidResultValidationBehavior<DeleteCommand>([new DeleteCommandValidator()]);
      var called   = false;

      await behavior.Handle(
         new DeleteCommand(0),
         _ => { called = true; return Task.FromResult(Result.NoContent()); },
         CancellationToken.None);

      Assert.False(called);
   }

   [Fact]
   public async Task Handle_InvalidRequest_ReturnsBadRequestError()
   {
      var behavior = new VoidResultValidationBehavior<DeleteCommand>([new DeleteCommandValidator()]);

      var result = await behavior.Handle(
         new DeleteCommand(0),
         _ => Task.FromResult(Result.NoContent()),
         CancellationToken.None);

      Assert.False(result.IsSuccess);
      Assert.Equal(ErrorType.BadRequest, result.Error!.Value.Type);
   }

   [Fact]
   public async Task Handle_InvalidRequest_IsValidationError()
   {
      var behavior = new VoidResultValidationBehavior<DeleteCommand>([new DeleteCommandValidator()]);

      var result = await behavior.Handle(
         new DeleteCommand(0),
         _ => Task.FromResult(Result.NoContent()),
         CancellationToken.None);

      Assert.True(result.Error!.Value.IsValidation);
   }

   [Fact]
   public async Task Handle_InvalidRequest_ErrorsContainFieldKey()
   {
      var behavior = new VoidResultValidationBehavior<DeleteCommand>([new DeleteCommandValidator()]);

      var result = await behavior.Handle(
         new DeleteCommand(0),
         _ => Task.FromResult(Result.NoContent()),
         CancellationToken.None);

      Assert.True(result.Error!.Value.Errors!.ContainsKey("Id"));
   }

   // ════════════════════════════════════════════════════════
   // Global failures → "_" key
   // ════════════════════════════════════════════════════════

   [Fact]
   public async Task Handle_GlobalFailure_MappedToUnderscoreKey()
   {
      var behavior = new VoidResultValidationBehavior<DeleteCommand>([new GlobalDeleteValidator()]);

      var result = await behavior.Handle(
         new DeleteCommand(-99),
         _ => Task.FromResult(Result.NoContent()),
         CancellationToken.None);

      Assert.True(result.Error!.Value.Errors!.ContainsKey("_"));
   }

   // ════════════════════════════════════════════════════════
   // Multiple validators
   // ════════════════════════════════════════════════════════

   [Fact]
   public async Task Handle_MultipleValidators_AllErrorsCollected()
   {
      var behavior = new VoidResultValidationBehavior<DeleteCommand>([
         new DeleteCommandValidator(),
         new DeleteCommandSecondaryValidator()
      ]);

      var result = await behavior.Handle(
         new DeleteCommand(0),
         _ => Task.FromResult(Result.NoContent()),
         CancellationToken.None);

      var messages = result.Error!.Value.Errors!["Id"];
      Assert.Equal(2, messages.Length);
   }

   [Fact]
   public async Task Handle_MultipleValidators_WhenAllPass_CallsNext()
   {
      var behavior = new VoidResultValidationBehavior<DeleteCommand>([
         new DeleteCommandValidator(),
         new DeleteCommandSecondaryValidator()
      ]);

      var called = false;

      await behavior.Handle(
         new DeleteCommand(10),
         _ => { called = true; return Task.FromResult(Result.NoContent()); },
         CancellationToken.None);

      Assert.True(called);
   }

   // ════════════════════════════════════════════════════════
   // Test types
   // ════════════════════════════════════════════════════════

   private sealed record DeleteCommand(int Id) : IRequest<Result>;

   private sealed class DeleteCommandValidator : AbstractValidator<DeleteCommand>
   {
      public DeleteCommandValidator() =>
         RuleFor(x => x.Id).GreaterThan(0).WithMessage("Id must be greater than 0.");
   }

   private sealed class DeleteCommandSecondaryValidator : AbstractValidator<DeleteCommand>
   {
      public DeleteCommandSecondaryValidator() =>
         RuleFor(x => x.Id).LessThan(1000).WithMessage("Id must be less than 1000.");
   }

   private sealed class GlobalDeleteValidator : AbstractValidator<DeleteCommand>
   {
      public GlobalDeleteValidator()
      {
         RuleFor(x => x).Custom((x, ctx) =>
         {
            if (x.Id <= 0)
            {
               ctx.AddFailure(new ValidationFailure("", "Command is globally invalid."));
            }
         });
      }
   }
}
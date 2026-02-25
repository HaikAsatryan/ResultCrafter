using FluentValidation;
using FluentValidation.Results;
using MediatR;
using ResultCrafter.Core.Primitives;
using ResultCrafter.MediatR;


namespace ResultCrafter.Tests.MediatR;

public sealed class ResultValidationBehaviorTests
{
   // ════════════════════════════════════════════════════════
   // Pass-through — no validators registered
   // ════════════════════════════════════════════════════════

   [Fact]
   public async Task Handle_NoValidators_CallsNext()
   {
      var behavior = new ResultValidationBehavior<TestQuery, string>([]);
      var called = false;

      await behavior.Handle(
         new TestQuery("valid", null),
         _ =>
         {
            called = true;
            return Task.FromResult(Result<string>.Ok("value"));
         },
         CancellationToken.None);

      Assert.True(called);
   }

   [Fact]
   public async Task Handle_NoValidators_ReturnsHandlerValue()
   {
      var behavior = new ResultValidationBehavior<TestQuery, string>([]);

      var result = await behavior.Handle(
         new TestQuery("valid", null),
         _ => Task.FromResult(Result<string>.Ok("hello")),
         CancellationToken.None);

      Assert.True(result.IsSuccess);
      Assert.Equal("hello", result.Value);
   }

   // ════════════════════════════════════════════════════════
   // Valid request
   // ════════════════════════════════════════════════════════

   [Fact]
   public async Task Handle_ValidRequest_CallsNext()
   {
      var behavior = new ResultValidationBehavior<TestQuery, string>([new TestQueryValidator()]);
      var called = false;

      await behavior.Handle(
         new TestQuery("valid", null),
         _ =>
         {
            called = true;
            return Task.FromResult(Result<string>.Ok("ok"));
         },
         CancellationToken.None);

      Assert.True(called);
   }

   [Fact]
   public async Task Handle_ValidRequest_ReturnsHandlerValue()
   {
      var behavior = new ResultValidationBehavior<TestQuery, string>([new TestQueryValidator()]);

      var result = await behavior.Handle(
         new TestQuery("valid", null),
         _ => Task.FromResult(Result<string>.Ok("payload")),
         CancellationToken.None);

      Assert.True(result.IsSuccess);
      Assert.Equal("payload", result.Value);
   }

   // ════════════════════════════════════════════════════════
   // Invalid request — field error
   // ════════════════════════════════════════════════════════

   [Fact]
   public async Task Handle_InvalidRequest_DoesNotCallNext()
   {
      var behavior = new ResultValidationBehavior<TestQuery, string>([new TestQueryValidator()]);
      var called = false;

      await behavior.Handle(
         new TestQuery("", null),
         _ =>
         {
            called = true;
            return Task.FromResult(Result<string>.Ok("nope"));
         },
         CancellationToken.None);

      Assert.False(called);
   }

   [Fact]
   public async Task Handle_InvalidRequest_ReturnsBadRequestError()
   {
      var behavior = new ResultValidationBehavior<TestQuery, string>([new TestQueryValidator()]);

      var result = await behavior.Handle(
         new TestQuery("", null),
         _ => Task.FromResult(Result<string>.Ok("x")),
         CancellationToken.None);

      Assert.False(result.IsSuccess);
      Assert.Equal(ErrorType.BadRequest, result.Error!.Value.Type);
   }

   [Fact]
   public async Task Handle_InvalidRequest_IsValidationError()
   {
      var behavior = new ResultValidationBehavior<TestQuery, string>([new TestQueryValidator()]);

      var result = await behavior.Handle(
         new TestQuery("", null),
         _ => Task.FromResult(Result<string>.Ok("x")),
         CancellationToken.None);

      Assert.True(result.Error!.Value.IsValidation);
   }

   [Fact]
   public async Task Handle_InvalidRequest_ErrorsContainFieldKey()
   {
      var behavior = new ResultValidationBehavior<TestQuery, string>([new TestQueryValidator()]);

      var result = await behavior.Handle(
         new TestQuery("", null),
         _ => Task.FromResult(Result<string>.Ok("x")),
         CancellationToken.None);

      Assert.True(result.Error!.Value.Errors!.ContainsKey("Name"));
   }

   [Fact]
   public async Task Handle_InvalidRequest_ErrorMessageIsCorrect()
   {
      var behavior = new ResultValidationBehavior<TestQuery, string>([new TestQueryValidator()]);

      var result = await behavior.Handle(
         new TestQuery("", null),
         _ => Task.FromResult(Result<string>.Ok("x")),
         CancellationToken.None);

      Assert.Contains("Name is required.", result.Error!.Value.Errors!["Name"]);
   }

   // ════════════════════════════════════════════════════════
   // Global failures (empty PropertyName → "_" key)
   // ════════════════════════════════════════════════════════

   [Fact]
   public async Task Handle_GlobalFailure_MappedToUnderscoreKey()
   {
      var behavior = new ResultValidationBehavior<TestQuery, string>([new GlobalFailureValidator()]);

      var result = await behavior.Handle(
         new TestQuery("valid", "trigger-global"),
         _ => Task.FromResult(Result<string>.Ok("x")),
         CancellationToken.None);

      Assert.False(result.IsSuccess);
      Assert.True(result.Error!.Value.Errors!.ContainsKey("_"));
   }

   [Fact]
   public async Task Handle_GlobalFailure_MessageIsCorrect()
   {
      var behavior = new ResultValidationBehavior<TestQuery, string>([new GlobalFailureValidator()]);

      var result = await behavior.Handle(
         new TestQuery("valid", "trigger-global"),
         _ => Task.FromResult(Result<string>.Ok("x")),
         CancellationToken.None);

      Assert.Contains("Global rule failed.", result.Error!.Value.Errors!["_"]);
   }

   [Fact]
   public async Task Handle_MixedFieldAndGlobalFailures_BothKeysPresent()
   {
      var behavior = new ResultValidationBehavior<TestQuery, string>([
         new TestQueryValidator(),
         new GlobalFailureValidator()
      ]);

      var result = await behavior.Handle(
         new TestQuery("", "trigger-global"),
         _ => Task.FromResult(Result<string>.Ok("x")),
         CancellationToken.None);

      var errors = result.Error!.Value.Errors!;
      Assert.True(errors.ContainsKey("Name"));
      Assert.True(errors.ContainsKey("_"));
   }

   // ════════════════════════════════════════════════════════
   // Multiple validators — collect all failures
   // ════════════════════════════════════════════════════════

   [Fact]
   public async Task Handle_MultipleValidators_AllErrorsCollected()
   {
      var behavior = new ResultValidationBehavior<TestQuery, string>([
         new TestQueryValidator(),
         new TestQuerySecondaryValidator()
      ]);

      var result = await behavior.Handle(
         new TestQuery("", null),
         _ => Task.FromResult(Result<string>.Ok("x")),
         CancellationToken.None);

      var messages = result.Error!.Value.Errors!["Name"];
      Assert.True(messages.Length >= 2, $"Expected at least 2 messages, got {messages.Length}.");
   }

   [Fact]
   public async Task Handle_MultipleValidators_WhenFirstPassesSecondFails_ReturnsFailure()
   {
      var behavior = new ResultValidationBehavior<TestQuery, string>([
         new TestQueryValidator(),
         new TestQuerySecondaryValidator()
      ]);

      var result = await behavior.Handle(
         new TestQuery("ab", null),
         _ => Task.FromResult(Result<string>.Ok("x")),
         CancellationToken.None);

      Assert.False(result.IsSuccess);
   }

   [Fact]
   public async Task Handle_MultipleValidators_WhenAllPass_CallsNext()
   {
      var behavior = new ResultValidationBehavior<TestQuery, string>([
         new TestQueryValidator(),
         new TestQuerySecondaryValidator()
      ]);

      var called = false;

      await behavior.Handle(
         new TestQuery("valid-name", null),
         _ =>
         {
            called = true;
            return Task.FromResult(Result<string>.Ok("ok"));
         },
         CancellationToken.None);

      Assert.True(called);
   }

   // ════════════════════════════════════════════════════════
   // Field grouping — multiple rules on same field
   // ════════════════════════════════════════════════════════

   [Fact]
   public async Task Handle_MultipleRulesOnSameField_GroupedUnderOneKey()
   {
      var behavior = new ResultValidationBehavior<TestQuery, string>([new MultiRuleValidator()]);

      var result = await behavior.Handle(
         new TestQuery("", null),
         _ => Task.FromResult(Result<string>.Ok("x")),
         CancellationToken.None);

      var errors = result.Error!.Value.Errors!;
      Assert.Single(errors);
      Assert.True(errors["Name"].Length > 1);
   }

   // ════════════════════════════════════════════════════════
   // Different T types
   // ════════════════════════════════════════════════════════

   [Fact]
   public async Task Handle_IntResponseType_SuccessPath()
   {
      var behavior = new ResultValidationBehavior<TestQuery, int>([new TestQueryValidator()]);

      var result = await behavior.Handle(
         new TestQuery("valid", null),
         _ => Task.FromResult(Result<int>.Ok(42)),
         CancellationToken.None);

      Assert.True(result.IsSuccess);
      Assert.Equal(42, result.Value);
   }

   [Fact]
   public async Task Handle_IntResponseType_FailurePath()
   {
      var behavior = new ResultValidationBehavior<TestQuery, int>([new TestQueryValidator()]);

      var result = await behavior.Handle(
         new TestQuery("", null),
         _ => Task.FromResult(Result<int>.Ok(0)),
         CancellationToken.None);

      Assert.False(result.IsSuccess);
      Assert.Equal(ErrorType.BadRequest, result.Error!.Value.Type);
   }

   // ════════════════════════════════════════════════════════
   // Test types
   // ════════════════════════════════════════════════════════

   private sealed record TestQuery(string Name, string? Tag) : IRequest<Result<string>>;

   private sealed class TestQueryValidator : AbstractValidator<TestQuery>
   {
      public TestQueryValidator() =>
         RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required.");
   }

   private sealed class TestQuerySecondaryValidator : AbstractValidator<TestQuery>
   {
      public TestQuerySecondaryValidator() =>
         RuleFor(x => x.Name)
            .MinimumLength(4)
            .WithMessage("Name must be at least 4 characters.");
   }

   private sealed class MultiRuleValidator : AbstractValidator<TestQuery>
   {
      public MultiRuleValidator()
      {
         RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required.");
         RuleFor(x => x.Name)
            .MinimumLength(4)
            .WithMessage("Name must be at least 4 characters.");
      }
   }

   private sealed class GlobalFailureValidator : AbstractValidator<TestQuery>
   {
      public GlobalFailureValidator()
      {
         // .WithName(string.Empty) is rejected by FluentValidation — use Custom() to emit
         // a failure with an empty PropertyName (the "global failure" case).
         RuleFor(x => x)
            .Custom((x, ctx) =>
            {
               if (x.Tag == "trigger-global")
               {
                  ctx.AddFailure(new ValidationFailure("", "Global rule failed."));
               }
            });
      }
   }
}
using FluentValidation;
using ResultCrafter.FluentValidation;

namespace ResultCrafter.Tests.FluentValidation;

public sealed class ValidatorExtensionsTests
{
    private readonly IValidator<CreateOrderRequest> _validator = new CreateOrderRequestValidator();

    // ── Valid input ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateToResultAsync_ValidInput_ReturnsNull()
    {
        var request = new CreateOrderRequest("user@example.com", 5);

        var error = await _validator.ValidateToResultAsync(request, TestContext.Current.CancellationToken);

        Assert.Null(error);
    }

    // ── Invalid input returns Error ────────────────────────────────────────────

    [Fact]
    public async Task ValidateToResultAsync_InvalidInput_ReturnsError()
    {
        var request = new CreateOrderRequest(null, 0);

        var error = await _validator.ValidateToResultAsync(request, TestContext.Current.CancellationToken);

        Assert.NotNull(error);
    }

    [Fact]
    public async Task ValidateToResultAsync_InvalidInput_ReturnsBadRequestType()
    {
        var request = new CreateOrderRequest(null, 0);

        var error = await _validator.ValidateToResultAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorType.BadRequest, error!.Type);
    }

    [Fact]
    public async Task ValidateToResultAsync_InvalidInput_IsValidationIsTrue()
    {
        var request = new CreateOrderRequest(null, 0);

        var error = await _validator.ValidateToResultAsync(request, TestContext.Current.CancellationToken);

        Assert.True(error!.IsValidation);
    }

    // ── Field keys match property names ───────────────────────────────────────

    [Fact]
    public async Task ValidateToResultAsync_ContainsFieldKeyForFailedProperty()
    {
        var request = new CreateOrderRequest("user@example.com", 0); // only Quantity fails

        var error = await _validator.ValidateToResultAsync(request, TestContext.Current.CancellationToken);

        Assert.NotNull(error!.Errors);
        Assert.True(error.Errors!.ContainsKey("Quantity"));
    }

    [Fact]
    public async Task ValidateToResultAsync_MultipleFailures_AllFieldKeysPresent()
    {
        var request = new CreateOrderRequest(null, 0); // both fail

        var error = await _validator.ValidateToResultAsync(request, TestContext.Current.CancellationToken);

        Assert.NotNull(error!.Errors);
        Assert.True(error.Errors!.ContainsKey("CustomerEmail"));
        Assert.True(error.Errors!.ContainsKey("Quantity"));
    }

    // ── Error messages ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateToResultAsync_ErrorMessages_MatchValidatorMessages()
    {
        var request = new CreateOrderRequest(null, 5); // only email fails

        var error = await _validator.ValidateToResultAsync(request, TestContext.Current.CancellationToken);

        var messages = error!.Errors!["CustomerEmail"];
        Assert.Contains("Email is required.", messages);
    }

    [Fact]
    public async Task ValidateToResultAsync_MultipleRulesOnSameField_AllMessagesIncluded()
    {
        // "not-an-email" passes NotEmpty but fails EmailAddress
        var request = new CreateOrderRequest("not-an-email", 5);

        var error = await _validator.ValidateToResultAsync(request, TestContext.Current.CancellationToken);

        var messages = error!.Errors!["CustomerEmail"];
        Assert.Contains("Email must be a valid address.", messages);
    }

    // ── Cancellation is forwarded ──────────────────────────────────────────────

    [Fact]
    public async Task ValidateToResultAsync_WithCancelledToken_ThrowsOperationCancelled()
    {
        var request = new CreateOrderRequest("user@example.com", 5);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _validator.ValidateToResultAsync(request, cts.Token));
    }
    // ── Fixture ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateToResultAsync_ModelLevelAndDuplicateMessages_AreNormalized()
    {
        var error = await new ModelLevelValidator().ValidateToResultAsync(
            new CreateOrderRequest("a@b.c", 0), TestContext.Current.CancellationToken);

        Assert.Equal(["Order is invalid."], error!.Errors!["_"]);
        Assert.Equal(["Bad."], error.Errors["Quantity"]);
    }

    // ── Codes and args ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateToResultAsync_FieldErrors_AlignWithMessages()
    {
        var error = await new ModelLevelValidator().ValidateToResultAsync(
            new CreateOrderRequest("a@b.c", 0), TestContext.Current.CancellationToken);

        foreach (var (key, messages) in error!.Errors!)
        {
            Assert.Equal(messages, error.FieldErrors![key].Select(e => e.Message).ToArray());
        }
    }

    [Fact]
    public async Task ValidateToResultAsync_RuleWithoutErrorCode_UsesValidatorName()
    {
        var error = await new CodesValidator().ValidateToResultAsync(Valid() with { Name = "" },
            TestContext.Current.CancellationToken);

        var fieldError = Assert.Single(error!.FieldErrors!["Name"]);
        Assert.Equal("NotEmptyValidator", fieldError.Code);
        Assert.Empty(fieldError.Args);
    }

    [Fact]
    public async Task ValidateToResultAsync_WithErrorCode_KeepsItAsWritten()
    {
        var error = await new CodesValidator().ValidateToResultAsync(Valid() with { Email = "taken@example.com" },
            TestContext.Current.CancellationToken);

        Assert.Equal("email_in_use", Assert.Single(error!.FieldErrors!["Email"]).Code);
    }

    [Fact]
    public async Task ValidateToResultAsync_Length_SendsLimitsButNotTheInput()
    {
        var error = await new CodesValidator().ValidateToResultAsync(Valid() with { Name = "secret-name" },
            TestContext.Current.CancellationToken);

        var fieldError = Assert.Single(error!.FieldErrors!["Name"]);
        Assert.Equal("MaximumLengthValidator", fieldError.Code);
        Assert.Equal(0, fieldError.Args["minLength"]);
        Assert.Equal(5, fieldError.Args["maxLength"]);
        Assert.Equal(2, fieldError.Args.Count);
    }

    [Fact]
    public async Task ValidateToResultAsync_FixedComparisonAndRange_SendTheirLimits()
    {
        var error = await new CodesValidator().ValidateToResultAsync(Valid() with { Quantity = 0, Discount = 2.5m },
            TestContext.Current.CancellationToken);

        var quantity = Assert.Single(error!.FieldErrors!["Quantity"]);
        Assert.Equal("GreaterThanValidator", quantity.Code);
        Assert.Equal(0, quantity.Args["comparisonValue"]);
        Assert.Single(quantity.Args);

        var discount = Assert.Single(error.FieldErrors["Discount"]);
        Assert.Equal("InclusiveBetweenValidator", discount.Code);
        Assert.Equal(0m, discount.Args["from"]);
        Assert.Equal(1m, discount.Args["to"]);
        Assert.Equal(2, discount.Args.Count);
    }

    [Fact]
    public async Task ValidateToResultAsync_ComparisonWithAnotherProperty_DropsItsValue()
    {
        var error = await new CodesValidator().ValidateToResultAsync(
            Valid() with { Password = "hunter2-secret", ConfirmPassword = "other" },
            TestContext.Current.CancellationToken);

        var fieldError = Assert.Single(error!.FieldErrors!["ConfirmPassword"]);
        Assert.Equal("EqualValidator", fieldError.Code);
        Assert.Empty(fieldError.Args);
    }

    [Fact]
    public async Task ValidateToResultAsync_StringComparisonFromLambda_DropsItsValue()
    {
        var error = await new CodesValidator().ValidateToResultAsync(
            Valid() with { Password = "  hunter2-secret  ", Hint = "hunter2-secret" },
            TestContext.Current.CancellationToken);

        var fieldError = Assert.Single(error!.FieldErrors!["Hint"]);
        Assert.Equal("NotEqualValidator", fieldError.Code);
        Assert.Empty(fieldError.Args);
    }

    [Fact]
    public async Task ValidateToResultAsync_FailureAddedByHand_HasNoCode()
    {
        var error = await new CodesValidator().ValidateToResultAsync(Valid() with { Quantity = 13 },
            TestContext.Current.CancellationToken);

        var fieldError = Assert.Single(error!.FieldErrors!["Quantity"]);
        Assert.Null(fieldError.Code);
        Assert.Equal("Thirteen is unlucky.", fieldError.Message);
    }

    private static Signup Valid()
    {
        return new Signup("Ann", "ann@example.com", 1, 0.5m, "hunter2", "hunter2", "a hint");
    }

    private sealed record Signup(
        string Name,
        string Email,
        int Quantity,
        decimal Discount,
        string Password,
        string ConfirmPassword,
        string Hint);

    private sealed class CodesValidator : AbstractValidator<Signup>
    {
        public CodesValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(5);
            RuleFor(x => x.Email).NotEqual("taken@example.com").WithErrorCode("email_in_use");
            RuleFor(x => x.Quantity).GreaterThan(0);
            RuleFor(x => x.Quantity).Custom((quantity, context) =>
            {
                if (quantity == 13) context.AddFailure("Quantity", "Thirteen is unlucky.");
            });
            RuleFor(x => x.Discount).InclusiveBetween(0m, 1m);
            RuleFor(x => x.ConfirmPassword).Equal(x => x.Password);
            RuleFor(x => x.Hint).NotEqual(x => x.Password.Trim());
        }
    }

    private sealed record CreateOrderRequest(string? CustomerEmail, int Quantity);

    // A model-level rule (empty property name), a repeated message and a whitespace-only message.
    private sealed class ModelLevelValidator : AbstractValidator<CreateOrderRequest>
    {
        public ModelLevelValidator()
        {
            RuleFor(x => x).Must(_ => false).WithMessage("Order is invalid.");
            RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Bad.");
            RuleFor(x => x.Quantity).GreaterThan(1).WithMessage("Bad.");
            RuleFor(x => x.Quantity).GreaterThan(2).WithMessage(" ");
        }
    }

    private sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
    {
        public CreateOrderRequestValidator()
        {
            RuleFor(x => x.CustomerEmail)
                .NotEmpty()
                .WithMessage("Email is required.")
                .EmailAddress()
                .WithMessage("Email must be a valid address.");

            RuleFor(x => x.Quantity)
                .GreaterThan(0)
                .WithMessage("Quantity must be greater than 0.");
        }
    }
}

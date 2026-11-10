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

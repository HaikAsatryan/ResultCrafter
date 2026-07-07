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

        Assert.Equal(ErrorType.BadRequest, error!.Value.Type);
    }

    [Fact]
    public async Task ValidateToResultAsync_InvalidInput_IsValidationIsTrue()
    {
        var request = new CreateOrderRequest(null, 0);

        var error = await _validator.ValidateToResultAsync(request, TestContext.Current.CancellationToken);

        Assert.True(error!.Value.IsValidation);
    }

    // ── Field keys match property names ───────────────────────────────────────

    [Fact]
    public async Task ValidateToResultAsync_ContainsFieldKeyForFailedProperty()
    {
        var request = new CreateOrderRequest("user@example.com", 0); // only Quantity fails

        var error = await _validator.ValidateToResultAsync(request, TestContext.Current.CancellationToken);

        Assert.NotNull(error!.Value.Errors);
        Assert.True(error.Value.Errors!.ContainsKey("Quantity"));
    }

    [Fact]
    public async Task ValidateToResultAsync_MultipleFailures_AllFieldKeysPresent()
    {
        var request = new CreateOrderRequest(null, 0); // both fail

        var error = await _validator.ValidateToResultAsync(request, TestContext.Current.CancellationToken);

        Assert.NotNull(error!.Value.Errors);
        Assert.True(error.Value.Errors!.ContainsKey("CustomerEmail"));
        Assert.True(error.Value.Errors!.ContainsKey("Quantity"));
    }

    // ── Error messages ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateToResultAsync_ErrorMessages_MatchValidatorMessages()
    {
        var request = new CreateOrderRequest(null, 5); // only email fails

        var error = await _validator.ValidateToResultAsync(request, TestContext.Current.CancellationToken);

        var messages = error!.Value.Errors!["CustomerEmail"];
        Assert.Contains("Email is required.", messages);
    }

    [Fact]
    public async Task ValidateToResultAsync_MultipleRulesOnSameField_AllMessagesIncluded()
    {
        // "not-an-email" passes NotEmpty but fails EmailAddress
        var request = new CreateOrderRequest("not-an-email", 5);

        var error = await _validator.ValidateToResultAsync(request, TestContext.Current.CancellationToken);

        var messages = error!.Value.Errors!["CustomerEmail"];
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

    private sealed record CreateOrderRequest(string? CustomerEmail, int Quantity);

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

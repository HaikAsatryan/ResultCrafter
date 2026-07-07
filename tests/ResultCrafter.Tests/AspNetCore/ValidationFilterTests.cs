using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ResultCrafter.AspNetCore;
using ResultCrafter.AspNetCore.FluentValidation;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace ResultCrafter.Tests.AspNetCore;

public sealed class ValidationFilterTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private const string NextSentinel = "next-was-called";

    private static readonly EndpointFilterDelegate Next = _ => ValueTask.FromResult<object?>(NextSentinel);
    // ── Direct InvokeAsync: pass-through paths ─────────────────────────────────

    [Fact]
    public async Task NoMatchingArgument_PassesThrough()
    {
        var provider = BuildProvider(true, JsonNamingPolicy.CamelCase);
        var context = EndpointFilterInvocationContext.Create(new DefaultHttpContext { RequestServices = provider });

        var result = await new ValidationFilter<CreateOrderRequest>().InvokeAsync(context, Next);

        Assert.Equal(NextSentinel, result);
    }

    [Fact]
    public async Task NoValidatorRegistered_WithMatchingArgument_Throws()
    {
        var provider = BuildProvider(false, JsonNamingPolicy.CamelCase);
        var context = EndpointFilterInvocationContext.Create(
            new DefaultHttpContext { RequestServices = provider },
            new CreateOrderRequest(null, 0));

        // A TRequest argument is present but no validator is registered: fail fast rather than
        // silently skipping validation.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ValidationFilter<CreateOrderRequest>().InvokeAsync(context, Next).AsTask());
    }

    [Fact]
    public async Task ValidRequest_PassesThrough()
    {
        var provider = BuildProvider(true, JsonNamingPolicy.CamelCase);
        var context = EndpointFilterInvocationContext.Create(
            new DefaultHttpContext { RequestServices = provider },
            new CreateOrderRequest("user@example.com", 5));

        var result = await new ValidationFilter<CreateOrderRequest>().InvokeAsync(context, Next);

        Assert.Equal(NextSentinel, result);
    }

    // ── Direct InvokeAsync: failure path + key casing ──────────────────────────

    [Fact]
    public async Task InvalidRequest_ReturnsProblem_WithCamelCaseKeys()
    {
        var provider = BuildProvider(true, JsonNamingPolicy.CamelCase);
        var context = EndpointFilterInvocationContext.Create(
            new DefaultHttpContext { RequestServices = provider },
            new CreateOrderRequest(null, 0));

        var result = await new ValidationFilter<CreateOrderRequest>().InvokeAsync(context, Next);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(400, problem.StatusCode);

        var errors = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(
            problem.ProblemDetails.Extensions["errors"]);
        Assert.Contains("customerEmail", errors.Keys);
        Assert.Contains("quantity", errors.Keys);
        Assert.DoesNotContain("CustomerEmail", errors.Keys);
    }

    [Fact]
    public async Task InvalidRequest_NullNamingPolicy_KeepsClrPropertyNames()
    {
        var provider = BuildProvider(true, null);
        var context = EndpointFilterInvocationContext.Create(
            new DefaultHttpContext { RequestServices = provider },
            new CreateOrderRequest(null, 0));

        var result = await new ValidationFilter<CreateOrderRequest>().InvokeAsync(context, Next);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        var errors = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(
            problem.ProblemDetails.Extensions["errors"]);
        Assert.Contains("CustomerEmail", errors.Keys);
        Assert.DoesNotContain("customerEmail", errors.Keys);
    }

    // ── End-to-end wiring through both builder overloads ───────────────────────

    [Fact]
    public async Task RouteHandlerBuilder_WithValidation_InvalidBody_Returns400CamelCase()
    {
        await using var app = BuildEndpointApp(a =>
            a.MapPost("/orders", (CreateOrderRequest req) => Results.Ok(req)).WithValidation<CreateOrderRequest>());

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/orders", new CreateOrderRequest(null, 0),
            TestContext.Current.CancellationToken);

        await AssertCamelCaseValidationProblem(response);
    }

    [Fact]
    public async Task RouteGroupBuilder_WithValidation_InvalidBody_Returns400CamelCase()
    {
        await using var app = BuildEndpointApp(a =>
        {
            var group = a.MapGroup("/group").WithValidation<CreateOrderRequest>();
            group.MapPost("/orders", (CreateOrderRequest req) => Results.Ok(req));
        });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/group/orders", new CreateOrderRequest(null, 0),
            TestContext.Current.CancellationToken);

        await AssertCamelCaseValidationProblem(response);
    }

    [Fact]
    public async Task WithValidation_ValidBody_ReachesHandler()
    {
        await using var app = BuildEndpointApp(a =>
            a.MapPost("/orders", (CreateOrderRequest req) => Results.Ok(req)).WithValidation<CreateOrderRequest>());

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/orders", new CreateOrderRequest("user@example.com", 5),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Registration helper ────────────────────────────────────────────────────

    [Fact]
    public void AddResultCrafterValidation_RegistersValidatorsFromCallingAssembly()
    {
        var services = new ServiceCollection();

        services.AddResultCrafterValidation();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IValidator<CreateOrderRequest>>());
    }

    private static IServiceProvider BuildProvider(bool withValidator, JsonNamingPolicy? policy)
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<JsonOptions>(o => o.SerializerOptions.PropertyNamingPolicy = policy);

        if (withValidator) services.AddSingleton<IValidator<CreateOrderRequest>, CreateOrderRequestValidator>();

        return services.BuildServiceProvider();
    }

    private static WebApplication BuildEndpointApp(Action<WebApplication> map)
    {
        return TestApp.Build(
            b =>
            {
                b.Services.AddResultCrafter();
                b.Services.AddSingleton<IValidator<CreateOrderRequest>, CreateOrderRequestValidator>();
            },
            a =>
            {
                a.UseResultCrafter();
                map(a);
            });
    }

    private static async Task AssertCamelCaseValidationProblem(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = doc.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("customerEmail", out _));
        Assert.False(errors.TryGetProperty("CustomerEmail", out _));
    }
}

// Top-level public types so Minimal API model binding / System.Text.Json can construct them and
// FluentValidation assembly scanning (which excludes internal types by default) can discover the validator.
public sealed record CreateOrderRequest(string? CustomerEmail, int Quantity);

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CustomerEmail)
            .NotEmpty()
            .WithMessage("Email is required.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than 0.");
    }
}

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

namespace ResultCrafter.Tests.AspNetCore.FluentValidation;

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

        var errors = Assert.IsType<IReadOnlyDictionary<string, string[]>>(
            problem.ProblemDetails.Extensions["errors"], exactMatch: false);
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
        var errors = Assert.IsType<IReadOnlyDictionary<string, string[]>>(
            problem.ProblemDetails.Extensions["errors"], exactMatch: false);
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

    // ── Binding already failed: the filter must not validate ──────────────────
    // In Production, endpoint filters run even after a binding failure, with the response status
    // already 400. Validating then either wastes a call or replaces the binding
    // error with the validator's field errors.

    [Theory]
    [InlineData("endpoint", """{"customerEmail":null,"quantity":0}""")]
    [InlineData("endpoint", """{"customerEmail":"user@example.com","quantity":5}""")]
    [InlineData("group", """{"customerEmail":null,"quantity":0}""")]
    [InlineData("group", """{"customerEmail":"user@example.com","quantity":5}""")]
    public async Task BindingFailure_SkipsValidation_ReturnsBindingBadRequest(string registration, string body)
    {
        var validator = new CountingValidator();
        await using var app = TestApp.Build(
            b =>
            {
                b.Services.AddResultCrafter();
                b.Services.AddSingleton<IValidator<CreateOrderRequest>>(validator);
            },
            a =>
            {
                a.UseResultCrafter();
                if (registration == "endpoint")
                    a.MapPost("/orders", (CreateOrderRequest req, int count) => Results.Ok(count))
                        .WithValidation<CreateOrderRequest>();
                else
                    a.MapGroup("/").WithValidation<CreateOrderRequest>()
                        .MapPost("/orders", (CreateOrderRequest req, int count) => Results.Ok(count));
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/orders?count=abc", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("bad_request", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal("the_request_was_invalid_or_cannot_be_otherwise_served",
            doc.RootElement.GetProperty("detail").GetString());
        Assert.False(doc.RootElement.TryGetProperty("errors", out _));
        Assert.Equal(0, validator.Calls);
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

    [Fact]
    public void AddResultCrafterValidation_TakesAssembliesWithoutALifetime()
    {
        var services = new ServiceCollection();

        services.AddResultCrafterValidation(typeof(ValidationFilterTests).Assembly);

        Assert.Contains(services, d => d.ServiceType == typeof(IValidator<CreateOrderRequest>) &&
                                       d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddResultCrafterValidation_WithLifetime_UsesIt()
    {
        var services = new ServiceCollection();

        services.AddResultCrafterValidation(ServiceLifetime.Singleton);

        // Contains first: Assert.All passes on an empty list, so it alone would not prove the calling assembly
        // was scanned.
        Assert.Contains(services, d => d.ServiceType == typeof(IValidator<CreateOrderRequest>));
        Assert.All(services.Where(d => d.ServiceType == typeof(IValidator<CreateOrderRequest>)),
            d => Assert.Equal(ServiceLifetime.Singleton, d.Lifetime));
    }

    // Explicit assemblies replace the calling assembly. ResultCrafter.Core has no validators, so the test assembly's
    // validators must not be registered.
    [Fact]
    public void AddResultCrafterValidation_WithAssemblies_ScansOnlyThose()
    {
        var withoutLifetime = new ServiceCollection();
        var withLifetime = new ServiceCollection();

        withoutLifetime.AddResultCrafterValidation(typeof(Error).Assembly);
        withLifetime.AddResultCrafterValidation(ServiceLifetime.Singleton, typeof(Error).Assembly);

        Assert.DoesNotContain(withoutLifetime, d => d.ServiceType == typeof(IValidator<CreateOrderRequest>));
        Assert.DoesNotContain(withLifetime, d => d.ServiceType == typeof(IValidator<CreateOrderRequest>));
    }

    [Fact]
    public void AddResultCrafterValidation_RegistersInternalValidators()
    {
        var services = new ServiceCollection();

        services.AddResultCrafterValidation();

        using var provider = services.BuildServiceProvider();
        Assert.IsType<InternalScanRequestValidator>(provider.GetService<IValidator<InternalScanRequest>>());
    }

    // ── Nested and indexed keys, end to end through FluentValidation ──────────

    [Theory]
    [InlineData("camel", "shippingAddress.zipCode", "lines[1].productName")]
    [InlineData("snake", "shipping_address.zip_code", "lines[1].product_name")]
    public async Task InvalidNestedRequest_KeysFollowPolicyPerSegment(string policy, string zipKey, string lineKey)
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<JsonOptions>(o => o.SerializerOptions.PropertyNamingPolicy =
            policy == "camel" ? JsonNamingPolicy.CamelCase : JsonNamingPolicy.SnakeCaseLower);
        services.AddSingleton<IValidator<NestedOrder>, NestedOrderValidator>();
        var context = EndpointFilterInvocationContext.Create(
            new DefaultHttpContext { RequestServices = services.BuildServiceProvider() },
            new NestedOrder(new ShipTo(null), [new OrderLine("a"), new OrderLine(null)]));

        var result = await new ValidationFilter<NestedOrder>().InvokeAsync(context, Next);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        var errors = Assert.IsType<IReadOnlyDictionary<string, string[]>>(problem.ProblemDetails.Extensions["errors"],
            exactMatch: false);
        Assert.Equal(2, errors.Count);
        Assert.Contains(zipKey, errors.Keys);
        Assert.Contains(lineKey, errors.Keys);
    }

    // ── Codes follow their messages through key alignment ─────────────────────

    [Theory]
    [InlineData("camel", "shippingAddress.zipCode", "lines[1].productName")]
    [InlineData("snake", "shipping_address.zip_code", "lines[1].product_name")]
    public async Task InvalidNestedRequest_FieldErrorsKeepCodesUnderAlignedKeys(string policy, string zipKey,
        string lineKey)
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<JsonOptions>(o => o.SerializerOptions.PropertyNamingPolicy =
            policy == "camel" ? JsonNamingPolicy.CamelCase : JsonNamingPolicy.SnakeCaseLower);
        services.AddSingleton<IValidator<NestedOrder>, NestedOrderValidator>();
        var context = EndpointFilterInvocationContext.Create(
            new DefaultHttpContext { RequestServices = services.BuildServiceProvider() },
            new NestedOrder(new ShipTo(null), [new OrderLine("a"), new OrderLine(null)]));

        var result = await new ValidationFilter<NestedOrder>().InvokeAsync(context, Next);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        var codes = Assert.IsType<JsonElement>(problem.ProblemDetails.Extensions["errorCodes"]);
        Assert.Equal("NotEmptyValidator", codes.GetProperty(zipKey)[0].GetProperty("code").GetString());
        Assert.Equal("NotEmptyValidator", codes.GetProperty(lineKey)[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task WithValidation_InvalidBody_SendsErrorCodesNextToErrors()
    {
        await using var app = TestApp.Build(
            b =>
            {
                b.Services.AddResultCrafter();
                b.Services.AddSingleton<IValidator<CreateOrderRequest>, CodedOrderValidator>();
            },
            a =>
            {
                a.UseResultCrafter();
                a.MapPost("/orders", (CreateOrderRequest req) => Results.Ok(req)).WithValidation<CreateOrderRequest>();
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/orders", new CreateOrderRequest("a-very-long@example.com", 0),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.String, root.GetProperty("errors").GetProperty("customerEmail")[0].ValueKind);
        Assert.Equal("""[{"code":"MaximumLengthValidator","args":{"minLength":0,"maxLength":10}}]""",
            root.GetProperty("errorCodes").GetProperty("customerEmail").GetRawText());
        Assert.Equal("""[{"code":"quantity_too_low","args":{"comparisonValue":0}}]""",
            root.GetProperty("errorCodes").GetProperty("quantity").GetRawText());
        Assert.DoesNotContain("a-very-long@example.com", root.GetProperty("errorCodes").GetRawText());
    }

    private static ServiceProvider BuildProvider(bool withValidator, JsonNamingPolicy? policy)
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

// Top-level public types so Minimal API model binding and System.Text.Json can construct them.
public sealed record CreateOrderRequest(string? CustomerEmail, int Quantity);

public sealed record InternalScanRequest(string? Name);

/// <summary>Internal on purpose: AddResultCrafterValidation() must find internal validators too.</summary>
internal sealed class InternalScanRequestValidator : AbstractValidator<InternalScanRequest>
{
    public InternalScanRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}

public sealed record ShipTo(string? ZipCode);

public sealed record OrderLine(string? ProductName);

public sealed record NestedOrder(ShipTo ShippingAddress, List<OrderLine> Lines);

public sealed class NestedOrderValidator : AbstractValidator<NestedOrder>
{
    public NestedOrderValidator()
    {
        RuleFor(x => x.ShippingAddress.ZipCode).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line => line.RuleFor(y => y.ProductName).NotEmpty());
    }
}

/// <summary>Counts validation calls so a test can prove the filter skipped validation.</summary>
internal sealed class CountingValidator : AbstractValidator<CreateOrderRequest>
{
    private int _calls;

    public CountingValidator()
    {
        RuleFor(x => x).Must(_ =>
        {
            Interlocked.Increment(ref _calls);
            return true;
        });
        RuleFor(x => x.CustomerEmail).NotEmpty();
    }

    public int Calls => Volatile.Read(ref _calls);
}

internal sealed class CodedOrderValidator : AbstractValidator<CreateOrderRequest>
{
    public CodedOrderValidator()
    {
        RuleFor(x => x.CustomerEmail).MaximumLength(10);
        RuleFor(x => x.Quantity).GreaterThan(0).WithErrorCode("quantity_too_low");
    }
}

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

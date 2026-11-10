using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ResultCrafter.AspNetCore;
using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     The built-in Minimal API validation (<c>AddValidation()</c>) writes the framework title, no detail,
///     and C# property names. It gets the house BadRequest shape with keys that match the JSON on the wire.
/// </summary>
public sealed class BuiltInValidationTests
{
    [Fact]
    public async Task AddValidation_InvalidBody_ReturnsHouseShapeWithJsonKeys()
    {
        await using var app = TestApp.Build(
            b =>
            {
                b.Services.AddResultCrafter();
                b.Services.AddValidation();
            },
            a =>
            {
                a.UseResultCrafter();
                a.MapPost("/annotated", (AnnotatedRequest req) => Results.Ok(req.Name));
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        using var content = new StringContent("""{"name":null,"count":50}""", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/annotated", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = doc.RootElement;
        Assert.Equal(HttpErrorCatalog.TypeUri(ErrorType.BadRequest), root.GetProperty("type").GetString());
        Assert.Equal("bad_request", root.GetProperty("title").GetString());
        Assert.Equal("the_request_was_invalid_or_cannot_be_otherwise_served",
            root.GetProperty("detail").GetString());

        var errors = root.GetProperty("errors");
        Assert.Equal(["count", "name"], errors.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal("The Name field is required.", errors.GetProperty("name")[0].GetString());
        Assert.Equal("The field Count must be between 1 and 10.", errors.GetProperty("count")[0].GetString());
    }

    // MVC's automatic [ApiController] 400 is the same framework validation problem, so controllers get the
    // same shape as Minimal APIs.
    [Fact]
    public async Task ApiController_InvalidModel_ReturnsHouseShapeWithJsonKeys()
    {
        await using var app = TestApp.Build(
            b =>
            {
                b.Services.AddResultCrafter();
                b.Services.AddControllers().AddApplicationPart(typeof(AnnotatedController).Assembly);
            },
            a =>
            {
                a.UseResultCrafter();
                a.MapControllers();
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        using var content = new StringContent("""{"name":null,"count":50}""", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/annotated-controller", content,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("bad_request", doc.RootElement.GetProperty("title").GetString());
        var errors = doc.RootElement.GetProperty("errors");
        Assert.Equal(["count", "name"], errors.EnumerateObject().Select(p => p.Name).Order());
    }

    // MVC and Minimal APIs keep separate JSON options. With a different policy on each side, the keys must follow
    // the options of the endpoint that answered. The Minimal API side uses an app ValidationProblem, which takes the
    // same path as AddValidation(): a second AddValidation() call site breaks the .NET 10 validation generator.
    [Theory]
    [InlineData("/minimal-validation", "COUNT", "NAME")]
    [InlineData("/annotated-controller", "count", "name")]
    public async Task ValidationKeys_FollowTheAnsweringEndpointsJsonOptions(string path, string countKey,
        string nameKey)
    {
        await using var app = TestApp.Build(
            b =>
            {
                b.Services.AddResultCrafter();
                b.Services.ConfigureHttpJsonOptions(o =>
                    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper);
                b.Services.AddControllers().AddApplicationPart(typeof(AnnotatedController).Assembly);
            },
            a =>
            {
                a.UseResultCrafter();
                a.MapPost("/minimal-validation", () => TypedResults.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["Name"] = ["The Name field is required."],
                        ["Count"] = ["The field Count must be between 1 and 10."]
                    }));
                a.MapControllers();
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        using var content = new StringContent("""{"name":null,"count":50}""", Encoding.UTF8, "application/json");
        var response = await client.PostAsync(path, content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var errors = doc.RootElement.GetProperty("errors");
        Assert.Equal([countKey, nameKey], errors.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    }
}

/// <summary>Model-validated by MVC's [ApiController] behavior.</summary>
[ApiController]
[Route("annotated-controller")]
public sealed class AnnotatedController : ControllerBase
{
    /// <summary>Echoes the name; MVC answers 400 before this runs when the model is invalid.</summary>
    [HttpPost]
    public string Post(AnnotatedControllerRequest req)
    {
        return req.Name!;
    }
}

public sealed record AnnotatedRequest([property: Required] string? Name, [property: Range(1, 10)] int Count);

// MVC reads validation attributes from record constructor parameters, not from properties.
public sealed record AnnotatedControllerRequest([Required] string? Name, [Range(1, 10)] int Count);

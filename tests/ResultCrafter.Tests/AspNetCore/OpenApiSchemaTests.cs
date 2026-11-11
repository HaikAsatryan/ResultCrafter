using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ResultCrafter.AspNetCore;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     The <c>Produces*</c> helpers document the house error body, not the plain ProblemDetails schema, so
///     API clients see <c>traceId</c>, <c>errors</c> and <c>errorCodes</c>.
/// </summary>
public sealed class OpenApiSchemaTests
{
    [Fact]
    public async Task ProducesHelpers_DeclareProblemResponseSchema()
    {
        await using var app = TestApp.Build(
            b =>
            {
                b.Services.AddResultCrafter();
                b.Services.AddOpenApi();
            },
            a =>
            {
                a.UseResultCrafter();
                a.MapOpenApi();
                a.MapGet("/items/{id:int}", (int id) => TypedResults.Ok(id)).ProducesNotFound();
                a.MapPost("/items", (OpenApiItem item) => TypedResults.Ok(item.Name)).ProducesBadRequest();
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var json = await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("#/components/schemas/ProblemResponse", ProblemSchemaRef(root, "/items/{id}", "get", "404"));
        Assert.Equal("#/components/schemas/ProblemResponse", ProblemSchemaRef(root, "/items", "post", "400"));

        var schemas = root.GetProperty("components").GetProperty("schemas");
        var properties = schemas.GetProperty("ProblemResponse").GetProperty("properties");
        Assert.True(properties.TryGetProperty("traceId", out _));
        Assert.True(properties.TryGetProperty("errors", out _));

        // errorCodes: field name to an array of FieldErrorCode entries.
        var entryRef = properties.GetProperty("errorCodes").GetProperty("additionalProperties").GetProperty("items")
            .GetProperty("$ref").GetString();
        Assert.Equal("#/components/schemas/FieldErrorCode", entryRef);
        var entry = schemas.GetProperty("FieldErrorCode").GetProperty("properties");
        Assert.True(entry.TryGetProperty("code", out _));
        Assert.True(entry.TryGetProperty("args", out _));
    }

    private static string? ProblemSchemaRef(JsonElement root, string path, string method, string status)
    {
        return root.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("responses")
            .GetProperty(status).GetProperty("content").GetProperty("application/problem+json")
            .GetProperty("schema").GetProperty("$ref").GetString();
    }
}

public sealed record OpenApiItem(string Name);

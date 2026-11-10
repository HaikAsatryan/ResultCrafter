using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using ResultCrafter.AspNetCore;
using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     A JSON body that cannot be read gives the house BadRequest shape in both environments. On .NET 11
///     the framework reports which field failed, so the response carries field keys with fixed messages;
///     on .NET 10 it does not, so the body stays generic. Raw parser text never reaches the client.
/// </summary>
public sealed class JsonBodyErrorTests
{
    [Theory]
    [InlineData("Production", "{bad", "_", "invalid_json")]
    [InlineData("Production", """{"name":"x","count":"abc"}""", "count", "invalid_value")]
    [InlineData("Production", """{"name":123,"count":1}""", "name", "invalid_value")]
    [InlineData("Production", """{"name":"x","count":1,"address":{"zip":true}}""", "address.zip", "invalid_value")]
    [InlineData("Production", """{"name":"x","count":1,"items":[{"name":5}]}""", "items[0].name", "invalid_value")]
    [InlineData("Development", "{bad", "_", "invalid_json")]
    [InlineData("Development", """{"name":"x","count":"abc"}""", "count", "invalid_value")]
    [InlineData("Development", """{"name":123,"count":1}""", "name", "invalid_value")]
    [InlineData("Development", """{"name":"x","count":1,"address":{"zip":true}}""", "address.zip", "invalid_value")]
    [InlineData("Development", """{"name":"x","count":1,"items":[{"name":5}]}""", "items[0].name", "invalid_value")]
    public async Task UnreadableJsonBody_ReturnsHouseBadRequest(string environment,
        string body,
        string expectedKey,
        string expectedMessage)
    {
        await using var app = TestApp.Build(
            b => b.Services.AddResultCrafter(),
            a =>
            {
                a.UseResultCrafter();
                a.MapPost("/typed", (TypedBodyRequest req) => Results.Ok(req.Name));
            },
            environment);

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/typed", content, TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("System.", json, StringComparison.Ordinal);
        Assert.DoesNotContain("LineNumber", json, StringComparison.Ordinal);
        Assert.DoesNotContain("BytePositionInLine", json, StringComparison.Ordinal);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(HttpErrorCatalog.TypeUri(ErrorType.BadRequest), root.GetProperty("type").GetString());
        Assert.Equal("bad_request", root.GetProperty("title").GetString());
        Assert.Equal("the_request_was_invalid_or_cannot_be_otherwise_served",
            root.GetProperty("detail").GetString());

        if (Environment.Version.Major >= 11)
        {
            var errors = root.GetProperty("errors");
            var entry = Assert.Single(errors.EnumerateObject());
            Assert.Equal(expectedKey, entry.Name);
            Assert.Equal(expectedMessage, Assert.Single(entry.Value.EnumerateArray()).GetString());
        }
        else
        {
            Assert.False(root.TryGetProperty("errors", out _));
        }
    }
}

public sealed record TypedBodyRequest(
    string Name,
    int Count,
    TypedBodyAddress? Address,
    IReadOnlyList<TypedBodyItem>? Items);

public sealed record TypedBodyAddress(string? Zip);

public sealed record TypedBodyItem(string? Name);

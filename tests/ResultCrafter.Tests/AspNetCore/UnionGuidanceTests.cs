#if NET11_0_OR_GREATER
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.TestHost;
using ResultCrafter.AspNetCore;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     The README's "map at the edge" snippet for a C# union that holds an <see cref="Error" />: switch on the
///     union in the endpoint and map each case to an HTTP result.
/// </summary>
public sealed class UnionGuidanceTests
{
    [Theory]
    [InlineData("ann", HttpStatusCode.Created, null)]
    [InlineData("taken", HttpStatusCode.Conflict, "conflict")]
    public async Task UnionMappedAtTheEdge_ReturnsHouseShape(string name,
        HttpStatusCode expectedStatus,
        string? expectedTitle)
    {
        await using var app = TestApp.Build(
            b => b.Services.AddResultCrafter(),
            a =>
            {
                a.UseResultCrafter();
                // The README snippet.
                a.MapPost("/users/{name}", Results<Created<UnionUserDto>, ProblemHttpResult> (string name) =>
                    UnionUserService.Create(name) switch
                    {
                        UnionUserDto user => TypedResults.Created($"/users/{user.Id}", user),
                        Error error => error.ToProblemResult()
                    });
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.PostAsync($"/users/{name}", null, TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, response.StatusCode);
        if (expectedTitle is null) return;

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(expectedTitle, doc.RootElement.GetProperty("title").GetString());
        Assert.Equal("email_taken", doc.RootElement.GetProperty("detail").GetString());
    }
}

public sealed record UnionUserDto(int Id, string Name);

public readonly union CreateUnionUserResult(UnionUserDto, Error);

internal static class UnionUserService
{
    public static CreateUnionUserResult Create(string name)
    {
        return name == "taken" ? Error.Conflict("email_taken") : new UnionUserDto(1, name);
    }
}
#endif

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ResultCrafter.AspNetCore;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>Minimal API success results on the wire: status code, Location header and body.</summary>
public sealed class SuccessResponseTests
{
    [Theory]
    [InlineData("/ok", HttpStatusCode.OK, null, "5")]
    [InlineData("/created", HttpStatusCode.Created, "/items/1", "5")]
    [InlineData("/accepted-typed", HttpStatusCode.Accepted, "/status/2", "5")]
    [InlineData("/accepted-void", HttpStatusCode.Accepted, "/status/3", "")]
    [InlineData("/no-content", HttpStatusCode.NoContent, null, "")]
    public async Task Success_HasStatusLocationAndBody(string path, HttpStatusCode status, string? location,
        string body)
    {
        await using var app = TestApp.Build(
            b => b.Services.AddResultCrafter(),
            a =>
            {
                a.UseResultCrafter();
                a.MapGet("/ok", () => Result<int>.Ok(5).ToOkResult());
                a.MapGet("/created", () => Result<int>.Created("/items/1", 5).ToCreatedResult());
                a.MapGet("/accepted-typed", () => Result<int>.Accepted(5, "/status/2").ToAcceptedResult());
                a.MapGet("/accepted-void", () => Result.Accepted("/status/3").ToAcceptedResult());
                a.MapGet("/no-content", () => Result.NoContent().ToNoContentResult());
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal(location, response.Headers.Location?.OriginalString);
        Assert.Equal(body, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}

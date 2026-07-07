using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResultCrafter.AspNetCore;
using ResultCrafter.AspNetCore.EfCore;
using ResultCrafter.AspNetCore.Mvc;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     Integration tests pinning the transport status code to the ProblemDetails body.
///     Guards two fixed bugs: <c>ProblemActionResult</c> used to write a 404 body over a
///     200 transport status, and <c>EfCoreConcurrencyExceptionHandler</c> used to write a
///     409 body over the exception-handler middleware's 500 transport status.
/// </summary>
public sealed class TransportStatusCodeTests
{
    // ── Controller failure path: transport status must match the body ──────────

    [Fact]
    public async Task ControllerToOkResult_FailedResult_TransportStatusIs404WithProblemBody()
    {
        await using var app = TestApp.Build(
            b =>
            {
                b.Services.AddResultCrafter();
                b.Services.AddControllers()
                    .AddApplicationPart(typeof(TransportStatusCodeController).Assembly);
            },
            a =>
            {
                a.UseResultCrafter();
                a.MapControllers();
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/transport-status/missing",
            TestContext.Current.CancellationToken);

        // The transport status must match the ProblemDetails body (previously 200 + 404 body).
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = doc.RootElement;

        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Equal("not_found", root.GetProperty("title").GetString());
        Assert.Equal("item_42_not_found", root.GetProperty("detail").GetString());

        // Internal pipeline markers must be stripped before the body is written.
        Assert.False(root.TryGetProperty("x-rc", out _));
        Assert.False(root.TryGetProperty("x-rc-error-id", out _));
    }

    // ── EF Core concurrency path: 409 transport status, not 500 ────────────────

    [Fact]
    public async Task DbUpdateConcurrencyException_TransportStatusIs409WithConcurrencyConflictBody()
    {
        await using var app = TestApp.Build(
            b =>
            {
                b.Services.AddResultCrafter();
                b.Services.AddResultCrafterEfCore();
            },
            a =>
            {
                a.UseResultCrafter();
                a.MapGet("/concurrency",
                    IResult () => throw new DbUpdateConcurrencyException("token mismatch"));
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/concurrency", TestContext.Current.CancellationToken);

        // The transport status must match the ProblemDetails body (previously 500 + 409 body).
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = doc.RootElement;

        Assert.Equal(409, root.GetProperty("status").GetInt32());
        Assert.Equal("concurrency_conflict", root.GetProperty("title").GetString());
        Assert.Equal("concurrency_conflict", root.GetProperty("detail").GetString());
        Assert.False(root.TryGetProperty("x-rc", out _));
        Assert.False(root.TryGetProperty("x-rc-error-id", out _));
    }
}

/// <summary>MVC controller used by <see cref="TransportStatusCodeTests" />.</summary>
[ApiController]
[Route("transport-status")]
public sealed class TransportStatusCodeController : ControllerBase
{
    /// <summary>Always fails with a 404 <see cref="Error.NotFound" /> mapped via <c>ToOkResult</c>.</summary>
    [HttpGet("missing")]
    public ActionResult<string> GetMissing()
    {
        var result = Result<string>.Fail(Error.NotFound("item_42_not_found"));
        // Static invocation: this file imports both extension namespaces, which makes
        // instance-syntax ToOkResult ambiguous between the Minimal API and MVC surfaces.
        return ControllerResultExtensions.ToOkResult(result);
    }
}

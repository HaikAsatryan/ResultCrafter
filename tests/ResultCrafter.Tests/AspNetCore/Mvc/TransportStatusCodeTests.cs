using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ResultCrafter.AspNetCore;
using ResultCrafter.AspNetCore.Mvc;

namespace ResultCrafter.Tests.AspNetCore.Mvc;

/// <summary>
///     Integration test pinning the transport status code to the ProblemDetails body:
///     <c>ProblemActionResult</c> used to write a 404 body over a 200 transport status.
///     The EF Core handler's 409 transport status is covered by <c>EfCoreConcurrencyHandlerTests</c>.
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

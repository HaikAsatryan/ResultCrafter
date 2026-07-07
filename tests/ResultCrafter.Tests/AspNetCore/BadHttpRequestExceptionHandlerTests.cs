using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ResultCrafter.AspNetCore;

namespace ResultCrafter.Tests.AspNetCore;

public sealed class BadHttpRequestExceptionHandlerTests
{
    // ── Real binding failure: 400, not 500 (the dev/prod parity bug) ───────────

    [Fact]
    public async Task MissingRequiredParameter_MapsTo400_NotInternalServerError()
    {
        await using var app = TestApp.Build(
            b =>
            {
                b.Services.AddResultCrafter();
                // Force the Development behaviour deterministically: binding failures throw
                // instead of being silently turned into a 400 by the framework.
                b.Services.PostConfigure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
            },
            a =>
            {
                a.UseResultCrafter();
                a.MapGet("/bind", (int requiredId) => Results.Ok(requiredId));
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        // requiredId is omitted -> BadHttpRequestException with StatusCode 400.
        var response = await client.GetAsync("/bind", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var doc =
            JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = doc.RootElement;
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        // Normalized to the ResultCrafter house style: not the framework "Bad Request", and not the
        // catch-all 500 "internal_server_error".
        Assert.Equal("bad_request", root.GetProperty("title").GetString());
        Assert.Equal("the_request_was_invalid_or_cannot_be_otherwise_served",
            root.GetProperty("detail").GetString());
        // Internal pipeline markers are stripped from the body.
        Assert.False(root.TryGetProperty("x-rc", out _));
    }

    // ── Carries its own (non-400) status code ──────────────────────────────────

    [Fact]
    public async Task ThrownBadHttpRequest_CarriesItsOwnStatusCode()
    {
        await using var app = TestApp.Build(
            b => b.Services.AddResultCrafter(),
            a =>
            {
                a.UseResultCrafter();
                a.MapGet("/too-large",
                    IResult () => throw new BadHttpRequestException("payload too large",
                        StatusCodes.Status413PayloadTooLarge));
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/too-large", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode); // 413
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var doc =
            JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(413, doc.RootElement.GetProperty("status").GetInt32());
    }

    // ── Guard: non-BadHttpRequestException is left to the catch-all handler ────

    [Fact]
    public async Task NonBadHttpRequestException_IsNotHandled()
    {
        var handler = new BadHttpRequestExceptionHandler(NullLogger<BadHttpRequestExceptionHandler>.Instance);
        var context = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("boom"),
            CancellationToken.None);

        Assert.False(handled);
    }
}

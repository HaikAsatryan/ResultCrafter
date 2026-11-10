using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ResultCrafter.AspNetCore;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     A ResultCrafter error gets the house body whatever the registration order of <c>AddControllers()</c> and
///     <c>AddResultCrafter()</c>, whether the controller has <c>[ApiController]</c>, and whatever the client's
///     <c>Accept</c> header. The framework tries problem-details writers in registration order, and MVC's writer
///     or the non-JSON fallback would otherwise skip the hook that strips the internal markers.
/// </summary>
public sealed class ProblemWriterOrderTests
{
    public static TheoryData<bool, string, string> Cases()
    {
        var data = new TheoryData<bool, string, string>();
        foreach (var resultCrafterFirst in new[] { true, false })
        {
            foreach (var path in new[] { "/minimal", "/api-controller", "/plain-controller" })
            {
                foreach (var accept in new[] { "application/json", "application/xml" })
                {
                    data.Add(resultCrafterFirst, path, accept);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Error_IsHouseShaped_WhateverTheOrderAndAccept(bool resultCrafterFirst, string path,
        string accept)
    {
        await using var app = TestApp.Build(
            b =>
            {
                if (resultCrafterFirst) b.Services.AddResultCrafter();
                b.Services.AddControllers().AddApplicationPart(typeof(ApiLimitedController).Assembly);
                if (!resultCrafterFirst) b.Services.AddResultCrafter();
            },
            a =>
            {
                a.UseResultCrafter();
                a.MapGet("/minimal", () => ApiLimitedController.Limited.ToProblemResult());
                a.MapControllers();
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.ParseAdd(accept);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("30", response.Headers.RetryAfter?.ToString());
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("x-rc", body, StringComparison.Ordinal);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("too_many_requests", doc.RootElement.GetProperty("title").GetString());
        Assert.True(doc.RootElement.TryGetProperty("traceId", out _));
        Assert.True(doc.RootElement.TryGetProperty("requestId", out _));
    }

    // The exception handlers' own problems (unhandled exception, binding failure) carry the marker too, so a
    // non-JSON Accept header cannot make the framework skip the house body for them either. The detail proves the
    // binding failure was answered by its own handler: the catch-all would also return 400 here, with its own detail.
    [Theory]
    [InlineData("/crash", HttpStatusCode.InternalServerError, "internal_server_error", "an_unexpected_error_occurred")]
    [InlineData("/bind", HttpStatusCode.BadRequest, "bad_request",
        "the_request_was_invalid_or_cannot_be_otherwise_served")]
    public async Task HandlerProblem_IsHouseShaped_WithNonJsonAccept(string path, HttpStatusCode status,
        string title, string detail)
    {
        await using var app = TestApp.Build(
            b =>
            {
                b.Services.AddResultCrafter();
                // Binding failures throw, as in Development, so the BadHttpRequest handler answers them.
                b.Services.PostConfigure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
            },
            a =>
            {
                a.UseResultCrafter();
                a.MapGet("/crash", IResult () => throw new InvalidOperationException("boom"));
                a.MapGet("/bind", (int requiredId) => Results.Ok(requiredId));
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.ParseAdd("application/xml");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(title, doc.RootElement.GetProperty("title").GetString());
        Assert.Equal(detail, doc.RootElement.GetProperty("detail").GetString());
    }
}

/// <summary>Rate-limited controller with <c>[ApiController]</c>.</summary>
[ApiController]
[Route("api-controller")]
public sealed class ApiLimitedController : ControllerBase
{
    internal static Error Limited => Error.TooManyRequests(retryAfter: TimeSpan.FromSeconds(30));

    /// <summary>Always rate-limited.</summary>
    [HttpGet]
    public IActionResult Get()
    {
        return ResultCrafter.AspNetCore.Mvc.ControllerResultExtensions.ToProblemResult(Limited);
    }
}

/// <summary>Rate-limited controller without <c>[ApiController]</c>.</summary>
[Route("plain-controller")]
public sealed class PlainLimitedController : ControllerBase
{
    /// <summary>Always rate-limited.</summary>
    [HttpGet]
    public IActionResult Get()
    {
        return ResultCrafter.AspNetCore.Mvc.ControllerResultExtensions.ToProblemResult(ApiLimitedController.Limited);
    }
}

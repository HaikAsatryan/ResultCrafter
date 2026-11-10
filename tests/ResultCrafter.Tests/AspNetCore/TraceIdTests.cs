using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ResultCrafter.AspNetCore;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     Every error body carries exactly one trace id member, <c>traceId</c>, whatever the JSON naming policy. On
///     .NET 10 the framework's writer also adds it under the policy's name (<c>trace_id</c> for snake_case).
/// </summary>
public sealed class TraceIdTests
{
    [Theory]
    [InlineData("/framework")]
    [InlineData("/result")]
    public async Task SnakeCasePolicy_BodyHasOnlyTraceId(string path)
    {
        await using var app = TestApp.Build(
            b =>
            {
                b.Services.AddResultCrafter();
                b.Services.ConfigureHttpJsonOptions(o =>
                    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
            },
            a =>
            {
                a.UseResultCrafter();
                a.MapGet("/framework", () => Results.Problem(statusCode: 404));
                a.MapGet("/result", () => Error.NotFound().ToProblemResult());
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        Assert.True(doc.RootElement.TryGetProperty("traceId", out _));
        Assert.False(doc.RootElement.TryGetProperty("trace_id", out _));
    }

    // The test host normally runs without an Activity, which leaves traceId empty; set one so the value is
    // checked, not only the member's presence.
    [Fact]
    public async Task TraceId_IsTheRequestActivityId()
    {
        string? activityId = null;
        await using var app = TestApp.Build(
            b => b.Services.AddResultCrafter(),
            a =>
            {
                a.UseResultCrafter();
                a.Use(async (ctx, next) =>
                {
                    using var activity = new Activity("test-request").SetIdFormat(ActivityIdFormat.W3C).Start();
                    activityId = activity.Id;
                    ctx.Features.Set<IHttpActivityFeature>(new ActivityFeature(activity));
                    await next(ctx);
                });
                a.MapGet("/result", () => Error.NotFound().ToProblemResult());
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/result", TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.NotNull(activityId);
        Assert.Equal(activityId, doc.RootElement.GetProperty("traceId").GetString());
    }

    private sealed class ActivityFeature(Activity activity) : IHttpActivityFeature
    {
        public Activity Activity { get; set; } = activity;
    }
}

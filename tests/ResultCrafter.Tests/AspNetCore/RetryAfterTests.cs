using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ResultCrafter.AspNetCore;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     A 429 built with a retry delay sends the <c>Retry-After</c> header in whole seconds, rounded up. The
///     internal marker that carries it through the pipeline never reaches the body.
/// </summary>
public sealed class RetryAfterTests
{
    [Theory]
    [InlineData("/minimal/59.2", "60")]
    [InlineData("/controller/59.2", "60")]
    [InlineData("/minimal/none", null)]
    public async Task TooManyRequests_SendsRetryAfterHeader_NeverTheMarker(string path, string? expectedHeader)
    {
        await using var app = TestApp.Build(
            b =>
            {
                b.Services.AddResultCrafter();
                b.Services.AddControllers().AddApplicationPart(typeof(RetryAfterController).Assembly);
            },
            a =>
            {
                a.UseResultCrafter();
                a.MapGet("/minimal/{wait}", (string wait) => RetryAfterController.Limit(wait).ToProblemResult());
                a.MapControllers();
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(expectedHeader, response.Headers.RetryAfter?.ToString());
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("too_many_requests", doc.RootElement.GetProperty("title").GetString());
        Assert.DoesNotContain("x-rc", body, StringComparison.Ordinal);
    }
}

/// <summary>Returns a 429 from a controller action.</summary>
[ApiController]
[Route("controller")]
public sealed class RetryAfterController : ControllerBase
{
    internal static Error Limit(string wait)
    {
        return wait == "none"
            ? Error.TooManyRequests()
            : Error.TooManyRequests(retryAfter: TimeSpan.FromSeconds(double.Parse(wait,
                System.Globalization.CultureInfo.InvariantCulture)));
    }

    /// <summary>Always rate-limited.</summary>
    [HttpGet("{wait}")]
    public IActionResult Get(string wait)
    {
        return ResultCrafter.AspNetCore.Mvc.ControllerResultExtensions.ToProblemResult(Limit(wait));
    }
}

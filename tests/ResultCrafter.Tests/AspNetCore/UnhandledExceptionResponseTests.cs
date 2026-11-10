using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ResultCrafter.AspNetCore;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     The catch-all handler's body: the RFC 9110 type URI is always present, and the status follows what the
///     exception-handler middleware set (500, or the app's <c>StatusCodeSelector</c>), so body and transport agree.
/// </summary>
public sealed class UnhandledExceptionResponseTests
{
    [Theory]
    [InlineData(null, HttpStatusCode.InternalServerError, "internal_server_error",
        "https://tools.ietf.org/html/rfc9110#section-15.6.1")]
    [InlineData(503, HttpStatusCode.ServiceUnavailable, "Service Unavailable",
        "https://tools.ietf.org/html/rfc9110#section-15.6.4")]
    [InlineData(400, HttpStatusCode.BadRequest, "bad_request",
        "https://tools.ietf.org/html/rfc9110#section-15.5.1")]
    // A selector that picks a non-error status cannot turn an unhandled exception into a success.
    [InlineData(200, HttpStatusCode.InternalServerError, "internal_server_error",
        "https://tools.ietf.org/html/rfc9110#section-15.6.1")]
    public async Task UnhandledException_BodyMatchesTransportStatus(int? selected, HttpStatusCode status,
        string title, string type)
    {
        await using var app = TestApp.Build(
            b =>
            {
                b.Services.AddResultCrafter();
                if (selected is { } code)
                    b.Services.Configure<ExceptionHandlerOptions>(o => o.StatusCodeSelector = _ => code);
            },
            a =>
            {
                a.UseResultCrafter();
                a.MapGet("/crash", IResult () => throw new InvalidOperationException("boom"));
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/crash", TestContext.Current.CancellationToken);
        using var doc =
            JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = doc.RootElement;

        Assert.Equal(status, response.StatusCode);
        Assert.Equal((int)status, root.GetProperty("status").GetInt32());
        Assert.Equal(title, root.GetProperty("title").GetString());
        Assert.Equal(type, root.GetProperty("type").GetString());
        Assert.DoesNotContain("boom", root.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }
}

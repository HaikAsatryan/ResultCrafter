using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using ResultCrafter.AspNetCore;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     The 500 body shows exception details only where the mode allows it. <c>Sanitized</c> must hide them
///     even in Development, and <c>Auto</c> must hide them in Production.
/// </summary>
public sealed class ExceptionDetailModeTests
{
    private const string Secret = "secret_internal_state";

    [Theory]
    [InlineData("Development", ExceptionDetailMode.Auto)]
    [InlineData("Production", ExceptionDetailMode.IncludeExceptionDetails)]
    public async Task UnhandledException_DetailsShown(string environment, ExceptionDetailMode mode)
    {
        var (detail, _) = await GetServerError(environment, mode);

        Assert.Contains(Secret, detail, StringComparison.Ordinal);
    }

    // The documented Auto list. Each name matches exactly one rule, so every rule has to work on its own.
    [Theory]
    [InlineData("Staging")]
    [InlineData("Dev")]
    [InlineData("Local")]
    [InlineData("Test")]
    [InlineData("QA")]
    [InlineData("Stage")]
    [InlineData("UAT")]
    [InlineData("PreProd")]
    [InlineData("Sandbox")]
    [InlineData("Debug")]
    public async Task Auto_NonProductionEnvironmentName_ShowsDetails(string environment)
    {
        var (detail, _) = await GetServerError(environment, ExceptionDetailMode.Auto);

        Assert.Contains(Secret, detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Development", ExceptionDetailMode.Sanitized)]
    [InlineData("Production", ExceptionDetailMode.Auto)]
    public async Task UnhandledException_DetailsHidden(string environment, ExceptionDetailMode mode)
    {
        var (detail, body) = await GetServerError(environment, mode);

        Assert.Equal("an_unexpected_error_occurred", detail);
        Assert.DoesNotContain(Secret, body, StringComparison.Ordinal);
    }

    private static async Task<(string? Detail, string Body)> GetServerError(string environment,
        ExceptionDetailMode mode)
    {
        await using var app = TestApp.Build(
            b => b.Services.AddResultCrafter(o => o.ExceptionDetailMode = mode),
            a =>
            {
                a.UseResultCrafter();
                a.MapGet("/boom", IResult () => throw new InvalidOperationException(Secret));
            },
            environment);

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/boom", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);
        return (doc.RootElement.GetProperty("detail").GetString(), body);
    }
}
